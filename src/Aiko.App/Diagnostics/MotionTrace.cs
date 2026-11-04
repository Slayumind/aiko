using System.Diagnostics;

namespace Aiko.App;

/// The steps of the header motion that MotionTrace measures.
enum MotionStep
{
    Open,
    Fold,
    HeaderStands,
    ResizeSent,
    Output,
    LiveShown,
    Still,
    Sizes,
    PictureRemoved,
}

/// A temporary measure of the end of the header motion: when the header stands, when the size goes
/// to the pseudo console, when output comes after it, and when the real terminal and its picture
/// swap. Each line says milliseconds from the start of the motion. It takes steps and numbers only,
/// never text, so nothing of a session can reach the log (D-298).
static class MotionTrace
{
    private static readonly Stopwatch Clock = new();
    private static long ResizedAt = -1;

    public static void Start(MotionStep step)
    {
        Clock.Restart();
        Interlocked.Exchange(ref ResizedAt, -1);
        Write(step, 0, 0);
    }

    public static void Mark(MotionStep step, int first = 0, int second = 0)
    {
        if (!Clock.IsRunning || Clock.ElapsedMilliseconds > 3000)
        {
            return;
        }

        if (step == MotionStep.ResizeSent)
        {
            Interlocked.Exchange(ref ResizedAt, Clock.ElapsedMilliseconds);
        }

        Write(step, first, second);
    }

    /// Output of the terminal, from its own thread, in the 600 ms after a resize: its length only.
    public static void Output(int length)
    {
        var resized = Interlocked.Read(ref ResizedAt);
        if (resized >= 0 && Clock.ElapsedMilliseconds - resized < 600)
        {
            Write(MotionStep.Output, length, 0);
        }
    }

    private static void Write(MotionStep step, int first, int second) =>
        Log.Write($"header motion +{Clock.ElapsedMilliseconds} ms: {step} {first} {second}");
}
