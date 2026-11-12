using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The output of a command line as blocks (D-363, D-412): each command with a rule on the left,
/// grey when it passed, red when it failed, orange while it runs; its command and time on the first
/// line, its output under it, and a long output folded to its first lines. Under the mouse a block
/// offers Copy, Fold and, when it failed, Explain; the answer of Claude Code stands under it. Only
/// the rows on screen are drawn, so ten thousand lines cost what thirty do. The screen comes from
/// the output thread; every read of it takes its lock.
sealed class ShellBlocksView : FrameworkElement
{
    /// An output longer than this folds to FoldShows lines and a line "… N more".
    private const int FoldAbove = 40;
    private const int FoldShows = 20;

    /// The same air on every side of the blocks: left, right and above the first one.
    private const double Inset = 12;
    private const double RuleWidth = 2;
    private const double TextGap = 10;
    private const double BlockGap = 10;

    private static readonly Brush Text = Brush(0xFA, 0xFA, 0xFA);
    private static readonly Brush Output = Brush(0xBD, 0xBD, 0xBD);
    private static readonly Brush Muted = Brush(0xA1, 0xA1, 0xA1);
    private static readonly Brush Raised = Brush(0x40, 0x40, 0x40);
    private static readonly Brush Destructive = Brush(0xFF, 0x64, 0x67);
    private static readonly Brush Caution = Brush(0xFE, 0x9A, 0x00);
    private static readonly Brush Ground = Brush(0x14, 0x14, 0x16);
    private static readonly Brush Hovered = Brush(0x1B, 0x1B, 0x1D);
    private static readonly Brush Match = Brush(0x4A, 0x4A, 0x4A);
    private static readonly Brush MatchNow = Brush(0x7A, 0x52, 0x10);
    private static readonly Pen Edge = new(Brush(0x2A, 0x2A, 0x2C), 1);
    private static readonly Pen EdgeMain = new(Brush(0x3B, 0x3B, 0x3D), 1);

    /// The sixteen colours of the terminal's theme (TerminalSurface), so a block looks like the terminal.
    private static readonly uint[] Palette =
    [
        0x171717, 0xFF6467, 0x00BC7D, 0xFE9A00, 0x11A9F6, 0xD477CD, 0x00D3F2, 0xA1A1A1,
        0x737373, 0xFF8270, 0xAFE773, 0xFE9A00, 0x11A9F6, 0xF7BCF0, 0x24DCC3, 0xFAFAFA,
    ];

