using System.IO;
using System.Windows;
using Aiko.App;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.Checks;

/// --try-stop report.txt: no way out of Aiko ends a running session without a word (D-351). A
/// real window runs a command line in a temporary folder and keeps nothing on disk. The steps:
/// an error that repeats only hides the window; a question asked from the settings is seen, and
/// "no" brings the same page back; "yes" runs the step, and the session stays in the list.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class StopCheck
{
    public static int Run(string reportPath)
    {
        var report = new List<string>();
        var failed = false;
        void Check(string what, bool ok, string detail = "")
        {
            failed |= !ok;
            report.Add($"{(ok ? "PASS" : "FAIL")} {what}{(detail.Length > 0 ? $" ({detail})" : "")}");
        }

        var work = Path.Combine(Path.GetTempPath(), "aiko-stop-check");
        Directory.CreateDirectory(work);

        const string environment = "Check";
        const string id = "stop-check-shell";
        var settings = new EnvironmentSettings([new AikoEnvironment(environment, [Path.Combine(work, ".claude")])]);
        var sessions = TerminalSessions.Empty
            .Add(id, environment, work, "shell", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), SessionKind.Shell)
            .Opened(id);

        var application = Tips.Install(new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown });
        application.Startup += async (_, _) =>
        {
            var host = new TerminalHost(settings, sessions, _ => null, () => new CardModel("", []), () => (null, null), keepsNothing: true);
            host.Show();
            Check("the command line runs", await Until(() => host.AnyRunning, 15000));

            // ---- an error that repeats: the window steps aside, the session lives on ----
            host.StepAside();
            await Task.Delay(500);
            Check("an error hides the window", !host.ShownForCheck);
            Check("an error leaves the session running", host.AnyRunning);
            Check("an error leaves the session in the list", host.SessionsForCheck.Find(id) is not null);

            // ---- asked from the settings: the question is seen, and "no" gives the page back ----
            host.Activate();
            await Task.Delay(1000);
            Check("the window comes back with the session running", host.ShownForCheck && host.AnyRunning);
            host.OpenSettings(SettingsPanel.GeneralPageKey);
            await Task.Delay(1000);
            Check("the settings open with the session running", host.AnyRunning);
            var ran = 0;
            host.AskToStop(StopReason.Remove, () => ran++);
            Check("the question is asked", host.AskingForCheck == StopReason.Remove);
            Check("the settings step aside for the question", host.SettingsPageForCheck is null);
            Check("nothing runs before the answer", ran == 0);

            host.AnswerForCheck(false);
            Check("no keeps the session", host.AnyRunning && ran == 0);
            Check("no opens the same page of the settings", host.SettingsPageForCheck == SettingsPanel.GeneralPageKey,
                host.SettingsPageForCheck ?? "none");

            // ---- "yes": the step runs, the program ends, the session stays for the next start ----
            host.CloseSettings();
            host.AskToStop(StopReason.StartOver, () => ran++);
            host.AnswerForCheck(true);
            await Task.Delay(1500);
            Check("yes runs the step once", ran == 1, $"{ran}");
            Check("yes ends the program", !host.AnyRunning);
            Check("the ended command line stays in the list", host.SessionsForCheck.Find(id) is not null);

            host.Close();

            // ---- an environment goes: its running session is asked about, stops and leaves ----
            var second = new TerminalHost(settings, sessions, _ => null, () => new CardModel("", []), () => (null, null), keepsNothing: true);
            second.Show();
            Check("a new window runs the command line", await Until(() => second.AnyRunning, 15000));
            var removed = 0;
            second.AskToRemoveEnvironment(environment, () => removed++);
            Check("removing the environment asks", second.AskingForCheck == StopReason.RemoveEnvironment && removed == 0);
            second.AnswerForCheck(true);
            await Task.Delay(1500);
            Check("yes removes the environment once", removed == 1, $"{removed}");
            Check("its session is closed", second.SessionsForCheck.Find(id) is { Closed: true });
            Check("its program ends", !second.AnyRunning);
            Check("the settings come back", second.SettingsPageForCheck is not null);
            second.Close();

            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();

        Log.Write($"--try-stop: {(failed ? "FAILED" : "ok")}");
        return failed ? 1 : 0;
    }

    private static async Task<bool> Until(Func<bool> met, int milliseconds = 2000)
    {
        for (var waited = 0; waited < milliseconds; waited += 50)
        {
            if (met())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return met();
    }
}
