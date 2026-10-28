using System.Diagnostics;
using System.Text;
using Aiko.Core;

// Claude Code runs this on every status line update and waits for its output, so two rules hold
// above everything else: be quick, and never fail. Whatever goes wrong, the exit code is 0 and
// the user's own status line still gets printed.
// Claude Code runs "Aiko.Bridge.exe plugin aiko-persona" to get the persona plugin (D-201). It
// sends nothing on stdin, so this is decided before any input is read: waiting for input that
// never comes would hang the install. Unlike the status line, a failure here must show as one.
if (PersonaPluginOutput.IsRequest(args))
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

    if (memoryMode is { } memoryHook)
    {
        AnswerMemoryHook(memoryHook, input);
        return 0;
    }

    if (args is [SessionReminder.Argument, ..])
    {
        RemindAboutBinding(input);
        return 0;
    }

    // The hook of a session in the terminal of Aiko (D-360). It comes with --settings, so it runs
    // in every environment, with the personality or without it.
    if (args is [Aiko.Core.Terminal.TerminalEvent.HookArgument, ..])
    {
        RecordTerminalEvent(input);
        return 0;
    }

    // The persona plugin's hooks (D-206). Before this branch a hook call fell through to the status
    // line code, which could run the person's own status line with hook input.
    if (args is [PersonaPlugin.HookArgument, ..])
    {
        RecordActivity(input);
        return 0;
    }

    // Without the variable Claude Code works in ~\.claude, so the bridge does too. Naming that case
    // anything else made the app look for a file that was never written.
    var configDirectory = ClaudeConfigFolder.Resolve(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    var environment = SnapshotName.For(configDirectory);

    var report = StatusLineReport.FromJson(input);
    if (report.HasData)
    {
        var snapshot = LimitSnapshot.FromStatusLine(environment, DateTimeOffset.Now, report);
        WriteSnapshot(environment, SnapshotFile.ToJson(snapshot));
    }

    var wrapped = ReadWrappedCommand(configDirectory);
    var output = wrapped is null ? "" : RunWrapped(wrapped, input);
    if (output.Length > 0)
    {
        WriteOutput(output);
    }
}
catch (Exception)
{
    // A status line that throws would show a stack trace under the prompt. Silence is better.
}

return 0;

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
    var folder = SessionReminder.WorkingDirectoryIn(input);
    if (folder is null)
    {
        return;
    }

    var environments = ReadIfThere(ThisComputer.Folders.EnvironmentsFile);
    var language = AppSettings.FromJson(ReadIfThere(ThisComputer.Folders.SettingsFile)).Language;

    var message = SessionReminder.MessageFor(
        Environment.GetEnvironmentVariable(SessionReminder.LaunchVariable),
        Environment.GetEnvironmentVariable(SessionReminder.EnvironmentVariable),
        folder,
        EnvironmentSettings.FromJson(environments),
        SessionReminder.IsRussian(language, WindowsLanguage.UserInterface()));

    if (message is not null)
    {
        WriteOutput(SessionReminder.HookOutput(message));
    }
}

/// One small file per session for the face in the tray. An event that means nothing for the face
/// writes nothing, and the same activity is not written again every few seconds.
static void RecordActivity(string input)
{
    if (HookEvent.FromJson(input) is not { } hook)
    {
        return;
    }

    var configDirectory = ClaudeConfigFolder.Resolve(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    var folder = ThisComputer.Folders.ActivityFolder;
    var environment = SnapshotName.For(configDirectory);
    var path = Path.Combine(folder, ActivityRecord.FileName(environment, hook.SessionId));

    if (hook.Activity == SessionActivity.Ended)
    {
        File.Delete(path);
        return;
    }

    var now = DateTimeOffset.Now;
    var written = File.Exists(path) ? ActivityRecord.FromJson(File.ReadAllText(path)) : null;
    if (!ActivityRecord.NeedsWrite(written, hook.Activity, now))
    {
        return;
    }

    Directory.CreateDirectory(folder);
    ReplaceFile(path, new ActivityRecord(environment, hook.Activity, now).ToJson());
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
    ReplaceFile(path, terminalEvent.ToJson());
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
    using (TakeMemoryLock())
    {
        step = MemoryBridge.OnPrompt(input, ReadIfThere(path), today);
        if (step.MemoryJson is { } json)
        {
            WriteMemory(json, step.BadFile);
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
        using var gate = TakeMemoryLock();
        var path = ThisComputer.Folders.MemoryFile;
        var step = MemoryBridge.OnServerLine(line, ReadIfThere(path), DateOnly.FromDateTime(DateTime.Now), version);
        if (step.MemoryJson is { } json)
        {
            WriteMemory(json, step.BadFile);
        }

        return step.Output;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        return MemoryBridge.WriteFailedReply(line);
    }
}

/// One writer at a time. Hooks of several sessions, the memory server and the app all read the
/// file, change it and write it back; without a gate the slower one would undo the other's change.
static FileStream TakeMemoryLock()
{
    var path = ThisComputer.Folders.MemoryFile + ".lock";
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) when (attempt < 80)
        {
            Thread.Sleep(25);
        }
    }
}

/// A file that was not a memory is kept as ".bad" before the new one takes its place, so a slip
/// in a hand edit never costs the person their facts.
static void WriteMemory(string json, bool keepBadFile)
{
    var path = ThisComputer.Folders.MemoryFile;
    if (keepBadFile && File.Exists(path))
    {
        File.Move(path, path + ".bad", overwrite: true);
    }

    ReplaceFile(path, json);
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

static string SnapshotPath(string environment)
{
    Directory.CreateDirectory(ThisComputer.Folders.SnapshotsFolder);
    return ThisComputer.Folders.SnapshotFile(environment);
}

static void WriteSnapshot(string environment, string json) => ReplaceFile(SnapshotPath(environment), json);

/// Write to a temporary file and move it over the old one, so the tray never sees half a file.
/// While the tray reads the old file, Windows refuses to replace it, so the move is tried again
/// for a short while. A temporary file that could not take the place is removed, not left in the
/// folder (AIKO-267).
static void ReplaceFile(string path, string text)
{
    const int Attempts = 10;
    var temporary = path + "." + Environment.ProcessId + ".tmp";
    try
    {
        File.WriteAllText(temporary, text, new UTF8Encoding(false));
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < Attempts)
            {
                Thread.Sleep(20);
            }
        }
    }
    finally
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Aiko removes it when it starts.
        }
    }
}

static string? ReadWrappedCommand(string? configDirectory)
{
    if (string.IsNullOrWhiteSpace(configDirectory))
    {
        return null;
    }

    var settings = Path.Combine(configDirectory, "settings.json");
    if (!File.Exists(settings))
    {
        return null;
    }

    return SettingsJsonPatch.ReadWrappedCommand(File.ReadAllText(settings));
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

/// What the core needs to know about this Windows computer. The core describes the rules; the
/// bridge only fills them in from Windows.
static class ThisComputer
{
    public static readonly PlatformConventions Platform = PlatformConventions.Windows;

    public static readonly AikoFolders Folders = AikoFolders.Windows(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static readonly WindowsSystemFolders SystemFolders = new(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows));
}
