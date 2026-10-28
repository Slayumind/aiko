using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Aiko.Core.Terminal;
using Microsoft.Terminal.Wpf;
using Microsoft.Win32.SafeHandles;

namespace Aiko.App;

/// What to start in a real terminal: the command line, its folder, and the environment
/// variables to set (a null value removes one that Aiko itself has).
sealed record PtyLaunch(string CommandLine, string Folder, IReadOnlyDictionary<string, string?> Variables);

/// One program in a pseudo console of Windows, the connection the terminal control draws
/// (D-360). The ConPTY of the package ships next to Aiko: it is newer than the one in Windows.
///
/// Everything the program starts belongs to one Windows job, so closing the terminal ends the
/// whole tree: the shells of Claude Code's tools, a dev server, a build.
[SupportedOSPlatform("windows10.0.17763")]
sealed class PseudoConsole : ITerminalConnection, IDisposable
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const int PseudoConsoleAttribute = 0x00020016;

    private readonly PtyLaunch _launch;
    private IntPtr _console;
    private FileStream? _input;
    private FileStream? _output;
    private SafeProcessHandle? _process;
    private ProcessJob? _job;
    private short _columns = 120;
    private short _rows = 30;

    public PseudoConsole(PtyLaunch launch) => _launch = launch;

    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// The person typed into the program: not only sequences of the terminal itself.
    public event Action? Typed;

    /// The program ended, with its exit code. Raised on a background thread.
    public event Action<int>? Exited;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0)
            || !CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
        {
            throw new Win32Exception();
        }

        var result = ConptyCreatePseudoConsole(new Coord(_columns, _rows), inputRead, outputWrite, 0, out _console);
        inputRead.Dispose();
        outputWrite.Dispose();
        if (result != 0)
        {
            throw new Win32Exception(result);
        }

        var info = new StartupInfoEx();
        info.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        info.AttributeList = Marshal.AllocHGlobal(size);
        try
        {
            if (!InitializeProcThreadAttributeList(info.AttributeList, 1, 0, ref size)
                || !UpdateProcThreadAttribute(info.AttributeList, 0, (IntPtr)PseudoConsoleAttribute, _console, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero)
                || !CreateProcessW(null, new StringBuilder(_launch.CommandLine), IntPtr.Zero, IntPtr.Zero, false,
                    ExtendedStartupInfoPresent | CreateUnicodeEnvironment, EnvironmentBlock(_launch.Variables), _launch.Folder,
                    ref info, out var started))
            {
                throw new Win32Exception();
            }

            CloseHandle(started.Thread);
            _process = new SafeProcessHandle(started.Process, ownsHandle: true);
        }
        finally
        {
            DeleteProcThreadAttributeList(info.AttributeList);
            Marshal.FreeHGlobal(info.AttributeList);
        }

        _job = ProcessJob.Around(_process);
        _input = new FileStream(inputWrite, FileAccess.Write);
        _output = new FileStream(outputRead, FileAccess.Read);
        IsRunning = true;
        new Thread(Read) { IsBackground = true, Name = "pseudo console output" }.Start();
        new Thread(Wait) { IsBackground = true, Name = "pseudo console exit" }.Start();
    }

    public void WriteInput(string data)
    {
        if (_input is null || !IsRunning)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(data);
        try
        {
            _input.Write(bytes, 0, bytes.Length);
            _input.Flush();
            if (TerminalPulse.IsTyped(data))
            {
                Typed?.Invoke();
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The program has just ended or the terminal was closed; Exited comes next.
        }
    }

    public void Resize(uint rows, uint columns)
    {
        _rows = (short)Math.Clamp(rows, 1, short.MaxValue);
        _columns = (short)Math.Clamp(columns, 1, short.MaxValue);
        if (_console != IntPtr.Zero)
        {
            ConptyResizePseudoConsole(_console, new Coord(_columns, _rows));
        }
    }

    /// Ends the program and everything it started.
    public void Close()
    {
        if (_console != IntPtr.Zero)
        {
            ConptyClosePseudoConsole(_console);
            _console = IntPtr.Zero;
        }

        _job?.End();
    }

    /// The output and the handle of the process are not closed here: their threads are still
    /// waiting on them, and each thread closes its own when the program has ended.
    public void Dispose()
    {
        Close();
        _job?.Dispose();
        _input?.Dispose();
    }

    private void Read()
    {
        var bytes = new byte[16384];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        // A letter can be cut between two reads: the decoder keeps its first half for the next one.
        var decoder = Encoding.UTF8.GetDecoder();
        try
        {
            int count;
            while ((count = _output!.Read(bytes, 0, bytes.Length)) > 0)
            {
                var length = decoder.GetChars(bytes, 0, count, chars, 0);
                TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(new string(chars, 0, length)));
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The pseudo console was closed.
        }
        finally
        {
            _output?.Dispose();
        }
    }

    private void Wait()
    {
        using var ended = new ManualResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(_process!.DangerousGetHandle(), ownsHandle: false) };
        ended.WaitOne();
        GetExitCodeProcess(_process.DangerousGetHandle(), out var code);
        IsRunning = false;
        _process.Dispose();
        Exited?.Invoke((int)code);
    }

    /// Aiko's own environment with the launch's changes, sorted as Windows expects.
    private static string EnvironmentBlock(IReadOnlyDictionary<string, string?> changes)
    {
        var variables = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? "", StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in changes)
        {
            if (value is null)
            {
                variables.Remove(name);
            }
            else
            {
                variables[name] = value;
            }
        }

        return string.Concat(variables.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{entry.Key}={entry.Value}\0")) + "\0";
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Coord(short X, short Y);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("conpty.dll")]
    private static extern int ConptyCreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out IntPtr console);

    [DllImport("conpty.dll")]
    private static extern int ConptyResizePseudoConsole(IntPtr console, Coord size);

    [DllImport("conpty.dll")]
    private static extern void ConptyClosePseudoConsole(IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, IntPtr attributes, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(string? application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        bool inheritHandles, uint flags, string environment, string folder, ref StartupInfoEx info, out ProcessInformation started);

    [DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
