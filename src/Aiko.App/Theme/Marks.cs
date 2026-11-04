using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aiko.App;

/// The marks of the states of a session, drawn the same in the list, the island and the notices.
static class Marks
{
    /// The width of the lines of the marks: as thin as the lines of the slayumind icon set, one unit
    /// in twenty-four, at the sizes they are drawn (D-332).
    public const double LineWidth = 0.75;

    /// The sign of "waits for you": two thin bars, drawn as thin as the lines of the slayumind icons
    /// (one in twenty-four), so they read as a quiet mark and not as a pause button (D-299, D-332).
    public static FrameworkElement Pause() => Line("M0.5,0.5 L0.5,9.5 M4.5,0.5 L4.5,9.5", "Caution");

    /// A mark of thin round lines in a colour of the palette.
    public static Path Line(string data, string tone = "Muted") => new()
    {
        Data = Geometry.Parse(data),
        Stroke = Tokens.Brush(tone),
        StrokeThickness = LineWidth,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
