using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// One copy of Aiko for a person (D-419). The first copy holds a named mutex for its whole life and
/// waits on a named event. A second copy finds the mutex taken, gives the first copy the right to
/// bring its window to the front, sets the event and exits. Windows lets a program take the front
/// only after a click on it, and the click was on the second copy, so without that right the window
/// of the first would only blink on the taskbar.
///
/// The names carry the user's SID: in one Windows session another account gets its own Aiko. No
/// window of any kind is made for this, hidden or not.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class SingleCopy
{
    private static Mutex? Held;
    private static EventWaitHandle? OpenAsked;

    private static string Name => $@"Local\Slayumind.Aiko.{WindowsIdentity.GetCurrent().User?.Value ?? "user"}";

    /// Says whether this copy goes on. False: another copy runs, it got the request, and this one
    /// should exit at once.
    public static bool Claim(AppStart start)
    {
        if (!AppStarts.KeepsOneCopy(start))
        {
            return true;
        }

        Held = new Mutex(initiallyOwned: true, Name, out var first);
        if (first)
        {
            OpenAsked = new EventWaitHandle(false, EventResetMode.AutoReset, Name + ".Open");
            return true;
        }

        Held.Dispose();
        Held = null;
        Log.Write($"another copy of Aiko runs: this {start} start exits");
        if (AppStarts.OpensRunningWindow(start))
        {
            AskToOpen();
        }

        return false;
    }

    /// In the first copy: calls opened on the dispatcher each time a second copy asks.
    public static void Listen(Dispatcher dispatcher, Action opened)
    {
        if (OpenAsked is null)
        {
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(
            OpenAsked,
            (_, _) => dispatcher.BeginInvoke(opened),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void AskToOpen()
    {
        // The first copy may still be starting and has no event yet: then the request is lost, and
        // the person clicks once more.
        if (!EventWaitHandle.TryOpenExisting(Name + ".Open", out var open))
        {
            return;
        }

        using (open)
        {
            var self = Environment.ProcessId;
            foreach (var other in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
            {
                using (other)
                {
                    if (other.Id != self)
                    {
                        AllowSetForegroundWindow((uint)other.Id);
                    }
                }
            }

            open.Set();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