    private readonly ShellScreen _screen;
    private readonly object _gate;
    private readonly HashSet<long> _unfolded = [];
    private readonly HashSet<long> _folded = [];
    private readonly Dictionary<long, string?> _explanations = [];
    private readonly HashSet<long> _asking = [];
    private readonly List<(Rect Area, Action Act)> _buttons = [];
    private readonly Typeface _face = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private readonly Typeface _bold = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly Typeface _sans = new(Tokens.Get<FontFamily>("Sans"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private readonly Dictionary<int, Brush> _colors = [];
    private readonly DispatcherTimer _copiedTimer;
    private List<Row> _rows = [];
    private List<(int Line, int Start, int Length)> _matches = [];
    private int _match = -1;
    private string _find = "";
    private double _offset;
    private bool _followsEnd = true;
    private double _fontSize = 13;
    private double _scale = 1;
    private double _charWidth = 7.8;
    private long? _hover;
    private long? _copied;

    public ShellBlocksView(ShellScreen screen, object gate)
    {
        _screen = screen;
        _gate = gate;
        ClipToBounds = true;
        Focusable = false;
        Cursor = Cursors.Arrow;
        _copiedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            _copied = null;
            InvalidateVisual();
        };
    }

    /// A failed command asks for an explanation: the block and its output as text.
    public event Action<ShellBlock, string>? ExplainAsked;

    /// The folder of the session, for a file named in the output without its folder.
    public string Folder { get; set; } = "";

    /// What the search found now: "3 of 12", "Nothing found", or empty without a search.
    public string FindStatus => _find.Length == 0 ? ""
        : _matches.Count == 0 ? Strings.ShellFindNone
        : string.Format(CultureInfo.CurrentCulture, Strings.ShellFindCount, _match + 1, _matches.Count);

    /// The font of the terminal in points, and the zoom of the window for the buttons.
    public void UseFont(double points, double scale)
    {
        _fontSize = points * 96 / 72;
        _scale = scale;
        _charWidth = Formatted("0", Text, _face, _fontSize, 1).WidthIncludingTrailingWhitespace;
        Refresh();
    }

    private double LineHeight => Math.Ceiling(_fontSize * 1.35);

    private double TextLeft => Inset + RuleWidth + TextGap;

    /// Where the first row starts: the inset, and the height of the search while it is open, so the
    /// search covers no block.
    private double Top => Inset + _room;

    private double _room;

    /// Room above the blocks for the search; zero when it is closed.
    public void MakeRoom(double height)
    {
        _room = height;
        Refresh();
    }

    /// New output: the rows are counted again and drawn. Called on the window's thread.
    public void Refresh()
    {
        lock (_gate)
        {
            _rows = Lay(ShellBlocks.Of(_screen));
        }

        if (_followsEnd)
        {
            _offset = MaxOffset();
        }

        _offset = Math.Clamp(_offset, 0, MaxOffset());
        InvalidateVisual();
    }

    /// The answer of Claude Code for a block, or null when none came.
    public void ShowExplanation(long block, string? answer)
    {
        _asking.Remove(block);
        _explanations[block] = answer;
        Refresh();
    }

    // ---- search ----

    /// Every place of the text in the output of the blocks, without case; the last one is shown.
    public void Find(string text)
    {
        _find = text;
        _matches = [];
        _match = -1;
        if (text.Length > 0)
        {
            lock (_gate)
            {
                foreach (var block in ShellBlocks.Of(_screen))
                {
                    for (var line = block.FirstLine; line < block.EndLine; line++)
                    {
                        var words = _screen.LineText(line);
                        for (var at = words.IndexOf(text, StringComparison.CurrentCultureIgnoreCase); at >= 0; at = words.IndexOf(text, at + text.Length, StringComparison.CurrentCultureIgnoreCase))
                        {
                            _matches.Add((line, at, text.Length));
                        }
                    }
                }
            }

            _match = _matches.Count > 0 ? _matches.Count - 1 : -1;
        }

        Reveal();
    }

    /// The next place (step 1) or the one before (step -1), going round.
    public void FindNext(int step)
    {
        if (_matches.Count == 0)
        {
            return;
        }

        _match = (_match + step + _matches.Count) % _matches.Count;
        Reveal();
    }

    /// Opens the block of the place shown now and scrolls to it.
    private void Reveal()
    {
        if (_match < 0)
        {
            Refresh();
            return;
        }

        var line = _matches[_match].Line;
        lock (_gate)
        {
            if (ShellBlocks.Of(_screen).FirstOrDefault(block => line >= block.FirstLine && line < block.EndLine) is { } owner)
            {
                _folded.Remove(owner.StartedAt);
                _unfolded.Add(owner.StartedAt);
            }
        }

        _followsEnd = false;
        Refresh();
        var index = _rows.FindIndex(row => row.Kind == RowKind.Output && row.Line == line);
        if (index >= 0)
        {
            _offset = Math.Clamp(Top + (index * LineHeight) - (ActualHeight / 3), 0, MaxOffset());
            _followsEnd = _offset >= MaxOffset() - 1;
        }

        InvalidateVisual();
    }

    // ---- the mouse ----

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _offset = Math.Clamp(_offset - (e.Delta / 120.0 * 3 * LineHeight), 0, MaxOffset());
        _followsEnd = _offset >= MaxOffset() - 1;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var at = e.GetPosition(this);
        var row = RowAt(at.Y);
        long? hover = row is { Kind: not RowKind.Gap } found ? found.Block.StartedAt : null;
        if (hover != _hover)
        {
            _hover = hover;
            InvalidateVisual();
        }

        var overButton = _buttons.Any(button => button.Area.Contains(at));
        var overLink = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && row is { Kind: RowKind.Output } output && LinkAt(output.Line, at.X) is not null;
        Cursor = overButton || overLink ? Cursors.Hand : Cursors.Arrow;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = null;
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var at = e.GetPosition(this);
        foreach (var (area, act) in _buttons)
        {
            if (area.Contains(at))
            {
                act();
                e.Handled = true;
                return;
            }
        }

        if (RowAt(at.Y) is not { } row)
        {
            return;
        }

        if (row.Kind == RowKind.More)
        {
            _unfolded.Add(row.Block.StartedAt);
            _followsEnd = false;
            Refresh();
            e.Handled = true;
        }
        else if (row.Kind == RowKind.Output && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && LinkAt(row.Line, at.X) is { } link)
        {
            ShellLinks.Open(link, Folder);
            e.Handled = true;
        }
    }

