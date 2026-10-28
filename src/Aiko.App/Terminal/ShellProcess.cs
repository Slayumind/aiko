using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Runs a command of "!" in the folder of the session (D-343): in Git Bash when it is there, as in
/// Claude Code on Windows, else in PowerShell. The command gets no input, its errors come with its
/// output, and after two minutes it is stopped, the same limit Claude Code gives its own commands.
[SupportedOSPlatform("windows10.0.17763")]
sealed class ShellProcess : IShell
{
    private static readonly TimeSpan MostTime = TimeSpan.FromMinutes(2);

    /// More output than this is not kept: Claude gets far less anyway.
    private const int MostKept = 200_000;

    /// One conversation runs one command at a time (Conversation holds the next one).
    private Process? _running;
    private volatile bool _stopped;

    public void Run(string command, string folder, Action<string, int> ended) =>
        _ = Task.Run(() =>
        {
            _stopped = false;
            var (output, code) = RunNow(command, folder);
            ended(output, code);
        });

    /// Esc or a closed session: the command and what it started end now.
    public void Stop()
    {
        _stopped = true;
        try
        {
            _running?.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It has just ended by itself.
        }
    }

    private (string Output, int ExitCode) RunNow(string command, string folder)
    {
        var start = StartFor(command);
        start.WorkingDirectory = folder;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.StandardOutputEncoding = Encoding.UTF8;
        start.StandardErrorEncoding = Encoding.UTF8;

        var output = new StringBuilder();
        void Keep(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (output)
            {
                if (output.Length < MostKept)
                {
                    output.Append(line).Append('\n');
                }
            }
        }

        try
        {
            using var process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, e) => Keep(e.Data);
            process.ErrorDataReceived += (_, e) => Keep(e.Data);
            process.Start();
            _running = process;
            using var job = ProcessJob.Around(process);
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(MostTime))
            {
                if (job is not null)
                {
                    job.End();
                }
                else
                {
                    process.Kill(entireProcessTree: true);
                }

                Keep($"(stopped after {MostTime.TotalMinutes:0} minutes)");
                return (Text(output), -1);
            }

            // The last lines come after the exit; this waits for them.
            process.WaitForExit();
            _running = null;
            if (_stopped)
            {
                Keep("(stopped)");
            }

            return (Text(output), process.ExitCode);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (error.Message, -1);
        }
    }

    private static string Text(StringBuilder output)
    {
        lock (output)
        {
            return output.ToString();
        }
    }

    private static ProcessStartInfo StartFor(string command)
    {
        if (GitBash() is { } bash)
        {
            var start = new ProcessStartInfo(bash);
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(command);
            return start;
        }

        var shell = OnPath("pwsh.exe") ?? "powershell.exe";
        var powerShell = new ProcessStartInfo(shell);
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding = [Text.Encoding]::UTF8; " + command })
        {
            powerShell.ArgumentList.Add(argument);
        }

        return powerShell;
    }

    /// The bash of Git for Windows. The bash of WSL in System32 is another system with other paths,
    /// so it is not taken.
    private static string? GitBash()
    {
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        ];
        foreach (var root in roots.Where(root => root.Length > 0))
        {
            var bash = Path.Combine(root, "Git", "bin", "bash.exe");
            if (File.Exists(bash))
            {
                return bash;
            }
        }

        // Git elsewhere: git.exe sits in Git\cmd, bash.exe in Git\bin.
        if (OnPath("git.exe") is { } git && Path.GetDirectoryName(Path.GetDirectoryName(git)) is { } home)
        {
            var bash = Path.Combine(home, "bin", "bash.exe");
            if (File.Exists(bash))
            {
                return bash;
            }
        }

        return null;
    }

    private static string? OnPath(string file) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim('"'), file))
            .FirstOrDefault(File.Exists);
}
