using System.Diagnostics;
using System.Text;
using Aiko.Core;

// Claude Code runs this on every status line update and waits for its output, so two rules hold
// above everything else: be quick, and never fail. Whatever goes wrong, the exit code is 0 and
// the user's own status line still gets printed.
try
{
    var input = ReadAllInput();

    if (args is [SessionReminder.Argument, ..])
    {
        RemindAboutBinding(input);
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

    var aiko = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aiko");
    var environments = ReadIfThere(Path.Combine(aiko, "environments.json"));
    var language = AppSettings.FromJson(ReadIfThere(Path.Combine(aiko, "settings.json"))).Language;

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
    var folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aiko",
        "environments");
    Directory.CreateDirectory(folder);
    return Path.Combine(folder, environment + ".json");
}

/// Write to a temporary file and move it over the old one, so the tray never sees half a file.
static void WriteSnapshot(string environment, string json)
{
    var path = SnapshotPath(environment);
    var temporary = path + "." + Environment.ProcessId + ".tmp";
    File.WriteAllText(temporary, json, new UTF8Encoding(false));
    File.Move(temporary, path, overwrite: true);
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
    var gitBash = ClaudeShellLookup.FindGitBash(File.Exists);
    var (fileName, arguments) = ClaudeShellLookup.CallFor(gitBash, command);

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
