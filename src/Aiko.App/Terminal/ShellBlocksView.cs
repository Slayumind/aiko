using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The output of a command line as blocks (D-363, D-412): each command with a rule on the left,
/// grey when it passed, red when it failed, orange while it runs; its command and time on the first
/// line, its output under it, and a long output folded to its first lines. Only the rows on screen
/// are drawn, so ten thousand lines cost what thirty do. The screen comes from the output thread;
/// every read of it takes its lock.
sealed class ShellBlocksView : FrameworkElement
{
    /// An output longer than this folds to FoldShows lines and a line "… N more".
    private const int FoldAbove = 40;
    private const int FoldShows = 20;
    /// The same air on every side of the blocks: left, right and above the first one.
    private const double Inset = 12;
    private const double RuleWidth = 2;
    private const double BlockGap = 10;

    private static readonly Brush Text = Brush(0xFA, 0xFA, 0xFA);
    private static readonly Brush Output = Brush(0xBD, 0xBD, 0xBD);
    private static readonly Brush Muted = Brush(0xA1, 0xA1, 0xA1);
    private static readonly Brush Raised = Brush(0x40, 0x40, 0x40);
    private static readonly Brush Destructive = Brush(0xFF, 0x64, 0x67);
    private static readonly Brush Caution = Brush(0xFE, 0x9A, 0x00);
    private static readonly Brush Ground = Brush(0x14, 0x14, 0x16);

    /// The sixteen colours of the terminal's theme (TerminalSurface), so a block looks like the terminal.
    private static readonly uint[] Palette =
    [
        0x171717, 0xFF6467, 0x00BC7D, 0xFE9A00, 0x11A9F6, 0xD477CD, 0x00D3F2, 0xA1A1A1,
        0x737373, 0xFF8270, 0xAFE773, 0xFE9A00, 0x11A9F6, 0xF7BCF0, 0x24DCC3, 0xFAFAFA,
    ];

    private readonly ShellScreen _screen;
    private readonly object _gate;
    private readonly HashSet<long> _unfolded = [];
    private readonly Typeface _face = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private readonly Typeface _bold = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly Dictionary<int, Brush> _colors = [];
    private List<Row> _rows = [];
    private double _offset;
    private bool _followsEnd = true;
    private double _fontSize = 13;

    public ShellBlocksView(ShellScreen screen, object gate)
    {
        _screen = screen;
        _gate = gate;
        ClipToBounds = true;
        Focusable = false;
        Cursor = Cursors.Arrow;
    }

    /// The font of the terminal in units of WPF, from its size in points.
    public void UseFont(double points)
    {
        _fontSize = points * 96 / 72;
        Refresh();
    }

    private double LineHeight => Math.Ceiling(_fontSize * 1.35);

    internal int RowsForCheck => _rows.Count;

    internal int BlockCountForCheck => _rows.Count(row => row.Kind == RowKind.Command);

    /// Opens every folded output, as a click on its line would.
    internal void UnfoldForCheck()
    {
        foreach (var row in _rows.Where(row => row.Kind == RowKind.More))
        {
            _unfolded.Add(row.Started);
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

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var index = (int)Math.Floor((e.GetPosition(this).Y + _offset - Inset) / LineHeight);
        if (index >= 0 && index < _rows.Count && _rows[index].Kind == RowKind.More)
        {
            _unfolded.Add(_rows[index].Started);
            _followsEnd = false;
            Refresh();
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Ground, null, new Rect(RenderSize));
        if (_rows.Count == 0)
        {
            return;
        }

        var height = LineHeight;
        var first = Math.Max(0, (int)((_offset - Inset) / height));
        var last = Math.Min(_rows.Count, first + (int)(ActualHeight / height) + 2);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        lock (_gate)
        {
            for (var i = first; i < last; i++)
            {
                var row = _rows[i];
                var top = Inset + (i * height) - _offset;
                if (row.Kind != RowKind.Gap)
                {
                    context.DrawRectangle(RuleOf(row.Block), null, new Rect(Inset, top, RuleWidth, height));
                }

                var x = Inset + RuleWidth + 10;
                switch (row.Kind)
                {
                    case RowKind.Command:
                        DrawCommand(context, row.Block, x, top, dpi);
                        break;
                    case RowKind.Output:
                        DrawLine(context, row.Line, x, top, dpi);
                        break;
                    case RowKind.More:
                        context.DrawText(Formatted(More(row.Line), Muted, _face, _fontSize * 0.9, dpi), new Point(x, top + 2));
                        break;
                }
            }
        }
    }

    private void DrawCommand(DrawingContext context, ShellBlock block, double x, double top, double dpi)
    {
        var meta = block.Running ? Strings.ShellRunning
            : block.Failed ? string.Join(" · ", new[] { string.Format(CultureInfo.CurrentCulture, Strings.ShellExitCode, block.ExitCode), Seconds(block) }.Where(part => part.Length > 0))
            : Seconds(block);
        var metaText = Formatted(meta, block.Running ? Caution : block.Failed ? Destructive : Muted, _face, _fontSize * 0.85, dpi);
        var metaX = Math.Max(x, ActualWidth - Inset - metaText.Width);
        var command = Formatted(block.Command, Text, _bold, _fontSize, dpi);
        command.MaxTextWidth = Math.Max(1, metaX - x - 12);
        command.MaxLineCount = 1;
        command.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(command, new Point(x, top));
        context.DrawText(metaText, new Point(metaX, top + 2));
    }

    private void DrawLine(DrawingContext context, int line, double x, double top, double dpi)
    {
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

    /// Every block as rows: its command, its output (folded when long), and a gap after it.
    private List<Row> Lay(IReadOnlyList<ShellBlock> blocks)
    {
        var rows = new List<Row>();
        foreach (var block in blocks)
        {
            rows.Add(new Row(RowKind.Command, block, 0));
            var folds = !block.Running && block.LineCount > FoldAbove && !_unfolded.Contains(block.StartedAt);
            var shown = folds ? FoldShows : block.LineCount;
            for (var line = 0; line < shown; line++)
            {
                rows.Add(new Row(RowKind.Output, block, block.FirstLine + line));
            }

            if (folds)
            {
                rows.Add(new Row(RowKind.More, block, block.LineCount - FoldShows));
            }

            rows.Add(new Row(RowKind.Gap, block, 0));
        }

        return rows;
    }

    private double MaxOffset() => Math.Max(0, Inset + (_rows.Count * LineHeight) - ActualHeight + BlockGap);

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

    private enum RowKind { Command, Output, More, Gap }

    /// One row on screen. Line is a row of the screen for Output and the count left for More.
    private readonly record struct Row(RowKind Kind, ShellBlock Block, int Line)
    {
        public long Started => Block.StartedAt;
    }
}