    private Row? RowAt(double y)
    {
        var index = (int)Math.Floor((y + _offset - Top) / LineHeight);
        return index >= 0 && index < _rows.Count ? _rows[index] : null;
    }

    /// The word under the mouse on a line of output, when it is an address or a file.
    private string? LinkAt(int line, double x)
    {
        string text;
        lock (_gate)
        {
            text = _screen.LineText(line);
        }

        var column = (int)((x - TextLeft) / _charWidth);
        return ShellLinks.At(text, column);
    }

    // ---- the buttons ----

    private void Copy(ShellBlock block)
    {
        string output;
        lock (_gate)
        {
            output = block.LineCount > 0 ? _screen.Text(block.FirstLine, 0, block.EndLine - 1, int.MaxValue) : "";
        }

        try
        {
            Clipboard.SetText(output.Length > 0 ? output : block.Command);
            _copied = block.StartedAt;
            _copiedTimer.Stop();
            _copiedTimer.Start();
            InvalidateVisual();
        }
        catch (COMException)
        {
            // Another program holds the clipboard; the next click tries again.
        }
    }

    private void Fold(ShellBlock block)
    {
        if (!_folded.Remove(block.StartedAt))
        {
            _folded.Add(block.StartedAt);
        }

        _followsEnd = false;
        Refresh();
    }

    private void Explain(ShellBlock block)
    {
        if (_asking.Contains(block.StartedAt))
        {
            return;
        }

        string output;
        lock (_gate)
        {
            output = block.LineCount > 0 ? _screen.Text(block.FirstLine, 0, block.EndLine - 1, int.MaxValue) : "";
        }

        _asking.Add(block.StartedAt);
        _explanations.Remove(block.StartedAt);
        Refresh();
        ExplainAsked?.Invoke(block, output);
    }

