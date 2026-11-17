using System.Windows;
using System.Windows.Threading;

namespace Aiko.App;

/// A mistake in one window must not close Aiko: the tray, the island and the sessions of the
/// terminal stay. The error goes to the log with its stack, and the program goes on.
///
/// An error that comes again and again, such as one in every layout pass, would fill the log and
/// freeze the window. After a few in a short time the windows are closed instead; the sessions live
/// on without them (D-351).
static class CrashGuard
{
    private const int RepeatLimit = 3;
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(2);

    private static readonly Queue<DateTime> Recent = new();

    /// closeWindows closes the windows of Aiko when an error repeats.
    public static void Watch(Application application, Action closeWindows)
    {
        application.DispatcherUnhandledException += (_, e) =>
        {
            Log.Write("error, kept running: " + Describe(e.Exception));
            e.Handled = true;
            if (Repeats(DateTime.UtcNow))
            {
                Log.Write("error repeats: the windows are closed");
                Recent.Clear();
                closeWindows();
            }
        };

        // A task nobody waited for: its error is only written down.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Write("error in a task: " + Describe(e.Exception));
            e.SetObserved();
        };

        // An error on another thread cannot be stopped; at least the log says what it was.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write("fatal error: " + (e.ExceptionObject is Exception error ? Describe(error) : e.ExceptionObject?.ToString()));
    }

    /// Whether this error is one of several in a short time.
    private static bool Repeats(DateTime now)
    {
        Recent.Enqueue(now);
        while (Recent.Count > 0 && now - Recent.Peek() > RepeatWindow)
        {
            Recent.Dequeue();
        }

        return Recent.Count >= RepeatLimit;
    }

    /// The type, the message and the stack. Stacks and messages of Aiko carry no tokens: a token is
    /// only ever in a request header (CLAUDE.md, "Лимиты и безопасность токенов").
    private static string Describe(Exception error) =>
        $"{error.GetType().FullName}: {error.Message}{Environment.NewLine}{error.StackTrace}";
}
