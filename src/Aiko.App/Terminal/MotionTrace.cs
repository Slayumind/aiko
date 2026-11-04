using System.Diagnostics;

namespace Aiko.App;

/// A temporary measure of the end of the header motion: when the header stands, when the size goes
/// to the pseudo console, when output comes after it, and when the real terminal and its picture
/// swap. Each line says milliseconds from the start of the motion. No text of the terminal is
/// written, only the lengths of its output.
static class MotionTrace
{
    private static readonly Stopwatch Clock = new();
    private static long _resizedAt = -1;

    public static void Start(string what)
    {
        Clock.Restart();
        _resizedAt = -1;
        Write($"start {what}");
    }

    public static void Mark(string what)
    {
        if (Clock.IsRunning && Clock.ElapsedMilliseconds < 3000)
        {
            Write(what);
        }
    }

    public static void Resized(int rows, int columns)
    {
        if (Clock.IsRunning && Clock.ElapsedMilliseconds < 3000)
        {
            _resizedAt = Clock.ElapsedMilliseconds;
            Write($"resize sent {rows}x{columns}");
        }
    }

    /// Output of the terminal, from its own thread, in the 600 ms after a resize: its length only.
    public static void Output(int chars)
    {
        var resized = Interlocked.Read(ref _resizedAt);
        if (resized >= 0 && Clock.ElapsedMilliseconds - resized < 600)
        {
            Write($"output {chars} chars");
        }
    }

    private static void Write(string what) => Log.Write($"header motion +{Clock.ElapsedMilliseconds} ms: {what}");
}
