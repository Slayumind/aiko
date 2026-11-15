namespace Aiko.App;

/// PROBE, to be removed: follows the icons of the taskbar button in the log, to find why the button
/// turns black between the rings and a face while a game runs. Writes when an icon is given to the
/// window, when someone asks the window for its icon, and when an icon is destroyed, each with the
/// age of the icon.
static class IconProbe
{
    private static readonly Dictionary<IntPtr, long> Given = new();
    private static IntPtr _lastAsked;

    /// How long a replaced icon lives. AIKO_ICON_KEEP_SECONDS changes it for the probe.
    public static TimeSpan KeepReplaced { get; } =
        double.TryParse(Environment.GetEnvironmentVariable("AIKO_ICON_KEEP_SECONDS"), System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(3);

    public static void Started() => Log.Write($"icon probe: replaced icons live {KeepReplaced.TotalSeconds} s");

    public static void Set(IntPtr wParam, IntPtr icon)
    {
        lock (Given)
        {
            Given[icon] = Environment.TickCount64;
        }

        Log.Write($"icon probe: set {(wParam == 1 ? "big" : "small")} {icon:X}");
    }

    public static void Asked(IntPtr wParam, IntPtr answer, bool otherThread)
    {
        if (answer == _lastAsked)
        {
            return;
        }

        _lastAsked = answer;
        Log.Write($"icon probe: asked {Kind(wParam)} by {(otherThread ? "another thread" : "us")}, gave {answer:X} {Age(answer)}");
    }

    public static void Destroyed(IntPtr icon)
    {
        Log.Write($"icon probe: destroyed {icon:X} {Age(icon)}");
        lock (Given)
        {
            Given.Remove(icon);
        }
    }

    private static string Kind(IntPtr wParam) => (long)wParam switch
    {
        1 => "big",
        0 => "small",
        _ => "small2",
    };

    private static string Age(IntPtr icon)
    {
        lock (Given)
        {
            return Given.TryGetValue(icon, out var given) ? $"given {Environment.TickCount64 - given} ms ago" : "not ours";
        }
    }
}
