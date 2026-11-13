using System.IO;
using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// Waits for something that happens outside Aiko: Claude Code getting installed, or somebody
/// finishing the sign-in in the browser. It checks every two seconds and only while the wizard is
/// waiting; the moment the thing is there, or the wizard closes, the timer is gone.
sealed class Waiter : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<bool> _isDone;
    private readonly Action _done;

    private Waiter(Func<bool> isDone, Action done)
    {
        _isDone = isDone;
        _done = done;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Check();
    }

    public static Waiter ForClaude(Action installed) =>
        Start(() => ClaudeLauncher.FindClaude() is not null, installed);

    /// Only that the credentials file exists is checked; it is never opened.
    public static Waiter ForSignIn(string configFolder, Action signedIn) =>
        Start(() => File.Exists(ClaudeInstall.CredentialsPathIn(ThisComputer.Platform, configFolder)), signedIn);

    private static Waiter Start(Func<bool> isDone, Action done)
    {
        var waiter = new Waiter(isDone, done);
        waiter._timer.Start();
        waiter.Check();
        return waiter;
    }

    private void Check()
    {
        bool done;
        try
        {
            done = _isDone();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (done)
        {
            Dispose();
            _done();
        }
    }

    public void Dispose() => _timer.Stop();
}
