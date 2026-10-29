using System.Windows;
using System.Windows.Threading;

namespace Aiko.App;

/// --try-crash-guard: an empty app with the guard, no tray and no shell. Three errors in a row on
/// the dispatcher must neither end the process nor be missed: the windows are closed once. The
/// result is a line in the log and the exit code.
static class CrashGuardCheck
{
    public static int Run()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var closed = 0;
        CrashGuard.Watch(application, () => closed++);

        application.Startup += (_, _) =>
        {
            for (var i = 0; i < 3; i++)
            {
                application.Dispatcher.BeginInvoke(() => throw new InvalidOperationException("a test error of --try-crash-guard"));
            }

            application.Dispatcher.BeginInvoke(application.Shutdown, DispatcherPriority.ApplicationIdle);
        };
        application.Run();

        var ok = closed == 1;
        Log.Write($"--try-crash-guard: alive after 3 errors, windows closed {closed} time(s): {(ok ? "ok" : "FAILED")}");
        return ok ? 0 : 1;
    }
}
