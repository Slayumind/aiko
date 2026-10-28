using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// --try-terminal-hooks: a real Claude Code (on Haiku) in a pseudo console with the hook of the
/// terminal, the way the window starts it (D-366). It reads a file, asks to write one, is
/// allowed, finishes and exits. The input goes into the pseudo console only; no key and no click
/// reaches the desktop. The titles and the events go into the report file with their times, never
/// into the log of Aiko. It spends a little of the limits of environment 1. The mode is the default
/// one, which asks: in auto mode Claude Code allows a harmless write itself and nothing waits.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalHookCheck
{
    private static readonly Regex Sequence = new(@"\x1b\[[0-?]*[ -/]*[@-~]|\x1b\][^\x07\x1b]*(\x07|\x1b\\)|\x1b.", RegexOptions.Compiled);

    public static int Run(string folder, string reportPath)
    {
        var report = new List<string>();
        var clock = Stopwatch.StartNew();
        void Note(string line)
        {
            lock (report)
            {
                report.Add($"{clock.ElapsedMilliseconds,6} {line}");
            }
        }

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "note.txt"), "The word is lantern.\n");

        if (ClaudeLauncher.FindClaude() is not { } claude)
        {
            File.WriteAllText(reportPath, "claude not found\n");
            return 1;
        }

        // A settings file of its own, next to the folder: the shared one belongs to the installed
        // Aiko, and its sessions must keep calling the installed bridge.
        if (BridgePath.Current() is not { } bridge)
        {
            File.WriteAllText(reportPath, "bridge not found\n");
            return 1;
        }

        var hooks = Path.Combine(folder, "terminal-hooks.json");
        File.WriteAllText(hooks, TerminalHooks.SettingsJson(bridge));

        var id = Guid.NewGuid().ToString();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var inherited = Environment.GetEnvironmentVariables().Keys.Cast<string>().ToList();
        var plan = ClaudeLaunchPlan.For(
            new LaunchRequest(ClaudeConfigFolder.Resolve(null, home), home, "check", id, false, hooks), inherited);
        var arguments = plan.Arguments.Concat(["--model", "haiku", "--permission-mode", "default"]).Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument);
        Note("hooks file: " + (plan.Arguments.Contains("--settings") ? "yes" : "no"));

        var screen = new StringBuilder();
        var titles = new TitleReader();
        var lastTitle = "";
        var drawn = false;
        using var console = new PseudoConsole(new PtyLaunch(
            string.Join(' ', [$"\"{claude}\"", .. arguments]), folder,
            plan.Variables.ToDictionary(change => change.Name, change => change.Value, StringComparer.OrdinalIgnoreCase)));
        console.TerminalOutput += (_, e) =>
        {
            lock (screen)
            {
                screen.Append(e.Data);
            }

            if (!drawn && TerminalScreen.HasText(e.Data))
            {
                drawn = true;
                Note("first letter drawn");
            }

            if (titles.Read(e.Data) is { } title && ClaudeTitle.Parse(title) is { } parsed)
            {
                var shown = (parsed.Working ? "working" : "idle") + " · " + parsed.Name;
                if (shown != lastTitle)
                {
                    lastTitle = shown;
                    Note("title " + shown);
                }
            }
        };
        console.Typed += () => Note("typed");
        var exited = new ManualResetEventSlim();
        console.Exited += code =>
        {
            Note($"exited {code}");
            exited.Set();
        };
        console.Start();
        console.Resize(30, 120);

        var eventFile = Path.Combine(ThisComputer.Folders.TerminalEventsFolder, id + ".json");
        TerminalEvent? last = null;
        void Watch()
        {
            if (File.Exists(eventFile) && TerminalEvent.FromJson(SafeRead(eventFile)) is { } read && read != last)
            {
                last = read;
                Note($"event {read.Kind} {read.Detail}");
            }
        }

        bool WaitFor(Func<bool> done, int seconds)
        {
            var until = clock.ElapsedMilliseconds + seconds * 1000L;
            while (clock.ElapsedMilliseconds < until && !exited.IsSet)
            {
                Watch();
                if (done())
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }

        string Plain()
        {
            lock (screen)
            {
                return Sequence.Replace(screen.ToString(), " ");
            }
        }

        // A folder seen for the first time asks whether to trust it: the first answer is yes.
        WaitFor(() => lastTitle.StartsWith("idle", StringComparison.Ordinal) || Plain().Contains("trust", StringComparison.OrdinalIgnoreCase), 30);
        if (!lastTitle.StartsWith("idle", StringComparison.Ordinal))
        {
            Note("answering the trust question");
            console.WriteInput("\r");
            WaitFor(() => lastTitle.StartsWith("idle", StringComparison.Ordinal), 30);
        }

        Thread.Sleep(1500);
        console.WriteInput("\u001b[200~Read note.txt, then create a file named out.txt with that word in it.\u001b[201~");
        Thread.Sleep(300);
        console.WriteInput("\r");

        if (WaitFor(() => last?.Kind is TerminalEventKind.AsksPermission or TerminalEventKind.Finished, 90)
            && last?.Kind == TerminalEventKind.AsksPermission)
        {
            Thread.Sleep(1500);
            Note("allowing");
            console.WriteInput("\r");
        }

        WaitFor(() => last?.Kind == TerminalEventKind.Finished && lastTitle.StartsWith("idle", StringComparison.Ordinal), 90);
        Thread.Sleep(1000);
        Watch();
        console.WriteInput("/exit");
        Thread.Sleep(300);
        console.WriteInput("\r");
        exited.Wait(TimeSpan.FromSeconds(15));

        lock (report)
        {
            File.WriteAllLines(reportPath, report);
        }

        try
        {
            File.Delete(eventFile);
        }
        catch (IOException)
        {
        }

        return report.Any(line => line.Contains("event AsksPermission")) && report.Any(line => line.Contains("event Finished")) ? 0 : 1;
    }

    private static string? SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
