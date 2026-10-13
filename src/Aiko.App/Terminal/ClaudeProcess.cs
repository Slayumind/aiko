using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Claude Code as a child process with pipes on both ends (D-297).
///
/// Both pipes speak UTF-8 without a mark at the start: the default of Windows is the code page
/// of the console, which breaks every letter outside it. The output is read on a thread of its
/// own, so a long answer never waits for the window and the window never waits for an answer.
[SupportedOSPlatform("windows10.0.17763")]
sealed class ClaudeProcess : IConversationProcess
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// How long Claude Code gets to leave on its own after its input was closed.
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(3);

    private readonly object _gate = new();
    private Process? _process;
    private ProcessJob? _job;
    private bool _gone;

    public bool Start(ProcessStart start, Action<string> onLine, Action<int> onExit)
    {
        var info = new ProcessStartInfo(start.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            WorkingDirectory = start.WorkingDirectory,
        };

        foreach (var argument in start.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var variable in start.Variables)
        {
            if (variable.Value is { } value)
            {
                info.Environment[variable.Name] = value;
            }
            else
            {
                info.Environment.Remove(variable.Name);
            }
        }

        Process? process;
        try
        {
            process = Process.Start(info);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }

        if (process is null)
        {
            return false;
        }

        _process = process;
        _job = ProcessJob.Around(process);

        // What Claude Code writes to its error stream is for a person at a console. Nobody reads
        // it here, but a pipe nobody drains fills up and stops the process.
        _ = process.StandardError.BaseStream.CopyToAsync(Stream.Null);

        new Thread(() => Read(process, onLine, onExit)) { IsBackground = true, Name = "Claude Code output" }.Start();
        return true;
    }

    public bool Write(string line)
    {
        lock (_gate)
        {
            if (_gone || _process is not { } process)
            {
                return false;
            }

            try
            {
                process.StandardInput.Write(line);
                process.StandardInput.Write('\n');
                process.StandardInput.Flush();
                return true;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
                return false;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_gone || _process is not { } process)
            {
                return;
            }

            try
            {
                process.StandardInput.Close();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        // One wait, once, for a process that was asked to leave. Nothing ticks after it.
        Task.Delay(Grace).ContinueWith(_ => End(), TaskScheduler.Default);
    }

    private void End()
    {
        lock (_gate)
        {
            if (_gone || _process is not { } process)
            {
                return;
            }

            if (_job is { } job)
            {
                job.End();
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
            {
            }
        }
    }

    private void Read(Process process, Action<string> onLine, Action<int> onExit)
    {
        try
        {
            while (process.StandardOutput.ReadLine() is { } line)
            {
                onLine(line);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
        }

        var code = -1;
        try
        {
            process.WaitForExit();
            code = process.ExitCode;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
        }

        lock (_gate)
        {
            _gone = true;
            _job?.Dispose();
            _job = null;
            process.Dispose();
        }

        onExit(code);
    }
}
