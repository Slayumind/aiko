using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;

namespace Aiko.App;

/// TEMPORARY, for the check of 2026-10-23: why the ink of a streaming answer looks uneven. It writes
/// numbers to the log of Aiko, never words: when a lump of the stream came and how many words it
/// had, how many frames were drawn each second and the longest gap between two, and how far the
/// feed jumped down. Removed once the cause is found.
static class InkProbe
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double _lastLump = -1;
    private static double _lastFrame = -1;
    private static double _secondStart;
    private static int _frames;
    private static double _worstGap;
    private static int _live;

    public static void Lump(int freshWords, int liveChars)
    {
        var now = Clock.Elapsed.TotalMilliseconds;
        var since = _lastLump < 0 ? 0 : now - _lastLump;
        _lastLump = now;
        Log.Write(string.Format(CultureInfo.InvariantCulture, "ink: lump +{0} words after {1:F0} ms, {2} chars live", freshWords, since, liveChars));
    }

    public static void Jump(double pixels) =>
        Log.Write(string.Format(CultureInfo.InvariantCulture, "ink: feed jumped {0:F0} px", pixels));

    /// A streaming answer is on screen: count frames until the last one is gone.
    public static void Start()
    {
        if (_live++ == 0)
        {
            _lastFrame = -1;
            _secondStart = Clock.Elapsed.TotalMilliseconds;
            _frames = 0;
            _worstGap = 0;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    public static void Stop()
    {
        if (_live > 0 && --_live == 0)
        {
            CompositionTarget.Rendering -= OnFrame;
            _lastLump = -1;
        }
    }

    private static void OnFrame(object? sender, EventArgs e)
    {
        var now = Clock.Elapsed.TotalMilliseconds;
        if (_lastFrame >= 0)
        {
            _worstGap = Math.Max(_worstGap, now - _lastFrame);
        }

        _lastFrame = now;
        _frames++;
        if (now - _secondStart >= 1000)
        {
            Log.Write(string.Format(CultureInfo.InvariantCulture, "ink: {0} frames in {1:F0} ms, longest gap {2:F0} ms", _frames, now - _secondStart, _worstGap));
            _secondStart = now;
            _frames = 0;
            _worstGap = 0;
        }
    }
}
