namespace Aiko.Core.Terminal;

/// The zoom of the terminal window in percent (D-384). The text, the icons, the gaps and the font of
/// the terminal grow together, so they keep one scale. Rows: spec/cases/window-zoom.
public static class WindowZoom
{
    public static readonly IReadOnlyList<int> Steps = [100, 115, 130];

    /// A little larger than the plain size: the terminal holds a lot of text to read.
    public const int Default = 115;

    /// The text of the window in units at 100 %: TextNumber of the theme.
    private const double WindowText = 13;

    /// The next step up, or the same at the top.
    public static int Larger(int zoom)
    {
        var at = IndexOf(zoom);
        return Steps[Math.Min(at + 1, Steps.Count - 1)];
    }

    /// The next step down, or the same at the bottom.
    public static int Smaller(int zoom)
    {
        var at = IndexOf(zoom);
        return Steps[Math.Max(at - 1, 0)];
    }

    /// A value from the settings file: one of the steps, or the default.
    public static int Read(int stored) => Steps.Contains(stored) ? stored : Default;

    /// The font of the terminal in whole points: the control of Windows Terminal takes points, and
    /// the text of the terminal should be as large as the text of the window.
    public static int TerminalPoints(int zoom) =>
        (int)Math.Round(WindowText * Read(zoom) / 100 * 72 / 96, MidpointRounding.AwayFromZero);

    /// The zoom as a factor for a scale: 1.15 for 115.
    public static double Scale(int zoom) => Read(zoom) / 100.0;

    private static int IndexOf(int zoom)
    {
        for (var at = 0; at < Steps.Count; at++)
        {
            if (Steps[at] == Read(zoom))
            {
                return at;
            }
        }

        return 0;
    }
}
