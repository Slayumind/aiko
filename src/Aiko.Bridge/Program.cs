using System.Diagnostics;
using System.Text;
using Aiko.Core;

// Claude Code runs this on every status line update and waits for its output, so two rules hold
// above everything else: be quick, and never fail. Whatever goes wrong, the exit code is 0 and
// the user's own status line still gets printed.
try
{
    var input = ReadAllInput();
    var configDirectory = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
    var environment = EnvironmentNameFrom(configDirectory);

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
static string ReadAllInput()
{
    using var stdin = Console.OpenStandardInput();
    using var buffer = new MemoryStream();
    stdin.CopyTo(buffer);
    return Encoding.UTF8.GetString(buffer.ToArray());
}

static void WriteOutput(string text)
{
    using var stdout = Console.OpenStandardOutput();
    var bytes = Encoding.UTF8.GetBytes(text);
    stdout.Write(bytes, 0, bytes.Length);
    stdout.Flush();
}

/// One file per environment, named after the Claude Code config folder. The tray matches it
/// with the environment the user named; the bridge only knows the folder it was started for.
static string EnvironmentNameFrom(string? configDirectory)
{
    if (string.IsNullOrWhiteSpace(configDirectory))
    {
        return "default";
    }

    var name = new DirectoryInfo(configDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
    var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray()).Trim('.');
    return safe.Length > 0 ? safe : "default";
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
static string RunWrapped(string command, string input)
{
    var start = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
    };

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
