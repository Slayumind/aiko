using System.Text;

namespace Aiko.Core.Terminal;

/// How a cell is drawn. A colour is -1 for the default, 0-255 for the palette, or
/// TrueColor | 0xRRGGBB.
public readonly record struct CellStyle(int Foreground, int Background, bool Bold, bool Dim, bool Italic, bool Underline, bool Inverse)
{
    public const int Default = -1;
    public const int TrueColor = 0x1000000;

    public static readonly CellStyle Plain = new(Default, Default, false, false, false, false, false);
}

/// A piece of a line with one style.
public sealed record ShellRun(string Text, CellStyle Style);

/// A mark of shell integration (OSC 133): A prompt, B command, C output, D end with its code. Row and
/// column are where the cursor stood; At is the time the mark came, in milliseconds.
public sealed record ShellMark(char Kind, int Row, int Column, int? Code, long At);

/// The screen of a command line as a terminal keeps it (D-412): every line since the start, the
/// cursor, the styles, and the marks the shell left. It follows what ConPTY sends, which is the
/// program's text in passthrough or a redraw with absolute cursor moves, so the same rows come out
/// either way. A full-screen program is not kept: the real terminal shows it.
/// Rows: spec/cases/shell-screen.
public sealed class ShellScreen
{
    private const int TabWidth = 8;

    /// A line longer than this many lines back is dropped from the start, with its marks.
    public const int MostLines = 50_000;

    private readonly List<Line> _lines = [new Line()];
    private readonly List<CellStyle> _styles = [CellStyle.Plain];
    private readonly Dictionary<CellStyle, int> _styleIds = new() { [CellStyle.Plain] = 0 };
    private readonly List<ShellMark> _marks = [];
    private readonly VtParser _parser;
    private int _columns;
    private int _rows;
    private int _top;
    private int _row;
    private int _column;
    private bool _pendingWrap;
    private CellStyle _style = CellStyle.Plain;
    private (int Row, int Column) _saved;
    private (int Row, int Column, int Top) _beforeFullScreen;
    private long _now;
    private int _dropped;

    public ShellScreen(int columns, int rows)
    {
        _columns = Math.Max(1, columns);
        _rows = Math.Max(1, rows);
        _parser = new VtParser(new Receiver(this));
    }

    /// A program uses the second screen of the terminal (vim, less): the window shows the terminal.
    public bool FullScreen { get; private set; }

    /// Raised when FullScreen changes.
    public event Action<bool>? FullScreenChanged;

    public int LineCount => _lines.Count;

    /// Lines dropped from the start since the screen began: a row of a mark is counted from here.
    public int Dropped => _dropped;

    public IReadOnlyList<ShellMark> Marks => _marks;

    public (int Row, int Column) Cursor => (_row, _column);

    public void Resize(int columns, int rows)
    {
        _columns = Math.Max(1, columns);
        _rows = Math.Max(1, rows);
        _top = Math.Clamp(_lines.Count - _rows, 0, _row);
        _column = Math.Min(_column, _columns - 1);
    }

    /// What the program wrote; now is the time in milliseconds, kept on the marks.
    public void Feed(string chunk, long now)
    {
        _now = now;
        _parser.Feed(chunk);
        Trim();
    }

    /// The text of a line without its trailing blanks.
    public string LineText(int row) => row >= 0 && row < _lines.Count ? _lines[row].Text() : "";

    /// Whether the line goes on in the next one: the terminal broke it by the width.
    public bool Wrapped(int row) => row >= 0 && row < _lines.Count && _lines[row].Wrapped;

    /// The line as pieces of one style each, trailing blanks left out.
    public IReadOnlyList<ShellRun> Runs(int row)
    {
        var runs = new List<ShellRun>();
        if (row < 0 || row >= _lines.Count)
        {
            return runs;
        }

        var line = _lines[row];
        var end = line.TextEnd();
        var text = new StringBuilder();
        var style = -1;
        for (var i = 0; i < end; i++)
        {
            var cell = line.Cells[i];
            if (cell.CodePoint == Cell.Continuation)
            {
                continue;
            }

            if (cell.Style != style && text.Length > 0)
            {
                runs.Add(new ShellRun(text.ToString(), _styles[style]));
                text.Clear();
            }

            style = cell.Style;
            text.Append(cell.CodePoint == 0 ? " " : char.ConvertFromUtf32(cell.CodePoint));
        }

        if (text.Length > 0)
        {
            runs.Add(new ShellRun(text.ToString(), _styles[style]));
        }

        return runs;
    }