    // ---- drawing ----

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Ground, null, new Rect(RenderSize));
        _buttons.Clear();
        if (_rows.Count == 0)
        {
            return;
        }

        var height = LineHeight;
        var first = Math.Max(0, (int)((_offset - Top) / height));
        var last = Math.Min(_rows.Count, first + (int)(ActualHeight / height) + 2);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        lock (_gate)
        {
            for (var i = first; i < last; i++)
            {
                var row = _rows[i];
                var top = Top + (i * height) - _offset;
                if (row.Kind == RowKind.Gap)
                {
                    continue;
                }

                var key = row.Block.StartedAt;
                if (_hover == key)
                {
                    context.DrawRectangle(Hovered, null, new Rect(Inset, top, Math.Max(0, ActualWidth - (2 * Inset)), height));
                }

                context.DrawRectangle(RuleOf(row.Block), null, new Rect(Inset, top, RuleWidth, height));
                switch (row.Kind)
                {
                    case RowKind.Command:
                        DrawCommand(context, row.Block, top, dpi);
                        break;
                    case RowKind.Output:
                        DrawMatches(context, row.Line, top);
                        DrawLine(context, row.Line, top, dpi);
                        break;
                    case RowKind.More:
                        context.DrawText(Formatted(More(row.Line), Muted, _face, _fontSize * 0.9, dpi), new Point(TextLeft, top + 2));
                        break;
                    case RowKind.Note:
                        context.DrawText(Formatted(row.Note ?? "", row.Line < 0 ? Destructive : Text, _sans, _fontSize, dpi), new Point(TextLeft, top));
                        break;
                }
            }
        }
    }

    private void DrawCommand(DrawingContext context, ShellBlock block, double top, double dpi)
    {
        var meta = block.Running ? Strings.ShellRunning
            : block.Failed ? string.Join(" · ", new[] { string.Format(CultureInfo.CurrentCulture, Strings.ShellExitCode, block.ExitCode), Seconds(block) }.Where(part => part.Length > 0))
            : Seconds(block);
        var metaText = Formatted(meta, block.Running ? Caution : block.Failed ? Destructive : Muted, _face, _fontSize * 0.85, dpi);
        var right = ActualWidth - Inset - 6;
        var metaX = Math.Max(TextLeft, right - metaText.Width);
        context.DrawText(metaText, new Point(metaX, top + ((LineHeight - metaText.Height) / 2)));

        var commandEnd = metaX - 12;
        if (_hover == block.StartedAt)
        {
            commandEnd = DrawButtons(context, block, metaX - 12, top, dpi) - 12;
        }

        var command = Formatted(block.Command, Text, _bold, _fontSize, dpi);
        command.MaxTextWidth = Math.Max(1, commandEnd - TextLeft);
        command.MaxLineCount = 1;
        command.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(command, new Point(TextLeft, top));
    }

    /// The buttons of a block under the mouse, laid from the right; returns where they start.
    private double DrawButtons(DrawingContext context, ShellBlock block, double right, double top, double dpi)
    {
        var key = block.StartedAt;
        var buttons = new List<(string Label, bool Main, Action Act)>
        {
            (_copied == key ? Strings.ShellCopied : Strings.ShellCopy, false, () => Copy(block)),
        };
        if (block.LineCount > 0)
        {
            buttons.Add((_folded.Contains(key) ? Strings.ShellUnfold : Strings.ShellFold, false, () => Fold(block)));
        }

        if (block.Failed)
        {
            buttons.Add((Strings.ShellExplain, true, () => Explain(block)));
        }

        var size = 11.5 * _scale;
        var height = Math.Min(LineHeight - 2, 20 * _scale);
        var x = right;
        for (var i = buttons.Count - 1; i >= 0; i--)
        {
            var (label, main, act) = buttons[i];
            var text = Formatted(label, main ? Text : Muted, _sans, size, dpi);
            var width = text.WidthIncludingTrailingWhitespace + (12 * _scale);
            x -= width;
            var area = new Rect(x, top + ((LineHeight - height) / 2), width, height);
            context.DrawRoundedRectangle(Ground, main ? EdgeMain : Edge, area, 5 * _scale, 5 * _scale);
            context.DrawText(text, new Point(x + (6 * _scale), area.Top + ((height - text.Height) / 2)));
            _buttons.Add((area, act));
            x -= 4 * _scale;
        }

        return x;
    }

    private void DrawMatches(DrawingContext context, int line, double top)
    {
        for (var i = 0; i < _matches.Count; i++)
        {
            var (at, start, length) = _matches[i];
            if (at == line)
            {
                context.DrawRectangle(i == _match ? MatchNow : Match, null, new Rect(TextLeft + (start * _charWidth), top, length * _charWidth, LineHeight));
            }
        }
    }

    private void DrawLine(DrawingContext context, int line, double top, double dpi)
    {
        var x = TextLeft;
        foreach (var run in _screen.Runs(line))
        {
            var text = Formatted(run.Text, ColorOf(run.Style), run.Style.Bold ? _bold : _face, _fontSize, dpi);
            context.DrawText(text, new Point(x, top));
            x += text.WidthIncludingTrailingWhitespace;
            if (x > ActualWidth)
            {
                break;
            }
        }
    }

    /// Every block as rows: its command, its output (folded when long or by the person), the answer
    /// of Claude Code when there is one, and a gap after it.
    private List<Row> Lay(IReadOnlyList<ShellBlock> blocks)
    {
        var rows = new List<Row>();
        var width = (int)Math.Max(20, (ActualWidth - TextLeft - Inset) / Math.Max(1, _charWidth * 0.9));
        foreach (var block in blocks)
        {
            var key = block.StartedAt;
            rows.Add(new Row(RowKind.Command, block, 0));
            if (!_folded.Contains(key))
            {
                var folds = !block.Running && block.LineCount > FoldAbove && !_unfolded.Contains(key);
                var shown = folds ? FoldShows : block.LineCount;
                for (var line = 0; line < shown; line++)
                {
                    rows.Add(new Row(RowKind.Output, block, block.FirstLine + line));
                }

                if (folds)
                {
                    rows.Add(new Row(RowKind.More, block, block.LineCount - FoldShows));
                }
            }

            if (_asking.Contains(key))
            {
                rows.Add(new Row(RowKind.Note, block, 0, Strings.ShellExplaining));
            }
            else if (_explanations.TryGetValue(key, out var answer))
            {
                if (answer is null)
                {
                    rows.Add(new Row(RowKind.Note, block, -1, Strings.ShellExplainFailed));
                }
                else
                {
                    rows.AddRange(ShellAsk.Wrap(answer, width).Select(text => new Row(RowKind.Note, block, 0, text)));
                }
            }

            rows.Add(new Row(RowKind.Gap, block, 0));
        }

        return rows;
    }

    private double MaxOffset() => Math.Max(0, Top + (_rows.Count * LineHeight) - ActualHeight + BlockGap);

    private static Brush RuleOf(ShellBlock block) => block.Running ? Caution : block.Failed ? Destructive : Raised;

    /// Under a minute in seconds with a tenth, as in the mockup ("0,2 с"); longer as minutes and seconds.
    private static string Seconds(ShellBlock block)
    {
        if (!block.Timed)
        {
            return "";
        }

        var milliseconds = (block.EndedAt ?? block.StartedAt) - block.StartedAt;
        return milliseconds < 60_000
            ? string.Format(CultureInfo.CurrentCulture, Strings.ShellSeconds, (milliseconds / 1000.0).ToString("0.0", CultureInfo.CurrentCulture))
            : $"{milliseconds / 60_000}:{milliseconds / 1000 % 60:00}";
    }

    private static string More(int count) => string.Format(CultureInfo.CurrentCulture, CountForms.Of(count) switch
    {
        CountForm.One => Strings.ShellMoreOne,
        CountForm.Few => Strings.ShellMoreFew,
        _ => Strings.ShellMoreMany,
    }, count);

    private Brush ColorOf(CellStyle style)
    {
        var color = style.Foreground;
        if (color == CellStyle.Default)
        {
            return style.Dim ? Muted : Output;
        }

        if (!_colors.TryGetValue(color, out var brush))
        {
            var rgb = color >= CellStyle.TrueColor ? (uint)(color & 0xFFFFFF)
                : color < 16 ? Palette[color]
                : Cube(color);
            brush = Brush((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            _colors[color] = brush;
        }

        return brush;
    }

    /// The 216 colours of the cube and the 24 greys of the xterm palette.
    private static uint Cube(int index)
    {
        if (index >= 232)
        {
            var grey = (uint)(8 + ((index - 232) * 10));
            return (grey << 16) | (grey << 8) | grey;
        }

        var n = index - 16;
        uint Level(int value) => value == 0 ? 0u : (uint)(55 + (value * 40));
        return (Level(n / 36) << 16) | (Level(n / 6 % 6) << 8) | Level(n % 6);
    }

    private static FormattedText Formatted(string text, Brush brush, Typeface face, double size, double dpi) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush, dpi);

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    // ---- doors for --try-shell-view ----

    internal int RowsForCheck => _rows.Count;

    internal int BlockCountForCheck => _rows.Count(row => row.Kind == RowKind.Command);

    internal int NoteRowsForCheck => _rows.Count(row => row.Kind == RowKind.Note);

    internal int MatchesForCheck => _matches.Count;

    /// Opens every folded output, as a click on its line would.
    internal void UnfoldForCheck()
    {
        foreach (var row in _rows.Where(row => row.Kind == RowKind.More))
        {
            _unfolded.Add(row.Block.StartedAt);
        }

        Refresh();
    }

    /// Scrolls as the wheel would, by units; a large negative number goes to the top.
    internal void ScrollForCheck(double units)
    {
        _offset = Math.Clamp(_offset + units, 0, MaxOffset());
        _followsEnd = false;
        InvalidateVisual();
    }

    /// Shows the buttons of a block, as the mouse over it would, by its place in the list.
    internal void HoverForCheck(int block)
    {
        lock (_gate)
        {
            _hover = ShellBlocks.Of(_screen)[block].StartedAt;
        }

        InvalidateVisual();
    }

    /// Presses a button of a block as a click would: Copy, Fold or Explain, by its place in the list.
    internal void PressForCheck(int block, string label)
    {
        ShellBlock chosen;
        lock (_gate)
        {
            chosen = ShellBlocks.Of(_screen)[block];
        }

        if (label == Strings.ShellCopy)
        {
            Copy(chosen);
        }
        else if (label == Strings.ShellFold)
        {
            Fold(chosen);
        }
        else if (label == Strings.ShellExplain)
        {
            Explain(chosen);
        }
    }

    private enum RowKind { Command, Output, More, Note, Gap }

    /// One row on screen. Line is a row of the screen for Output, the count left for More, and -1 for
    /// a Note that tells of a failure.
    private readonly record struct Row(RowKind Kind, ShellBlock Block, int Line, string? Note = null);
}
