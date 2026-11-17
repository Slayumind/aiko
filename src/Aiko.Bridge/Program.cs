using System.Diagnostics;
using System.Text;
using Aiko.Core;

// Claude Code runs this on every status line update and waits for its output, so two rules hold
// above everything else: be quick, and never fail. Whatever goes wrong, the exit code is 0 and
// the user's own status line still gets printed.
// Claude Code runs "Aiko.Bridge.exe plugin aiko-persona" to get the persona plugin (D-201). It
// sends nothing on stdin, so this is decided before any input is read: waiting for input that
// never comes would hang the install. Unlike the status line, a failure here must show as one.
var job = BridgeWork.JobFor(args);
if (job == BridgeJob.PersonaPlugin)
{
    return BuildPersonaPlugin();
}

// The memory server talks over stdin for as long as the session lives. Reading the input to its
// end, as every other job does, would wait for the session to close before the first answer.
var memoryMode = MemoryBridge.ModeFor(args);
if (memoryMode == MemoryBridgeMode.Serve)
{
    return ServeMemory();
}

try
{
    var input = ReadAllInput();

    // Without the variable Claude Code works in ~\.claude, so the bridge does too. Naming that case
    // anything else made the app look for a file that was never written.
    var configDirectory = ClaudeConfigFolder.Resolve(
        Environment.GetEnvironmentVariable(ClaudeConfigFolder.VariableName),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    switch (job)
    {
        case BridgeJob.Memory when memoryMode is { } memoryHook:
            AnswerMemoryHook(memoryHook, input);
            break;
        case BridgeJob.SessionStart:
            RemindAboutBinding(input);
            break;
        case BridgeJob.TerminalHook:
            // It comes with --settings, so it runs in every environment, with the personality or without it.
            RecordTerminalEvent(input);
            break;
        case BridgeJob.Hook:
            RecordActivity(configDirectory, input);
            break;
        default:
            RunStatusLine(configDirectory, input);
            break;
    }
}
catch (Exception)
{
    // A status line that throws would show a stack trace under the prompt. Silence is better.
}

return 0;

/// The snapshot of the limits, then the status line the user already had.
static void RunStatusLine(string configDirectory, string input)
{
    var settings = Path.Combine(configDirectory, "settings.json");
    var snapshotFile = ThisComputer.Folders.SnapshotFile(SnapshotName.For(configDirectory));
    var plan = BridgeWork.StatusLine(ThisComputer.Folders, configDirectory, ReadIfThere(settings), input, DateTimeOffset.Now, ReadIfThere(snapshotFile));
    if (plan.Snapshot is { } snapshot)
    {
        Directory.CreateDirectory(ThisComputer.Folders.SnapshotsFolder);
        AtomicFile.Replace(snapshot.Path, snapshot.Text);
    }

    var output = plan.WrappedCommand is { } wrapped ? RunWrapped(wrapped, input) : "";
    if (output.Length > 0)
    {
        WriteOutput(output);
    }
}

/// Read the payload as bytes: Console.In would decode it with the OEM code page, and on a
/// Russian Windows that turns Cyrillic paths into rubbish.
///
/// A leading byte order mark is dropped here as well as in the parser. The same text goes on to
/// the status line the user already had, and a mark in front of it would break that one too.
static string ReadAllInput()
{
    using var stdin = Console.OpenStandardInput();
    using var buffer = new MemoryStream();
    stdin.CopyTo(buffer);
    return Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('﻿');
}

/// The session start hook. Prints a system message only when a command overrode a folder binding,
/// and nothing at all otherwise: an empty hook answer changes nothing in Claude Code.
static void RemindAboutBinding(string input)
{
    var output = BridgeWork.SessionStart(
        input,
        Environment.GetEnvironmentVariable(SessionReminder.LaunchVariable),
        Environment.GetEnvironmentVariable(SessionReminder.EnvironmentVariable),
        ReadIfThere(ThisComputer.Folders.EnvironmentsFile),
        ReadIfThere(ThisComputer.Folders.SettingsFile),
        language => SessionReminder.IsRussian(language, WindowsLanguage.UserInterface()));

    if (output is not null)
    {
        WriteOutput(output);
    }
}

/// One small file per session for the face in the tray. An event that means nothing for the face
/// writes nothing, and the same activity is not written again every few seconds.
static void RecordActivity(string configDirectory, string input)
{
    var step = BridgeWork.Activity(ThisComputer.Folders, configDirectory, input, path => File.Exists(path) ? File.ReadAllText(path) : null, DateTimeOffset.Now);
    if (step.Remove is { } gone)
    {
        File.Delete(gone);
    }
    else if (step.Write is { } write)
    {
        Directory.CreateDirectory(ThisComputer.Folders.ActivityFolder);
        AtomicFile.Replace(write.Path, write.Text);
    }
}

/// The last event of a session of the terminal, for its row. The session is named by the variable
/// Aiko gave it, not by the id in the input: a file name never comes from what Claude Code sends.
static void RecordTerminalEvent(string input)
{
    var file = Aiko.Core.Terminal.TerminalEvent.FileName(Environment.GetEnvironmentVariable(Aiko.Core.Terminal.TerminalEvent.SessionVariable));
    if (file is null || Aiko.Core.Terminal.TerminalEvent.FromHook(input, DateTimeOffset.Now.ToUnixTimeMilliseconds()) is not { } terminalEvent)
    {
        return;
    }

    var folder = ThisComputer.Folders.TerminalEventsFolder;
    var path = Path.Combine(folder, file);
    var written = File.Exists(path) ? Aiko.Core.Terminal.TerminalEvent.FromJson(File.ReadAllText(path)) : null;
    if (!terminalEvent.Replaces(written))
    {
        return;
    }

    Directory.CreateDirectory(folder);
    AtomicFile.Replace(path, terminalEvent.ToJson());
}

/// The memory hooks of the persona plugin. The rules and the facts go to the model at the start of
/// a session; a message of the person refreshes facts by their keys and may bring a reminder.
static void AnswerMemoryHook(MemoryBridgeMode mode, string input)
{
    var today = DateOnly.FromDateTime(DateTime.Now);
    var path = ThisComputer.Folders.MemoryFile;
    if (mode == MemoryBridgeMode.Rules)
    {
        WriteOutput(MemoryBridge.RulesOutput(today));
        return;
    }

    if (mode == MemoryBridgeMode.Facts)
    {
        WriteOutput(MemoryBridge.FactsOutput(ReadIfThere(path)));
        return;
    }

    MemoryBridgeStep step;
    using (MemoryDisk.TakeLock(ThisComputer.Folders.MemoryFile))
    {
        step = MemoryBridge.OnPrompt(input, ReadIfThere(path), today);
        if (step.MemoryJson is { } json)
        {
            MemoryDisk.Write(ThisComputer.Folders.MemoryFile, json, step.BadFile);
        }
    }

    if (step.Output is { } output)
    {
        WriteOutput(output);
    }
}

/// The MCP server with the memory tools: one JSON-RPC message per line, until Claude Code closes
/// the input. The file is read again for every message, because hooks of other sessions and the
/// app write it too.
static int ServeMemory()
{
    using var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
    using var stdout = Console.OpenStandardOutput();
    var version = typeof(MemoryServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    while (stdin.ReadLine() is { } line)
    {
        if (string.IsNullOrWhiteSpace(line) || AnswerServerLine(line, version) is not { } reply)
        {
            continue;
        }

        stdout.Write(Encoding.UTF8.GetBytes(reply + "\n"));
        stdout.Flush();
    }

    return 0;
}

/// A change that could not be written is answered as a failed tool call: the model must not read
/// "Saved" for a fact that is not in the file.
static string? AnswerServerLine(string line, string version)
{
    try
    {
        using var gate = MemoryDisk.TakeLock(ThisComputer.Folders.MemoryFile);
        var path = ThisComputer.Folders.MemoryFile;
        var step = MemoryBridge.OnServerLine(line, ReadIfThere(path), DateOnly.FromDateTime(DateTime.Now), version);
        if (step.MemoryJson is { } json)
        {
            MemoryDisk.Write(ThisComputer.Folders.MemoryFile, json, step.BadFile);
        }

        return step.Output;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        return MemoryBridge.WriteFailedReply(line);
    }
}

/// Writes the plugin folder for the environment this run belongs to and prints its path.
/// Exit code 1 with a reason on stderr when that is not possible: Claude Code then keeps the
/// version it already has.
static int BuildPersonaPlugin()
{
    try
    {
        var configDirectory = ClaudeConfigFolder.Resolve(
            Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var bridge = Environment.ProcessPath ?? throw new InvalidOperationException("the bridge does not know its own path");

        var persona = PersonaSettings.FromJson(ReadIfThere(ThisComputer.Folders.PersonaFile));
        var files = PersonaPlugin.Files(persona.Temperament, bridge);
        var hash = PersonaPlugin.ContentHash(files);
        var folder = PersonaPluginOutput.VersionFolder(ThisComputer.Folders, configDirectory, hash);

        if (!Directory.Exists(folder))
        {
            WritePluginFolder(folder, files);
        }

        ClearOldPluginFolders(Path.GetDirectoryName(folder)!, hash);
        WriteOutput(folder + "\n");
        return 0;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Aiko could not build the persona plugin: {e.Message}");
        return 1;
    }
}

/// Written into a temporary folder first and moved into place in one step, so Claude Code never
/// copies a folder with half the files in it. Two runs racing for the same hash write the same
/// content; the one that loses the move just drops its copy.
static void WritePluginFolder(string folder, IReadOnlyDictionary<string, string> files)
{
    var temporary = folder + "." + Environment.ProcessId + ".tmp";
    foreach (var (relative, content) in files)
    {
        var path = Path.Combine(temporary, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    try
    {
        Directory.Move(temporary, folder);
    }
    catch (IOException) when (Directory.Exists(folder))
    {
        Directory.Delete(temporary, recursive: true);
    }
}

static void ClearOldPluginFolders(string environmentFolder, string currentHash)
{
    var folders = new DirectoryInfo(environmentFolder).GetDirectories()
        .Select(d => (d.Name, (DateTimeOffset)d.LastWriteTimeUtc));
    foreach (var stale in PersonaPluginOutput.StaleFolders(folders, currentHash, DateTimeOffset.UtcNow))
    {
        try
        {
            Directory.Delete(Path.Combine(environmentFolder, stale), recursive: true);
        }
        catch (IOException)
        {
            // Busy right now; the next run tries again.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

static string ReadIfThere(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

static void WriteOutput(string text)
{
    using var stdout = Console.OpenStandardOutput();
    var bytes = Encoding.UTF8.GetBytes(text);
    stdout.Write(bytes, 0, bytes.Length);
    stdout.Flush();
}

/// The status line the user had before Aiko. It gets the same input and its output is printed
/// as is, so nothing the user built is lost.
///
/// It runs through the shell Claude Code itself would have used, not through cmd.exe. Claude Code
/// runs status lines with Git Bash and falls back to PowerShell, so a line the user wrote is a
/// bash line on almost every machine. Through cmd.exe such a line prints nothing, and the promise
/// in the readme and in the wizard that an existing status line keeps working would be false.
///
/// The looking only happens when there is something to run, which is never for most people.
static string RunWrapped(string command, string input)
{
    var gitBash = WindowsGitBash.Find(
        File.Exists,
        WindowsGitBash.Places(ThisComputer.SystemFolders, Environment.GetEnvironmentVariable("PATH") ?? string.Empty));
    var (fileName, arguments) = ClaudeShellLookup.CallFor(ThisComputer.Platform, gitBash, command);

    var start = new ProcessStartInfo(fileName)
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
    };

    // Handed over one by one so the runtime quotes them. The command is somebody else's text and
    // may hold quotes of its own; escaping it by hand is a bug waiting to be written.
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start);
    if (process is null)
    {
        return "";
    }

    var output = process.StandardOutput.ReadToEndAsync();
    try
    {
        process.StandardInput.Write(input);
        process.StandardInput.Close();
    }
    catch (IOException)
    {
        // The command may exit without reading its input; that is its right.
    }

    if (!process.WaitForExit(2000))
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        return "";
    }

    return output.GetAwaiter().GetResult();
}