    /// The text from one position up to another, lines broken by the width joined again.
    public string Text(int fromRow, int fromColumn, int toRow, int toColumn)
    {
        var text = new StringBuilder();
        for (var row = Math.Max(0, fromRow); row <= toRow && row < _lines.Count; row++)
        {
            var line = _lines[row];
            var start = row == fromRow ? fromColumn : 0;
            var end = row == toRow ? Math.Min(toColumn, line.TextEnd()) : line.TextEnd();
            for (var i = start; i < end; i++)
            {
                var point = line.Cells[i].CodePoint;
                if (point != Cell.Continuation)
                {
                    text.Append(point == 0 ? " " : char.ConvertFromUtf32(point));
                }
            }

            if (row < toRow && !line.Wrapped)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    // ---- what the parser found ----

    private void Print(int codePoint)
    {
        if (FullScreen)
        {
            return;
        }

        var width = CellWidth.Of(codePoint);
        if (_pendingWrap || _column + width > _columns)
        {
            Current().Wrapped = true;
            NewLine();
            _column = 0;
        }

        var line = Current();
        line.Set(_column, new Cell(codePoint, StyleId()));
        if (width == 2)
        {
            line.Set(_column + 1, new Cell(Cell.Continuation, StyleId()));
        }

        _column += width;
        if (_column >= _columns)
        {
            _column = _columns - 1;
            _pendingWrap = true;
        }
    }

    private void Control(char control)
    {
        if (FullScreen)
        {
            return;
        }

        switch (control)
        {
            case '\r':
                _column = 0;
                _pendingWrap = false;
                break;
            case '\n' or '\v' or '\f':
                NewLine();
                break;
            case '\b':
                _column = Math.Max(0, _column - 1);
                _pendingWrap = false;
                break;
            case '\t':
                _column = Math.Min(_columns - 1, ((_column / TabWidth) + 1) * TabWidth);
                break;
        }
    }

    private void Escape(char final)
    {
        if (FullScreen)
        {
            return;
        }

        switch (final)
        {
            case '7':
                _saved = (_row - _top, _column);
                break;
            case '8':
                MoveTo(_top + _saved.Row, _saved.Column);
                break;
            case 'M':
                MoveTo(Math.Max(_top, _row - 1), _column);
                break;
            case 'D':
                NewLine();
                break;
            case 'E':
                NewLine();
                _column = 0;
                break;
        }
    }

    private void Csi(IReadOnlyList<int> p, char marker, char final)
    {
        if (marker == '?')
        {
            if (final is 'h' or 'l')
            {
                Modes(p, final == 'h');
            }

            return;
        }

        if (FullScreen || marker != '\0')
        {
            return;
        }

        int Arg(int index, int fallback) => index < p.Count && p[index] > 0 ? p[index] : fallback;

        switch (final)
        {
            case 'H' or 'f':
                MoveTo(_top + Arg(0, 1) - 1, Arg(1, 1) - 1);
                break;
            case 'A':
                MoveTo(Math.Max(_top, _row - Arg(0, 1)), _column);
                break;
            case 'B':
                MoveTo(Math.Min(_top + _rows - 1, _row + Arg(0, 1)), _column);
                break;
            case 'C':
                MoveTo(_row, _column + Arg(0, 1));
                break;
            case 'D':
                MoveTo(_row, _column - Arg(0, 1));
                break;
            case 'E':
                MoveTo(Math.Min(_top + _rows - 1, _row + Arg(0, 1)), 0);
                break;
            case 'F':
                MoveTo(Math.Max(_top, _row - Arg(0, 1)), 0);
                break;
            case 'G' or '`':
                MoveTo(_row, Arg(0, 1) - 1);
                break;
            case 'd':
                MoveTo(_top + Arg(0, 1) - 1, _column);
                break;
            case 'K':
                EraseLine(p.Count > 0 && p[0] > 0 ? p[0] : 0);
                break;
            case 'J':
                EraseScreen(p.Count > 0 && p[0] > 0 ? p[0] : 0);
                break;
            case 'X':
                Current().Clear(_column, _column + Arg(0, 1), StyleId());
                break;
            case 'P':
                Current().Delete(_column, Arg(0, 1));
                break;
            case '@':
                Current().Insert(_column, Arg(0, 1));
                break;
            case 'S':
                ScrollUp(Arg(0, 1));
                break;
            case 'm':
                Sgr(p);
                break;
            case 's':
                _saved = (_row - _top, _column);
                break;
            case 'u':
                MoveTo(_top + _saved.Row, _saved.Column);
                break;
        }
    }

    private void Osc(string text)
    {
        if (FullScreen || !text.StartsWith("133;", StringComparison.Ordinal) || text.Length < 5)
        {
            return;
        }

        var kind = text[4];
        if (kind is not ('A' or 'B' or 'C' or 'D'))
        {
            return;
        }

        int? code = null;
        if (kind == 'D' && text.Length > 6 && int.TryParse(text.AsSpan(6).ToString().Split(';')[0], out var parsed))
        {
            code = parsed;
        }

        _marks.Add(new ShellMark(kind, _row + _dropped, _column, code, _now));
    }

    // ---- moving and erasing ----

    private void Modes(IReadOnlyList<int> p, bool on)
    {
        foreach (var mode in p)
        {
            if (mode is not (1049 or 1047 or 47) || on == FullScreen)
            {
                continue;
            }

            if (on)
            {
                _beforeFullScreen = (_row, _column, _top);
                FullScreen = true;
            }
            else
            {
                FullScreen = false;
                (_row, _column, _top) = _beforeFullScreen;
                _pendingWrap = false;
            }

            FullScreenChanged?.Invoke(on);
        }
    }

    private void MoveTo(int row, int column)
    {
        _row = Math.Max(0, row);
        while (_lines.Count <= _row)
        {
            _lines.Add(new Line());
        }

        _column = Math.Clamp(column, 0, _columns - 1);
        _pendingWrap = false;
    }

    private void NewLine()
    {
        _pendingWrap = false;
        _row++;
        if (_row >= _lines.Count)
        {
            _lines.Add(new Line());
        }

        if (_row > _top + _rows - 1)
        {
            _top = _row - _rows + 1;
        }
    }

    private void ScrollUp(int count)
    {
        _top += count;
        _row += count;
        while (_lines.Count < _top + _rows)
        {
            _lines.Add(new Line());
        }
    }

    private void EraseLine(int how)
    {
        var line = Current();
        switch (how)
        {
            case 0:
                line.Clear(_column, int.MaxValue, StyleId());
                line.Wrapped = false;
                break;
            case 1:
                line.Clear(0, _column + 1, StyleId());
                break;
            default:
                line.Clear(0, int.MaxValue, StyleId());
                line.Wrapped = false;
                break;
        }
    }

    private void EraseScreen(int how)
    {
        var bottom = Math.Min(_lines.Count, _top + _rows);
        switch (how)
        {
            case 0:
                EraseLine(0);
                for (var row = _row + 1; row < bottom; row++)
                {
                    _lines[row] = new Line();
                }

                break;
            case 1:
                for (var row = _top; row < _row; row++)
                {
                    _lines[row] = new Line();
                }

                EraseLine(1);
                break;
            default:
                for (var row = _top; row < bottom; row++)
                {
                    _lines[row] = new Line();
                }

                break;
        }
    }

    private void Sgr(IReadOnlyList<int> p)
    {
        if (p.Count == 0)
        {
            _style = CellStyle.Plain;
            return;
        }

        for (var i = 0; i < p.Count; i++)
        {
            var code = Math.Max(0, p[i]);
            switch (code)
            {
                case 0: _style = CellStyle.Plain; break;
                case 1: _style = _style with { Bold = true }; break;
                case 2: _style = _style with { Dim = true }; break;
                case 3: _style = _style with { Italic = true }; break;
                case 4: _style = _style with { Underline = true }; break;
                case 7: _style = _style with { Inverse = true }; break;
                case 22: _style = _style with { Bold = false, Dim = false }; break;
                case 23: _style = _style with { Italic = false }; break;
                case 24: _style = _style with { Underline = false }; break;
                case 27: _style = _style with { Inverse = false }; break;
                case >= 30 and <= 37: _style = _style with { Foreground = code - 30 }; break;
                case 39: _style = _style with { Foreground = CellStyle.Default }; break;
                case >= 40 and <= 47: _style = _style with { Background = code - 40 }; break;
                case 49: _style = _style with { Background = CellStyle.Default }; break;
                case >= 90 and <= 97: _style = _style with { Foreground = code - 90 + 8 }; break;
                case >= 100 and <= 107: _style = _style with { Background = code - 100 + 8 }; break;
                case 38 or 48:
                    var (color, used) = ExtendedColor(p, i + 1);
                    i += used;
                    _style = code == 38 ? _style with { Foreground = color } : _style with { Background = color };
                    break;
            }
        }
    }

    /// 5;n is the palette, 2;r;g;b a true colour. Returns the colour and how many numbers it took.
    private static (int Color, int Used) ExtendedColor(IReadOnlyList<int> p, int at)
    {
        if (at < p.Count && p[at] == 5 && at + 1 < p.Count)
        {
            return (Math.Clamp(p[at + 1], 0, 255), 2);
        }

        if (at < p.Count && p[at] == 2 && at + 3 < p.Count)
        {
            var rgb = (Math.Clamp(p[at + 1], 0, 255) << 16) | (Math.Clamp(p[at + 2], 0, 255) << 8) | Math.Clamp(p[at + 3], 0, 255);
            return (CellStyle.TrueColor | rgb, 4);
        }

        return (CellStyle.Default, Math.Max(0, p.Count - at));
    }

    private Line Current()
    {
        while (_lines.Count <= _row)
        {
            _lines.Add(new Line());
        }

        return _lines[_row];
    }

    private int StyleId()
    {
        if (!_styleIds.TryGetValue(_style, out var id))
        {
            id = _styles.Count;
            _styles.Add(_style);
            _styleIds[_style] = id;
        }

        return id;
    }

    /// Keeps the screen within MostLines: the oldest lines go, and so do the marks on them.
    private void Trim()
    {
        var extra = _lines.Count - MostLines;
        if (extra <= 0)
        {
            return;
        }

        _lines.RemoveRange(0, extra);
        _row -= extra;
        _top = Math.Max(0, _top - extra);
        _dropped += extra;
        _marks.RemoveAll(mark => mark.Row < _dropped);
    }

    private readonly record struct Cell(int CodePoint, int Style)
    {
        /// The second half of a wide character.
        public const int Continuation = -1;
    }

    private sealed class Line
    {
        public Cell[] Cells = [];
        public int Length;
        public bool Wrapped;

        public void Set(int column, Cell cell)
        {
            Grow(column + 1);
            Cells[column] = cell;
        }

        public void Clear(int from, int to, int style)
        {
            to = Math.Min(to, Length);
            for (var i = Math.Max(0, from); i < to; i++)
            {
                Cells[i] = new Cell(0, style);
            }
        }

        public void Delete(int at, int count)
        {
            if (at >= Length)
            {
                return;
            }

            count = Math.Min(count, Length - at);
            Array.Copy(Cells, at + count, Cells, at, Length - at - count);
            Length -= count;
        }

        public void Insert(int at, int count)
        {
            if (at >= Length)
            {
                return;
            }

            Grow(Length + count);
            Array.Copy(Cells, at, Cells, at + count, Length - at - count);
            for (var i = at; i < at + count; i++)
            {
                Cells[i] = default;
            }
        }

        public int TextEnd()
        {
            var end = Length;
            while (end > 0 && Cells[end - 1].CodePoint is 0 or ' ')
            {
                end--;
            }

            return end;
        }

        public string Text()
        {
            var text = new StringBuilder();
            var end = TextEnd();
            for (var i = 0; i < end; i++)
            {
                var point = Cells[i].CodePoint;
                if (point != Cell.Continuation)
                {
                    text.Append(point == 0 ? " " : char.ConvertFromUtf32(point));
                }
            }

            return text.ToString();
        }

        private void Grow(int length)
        {
            if (length > Cells.Length)
            {
                Array.Resize(ref Cells, Math.Max(length, Math.Max(16, Cells.Length * 2)));
            }

            Length = Math.Max(Length, length);
        }
    }

    private sealed class Receiver(ShellScreen screen) : IVtReceiver
    {
        public void Print(int codePoint) => screen.Print(codePoint);

        public void Control(char control) => screen.Control(control);

        public void Csi(IReadOnlyList<int> parameters, char marker, char final) => screen.Csi(parameters, marker, final);

        public void Escape(char final) => screen.Escape(final);

        public void Osc(string text) => screen.Osc(text);
    }
}

/// How many cells a character takes: two for the wide ones of East Asia and most emoji.
public static class CellWidth
{
    public static int Of(int codePoint) =>
        codePoint is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1FAFF
            or >= 0x20000 and <= 0x3FFFD
            ? 2
            : 1;
}
