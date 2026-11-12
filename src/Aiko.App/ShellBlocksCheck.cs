using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// --try-shell-blocks out-folder: every shell of this machine starts with Aiko's marks in a real
/// ConPTY and runs the same commands: a success, a failure, a line redrawn with \r, ten thousand lines
/// of output and a full-screen program in Git Bash. What ConPTY sent is saved as it came, one file
/// per shell, and the blocks the core finds in it go to the log (D-412). Nothing outside a temporary
/// folder is touched.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class ShellBlocksCheck
{
    private const int Columns = 120;
    private const int Rows = 30;

    /// tests: a built test project for dotnet test, whose live progress jumps the cursor; null skips it.
    public static int Run(string outFolder, string? tests = null)
    {
        Directory.CreateDirectory(outFolder);
        var work = Path.Combine(Path.GetTempPath(), "aiko-shell-blocks");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "note.txt"), "lantern\n");
        Process.Start(new ProcessStartInfo("git", "init -q") { WorkingDirectory = work, CreateNoWindow = true })?.WaitForExit(10_000);

        var ok = true;
        if (ShellLaunch.GitBash() is { } bash)
        {
            List<(string, bool)> commands =
            [
                ("git status", false),
                ("ls nothing-here", false),
                ("for i in 1 2 3 4 5; do printf '\\rprogress %d/5' $i; sleep 0.1; done; echo", false),
                ("seq 1 10000", false),
                ("less note.txt", true),
                ("npm init -y > /dev/null && npm install left-pad --no-audit --no-fund", false),
            ];
            if (tests is not null)
            {
                commands.Add(($"dotnet test \"{tests.Replace('\\', '/')}\" --no-build", false));
            }

            ok &= Record(outFolder, work, ShellLaunch.Bash(bash), [.. commands]);
        }

        foreach (var (program, name) in new[] { (ShellLaunch.FindOnPath("pwsh.exe"), "pwsh"), ((string?)"powershell.exe", "powershell") })
        {
            if (program is null)
            {
                continue;
            }

            ok &= Record(outFolder, work, ShellLaunch.PowerShell(program, name),
            [
                ("git status", false),
                ("Get-Item nothing-here", false),
                ("1..5 | ForEach-Object { Write-Host -NoNewline \"`rprogress $_/5\"; Start-Sleep -Milliseconds 100 }; Write-Host ''", false),
                ("1..10000", false),
            ]);
        }

        return ok ? 0 : 1;
    }

    private static bool Record(string outFolder, string work, ShellLaunch launch, (string Command, bool FullScreen)[] commands)
    {
        var raw = new StringBuilder();
        var sawFullScreen = false;
        using var console = new PseudoConsole(new PtyLaunch(launch.CommandLine, work, new Dictionary<string, string?>()));
        console.TerminalOutput += (_, e) =>
        {
            lock (raw)
            {
                raw.Append(e.Data);
            }

            // The terminal control answers this in the window; here nobody else does.
            if (e.Data.Contains("\u001b[c", StringComparison.Ordinal))
            {
                console.WriteInput("\u001b[?61;4c");
            }

            if (e.Data.Contains("\u001b[?1049h", StringComparison.Ordinal))
            {
                sawFullScreen = true;
            }
        };
        console.Start();
        console.Resize(Rows, Columns);

        var ready = WaitFor(raw, 1, 20_000);
        var ends = 1;
        foreach (var (command, fullScreen) in commands)
        {
            console.WriteInput(command + "\r");
            if (fullScreen)
            {
                WaitUntil(() => sawFullScreen, 8_000);
                Thread.Sleep(500);
                console.WriteInput("q");
            }

            ends++;
            ready &= WaitFor(raw, ends, 60_000);
        }

        console.WriteInput("exit\r");
        Thread.Sleep(1000);

        string stream;
        lock (raw)
        {
            stream = raw.ToString();
        }

        File.WriteAllText(Path.Combine(outFolder, $"{launch.Name}.json"), JsonSerializer.Serialize(new { columns = Columns, rows = Rows, stream }));

        var screen = new ShellScreen(Columns, Rows);
        var watch = Stopwatch.StartNew();
        screen.Feed(stream, 0);
        var blocks = ShellBlocks.Of(screen);
        Log.Write($"--try-shell-blocks {launch.Name}: prompts {ready}, {stream.Length} chars read in {watch.ElapsedMilliseconds} ms, full screen seen {sawFullScreen}, now {screen.FullScreen}");
        foreach (var block in blocks)
        {
            Log.Write($"  [{block.ExitCode}] {block.Command} · {block.LineCount} lines · {(block.LineCount > 0 ? screen.LineText(block.FirstLine) : "")}");
        }

        return ready;
    }

    /// Waits until the stream has this many ends of a command (D marks): the prompt is back.
    private static bool WaitFor(StringBuilder raw, int ends, int milliseconds) =>
        WaitUntil(() =>
        {
            lock (raw)
            {
                return Count(raw.ToString(), "\u001b]133;D") >= ends;
            }
        }, milliseconds);

    private static bool WaitUntil(Func<bool> done, int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            if (done())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
