using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// --try-editor: the editor under the terminal with real programs (D-369). A Claude Code on Haiku and
/// a Git Bash run in pseudo consoles; the editor stands in a window off the screen that never takes
/// the keyboard, and the keys go to its handlers, the way the live keys do. No key and no click
/// reaches the desktop, and the clipboard is not touched. Each check is a line PASS or FAIL in the
/// report, with what was seen; the words of the test conversation stay in its files. It spends a
/// little of the limits of environment 1.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalEditorCheck
{
    private static readonly Regex Sequence = new(@"\x1b\[[0-?]*[ -/]*[@-~]|\x1b\][^\x07\x1b]*(\x07|\x1b\\)|\x1b.", RegexOptions.Compiled);

    public static int Run(string folder, string reportPath)
    {
        var report = new List<string>();
        var failed = false;
        void Check(string what, bool ok, string seen = "")
        {
            failed |= !ok;
            report.Add($"{(ok ? "PASS" : "FAIL")} {what}{(seen.Length > 0 ? " — " + seen : "")}");
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Startup += async (_, _) =>
        {
            try
            {
                await Steps(folder, Check, report);
            }
            catch (Exception e)
            {
                Check("no exception", false, e.ToString().Replace(Environment.NewLine, " | "));
            }

            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();
        return failed ? 1 : 0;
    }

    private static async Task Steps(string folder, Action<string, bool, string> check, List<string> report)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "note.txt"), "The word is lantern.\n");
        var picture = Path.Combine(folder, "red.png");
        WriteRedSquare(picture);

        var editor = new TerminalEditor { Width = 700, CheckWithoutFocus = true };
        var window = new Window
        {
            Left = -10000,
            Top = -10000,
            Width = 760,
            Height = 300,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Content = editor,
        };
        window.Show();

        var passed = new List<string>();
        var voicePresses = 0;
        editor.VoicePressed += () => voicePresses++;

        // ---- Claude Code ----
        var claude = ClaudeLauncher.FindClaude();
        var bridge = BridgePath.Current();
        if (claude is null || bridge is null)
        {
            check("claude and the bridge are found", false, "");
            return;
        }

        var hooks = Path.Combine(folder, "terminal-hooks.json");
        File.WriteAllText(hooks, TerminalHooks.SettingsJson(bridge));
        var id = Guid.NewGuid().ToString();
        var pictures = Path.Combine(folder, "pictures");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = ClaudeConfigFolder.Resolve(null, home);
        var inherited = Environment.GetEnvironmentVariables().Keys.Cast<string>().ToList();
        var plan = ClaudeLaunchPlan.For(new LaunchRequest(config, home, "check", id, false, hooks, pictures), inherited);
        var arguments = plan.Arguments.Concat(["--model", "haiku", "--permission-mode", "default"])
            .Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument);

        var screen = new StringBuilder();
        var titles = new TitleReader();
        var idle = false;
        using var console = new PseudoConsole(new PtyLaunch(
            string.Join(' ', [$"\"{claude}\"", .. arguments]), folder,
            plan.Variables.ToDictionary(change => change.Name, change => change.Value, StringComparer.OrdinalIgnoreCase)));
        console.TerminalOutput += (_, e) =>
        {
            lock (screen)
            {
                screen.Append(e.Data);
            }

            if (titles.Read(e.Data) is { } title && ClaudeTitle.Parse(title) is { } parsed)
            {
                idle = !parsed.Working;
            }
        };
        console.Start();
        console.Resize(30, 120);

        var events = Path.Combine(ThisComputer.Folders.TerminalEventsFolder, id + ".json");
        TerminalEvent? Last() => File.Exists(events) ? TerminalEvent.FromJson(Read(events)) : null;

        // The editor writes to one program at a time: Claude Code first, then Git Bash.
        var current = console;
        editor.Show(new EditorSession(id, SessionKind.Claude, folder, '/', pictures));
        editor.Passed += sequence =>
        {
            passed.Add(sequence);
            current.WriteInput(sequence);
        };
        var sends = 0;
        editor.Sent += async writes =>
        {
            sends++;
            for (var at = 0; at < writes.Count; at++)
            {
                if (at > 0)
                {
                    await Task.Delay(60);
                }

                current.WriteInput(writes[at]);
            }
        };

        static async Task<bool> Until(Func<bool> done, int seconds)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (done())
                {
                    return true;
                }

                await Task.Delay(150);
            }

            return done();
        }

        if (!await Until(() => idle, 30))
        {
            console.WriteInput("\r");
            await Until(() => idle, 30);
        }

        check("Claude Code is ready", idle, "");
        await Task.Delay(1000);

        // 1. Two lines, one message.
        editor.TypeForCheck("Answer with one word: what is 2+2?");
        editor.Press(Key.Enter, ModifierKeys.Shift);
        editor.TypeForCheck("This second line is part of the same message.");
        check("Shift+Enter starts a line in the editor", editor.TextForCheck.Contains('\n'), "");
        editor.Press(Key.Enter, ModifierKeys.None);
        check("Enter sends and empties the editor", sends == 1 && editor.TextForCheck.Length == 0, "");
        await Until(() => Last()?.Kind == TerminalEventKind.Finished, 90);
        var transcript = TranscriptPath.For(PlatformConventions.Windows, config, folder, id);
        var messages = UserMessages(transcript).Where(message => !message.Command).ToList();
        check(
            "Claude Code got one message with both lines",
            messages.Count == 1 && messages[0].Text.Contains("2+2") && messages[0].Text.Contains("second line") && messages[0].Text.Contains('\n'),
            $"{messages.Count} message(s)");

        // 2. "/" in an empty editor: every key goes to the menu of Claude Code until Enter.
        await Until(() => idle, 20);
        passed.Clear();
        editor.TypeForCheck("/cost");
        check("/cost goes key by key to the terminal, the editor stays empty", string.Concat(passed) == "/cost" && editor.TextForCheck.Length == 0, string.Join("|", passed));
        editor.Press(Key.Enter, ModifierKeys.None);
        check("Enter ends the menu in the terminal", passed.LastOrDefault() == "\r", "");
        await Task.Delay(3000);
        // /cost opens a panel of Claude Code that Esc closes, as a person would.
        editor.Press(Key.Escape, ModifierKeys.None);
        await Task.Delay(1000);
        check("/cost was a command, not a message", UserMessages(transcript).Count(message => message.Text.Contains("/cost") && !message.Command) == 0, "");

        // 3. A dropped picture goes as a picture.
        await Until(() => idle, 20);
        editor.DropForCheck([picture]);
        editor.TypeForCheck("What colour is the picture? One word.");
        editor.Press(Key.Enter, ModifierKeys.None);
        var before = Last()?.At ?? 0;
        await Until(() => Last() is { Kind: TerminalEventKind.Finished } last && last.At > before, 90);
        var withPicture = UserMessages(transcript).Any(message => message.Images > 0);
        check("Claude Code got the picture as an attachment", withPicture, $"{UserMessages(transcript).Sum(message => message.Images)} image(s)");
        check("the picture lies in the folder of the session", Directory.Exists(pictures) && Directory.EnumerateFiles(pictures, "*.png").Any(), "");

        // 4. "@" offers the files of the folder.
        await Until(() => idle, 20);
        editor.TypeForCheck("@not");
        await Until(() => editor.ChoicesOpenForCheck, 10);
        check("@ offers the files of the project", editor.ChoicesOpenForCheck && editor.OfferedForCheck.Contains("note.txt"), string.Join(",", editor.OfferedForCheck));
        editor.Press(Key.Escape, ModifierKeys.None);
        editor.ClearForCheck();

        // 5. Esc goes to the program even with text in the editor.
        editor.TypeForCheck("text that stays");
        passed.Clear();
        editor.Press(Key.Escape, ModifierKeys.None);
        check("Esc goes to the program and the text stays", passed.SequenceEqual([EditorKeys.Escape]) && editor.TextForCheck == "text that stays", "");
        editor.ClearForCheck();

        // 6. Voice writes at the caret and Enter ends the recording without sending.
        editor.TypeForCheck("Добавь тест");
        var sendsBefore = sends;
        editor.BeginDictation();
        editor.ShowDictation(DictationText.Phrases("про Esc", ""));
        editor.Press(Key.Enter, ModifierKeys.None);
        editor.EndDictation("про Esc");
        check("Enter during a recording ends it and sends nothing", voicePresses == 1 && sends == sendsBefore, "");
        check("the words stand after the text", editor.TextForCheck == "Добавь тест про Esc", editor.TextForCheck);
        editor.ClearForCheck();

        // The end: /exit through the menu path.
        editor.TypeForCheck("/exit");
        editor.Press(Key.Enter, ModifierKeys.None);
        await Task.Delay(3000);
        console.Dispose();

        // ---- Git Bash ----
        var bash = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
        }.FirstOrDefault(File.Exists);
        if (bash is null)
        {
            report.Add("SKIP the command line: Git Bash is not found");
            window.Close();
            return;
        }

        var shellScreen = new StringBuilder();
        using var shell = new PseudoConsole(new PtyLaunch($"\"{bash}\" --login -i", folder, new Dictionary<string, string?>()));
        shell.TerminalOutput += (_, e) =>
        {
            lock (shellScreen)
            {
                shellScreen.Append(e.Data);
            }
        };
        shell.Start();
        shell.Resize(30, 120);
        string ShellPlain()
        {
            lock (shellScreen)
            {
                return Sequence.Replace(shellScreen.ToString(), " ");
            }
        }

        await Until(() => ShellPlain().Contains('$'), 20);
        editor.Show(new EditorSession(id + "-shell", SessionKind.Shell, folder, '/', pictures));
        current = shell;

        editor.TypeForCheck("echo editor-ok");
        editor.Press(Key.Enter, ModifierKeys.None);
        var ran = await Until(() => Regex.Matches(ShellPlain(), "editor-ok").Count >= 2, 15);
        check("a command from the editor runs in the command line", ran, "");
        editor.Press(Key.Up, ModifierKeys.None);
        check("↑ in an empty editor brings the command back", editor.TextForCheck == "echo editor-ok", editor.TextForCheck);
        editor.ClearForCheck();
        editor.TypeForCheck("cat no");
        editor.Press(Key.Tab, ModifierKeys.None);
        check("Tab completes a path", editor.TextForCheck == "cat note.txt ", editor.TextForCheck);
        editor.Press(Key.Enter, ModifierKeys.None);
        check("the completed command runs", await Until(() => ShellPlain().Contains("lantern"), 15), "");
        editor.TypeForCheck("exit");
        editor.Press(Key.Enter, ModifierKeys.None);
        await Task.Delay(1000);
        window.Close();
    }

    /// The messages of the person in a conversation file: their text, how many pictures each has,
    /// and whether it was a command of Claude Code.
    private static List<(string Text, int Images, bool Command)> UserMessages(string transcript)
    {
        var found = new List<(string, int, bool)>();
        if (!File.Exists(transcript))
        {
            return found;
        }

        string all;
        using (var stream = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            all = reader.ReadToEnd();
        }

        foreach (var line in all.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("type", out var type) && type.GetString() == "user"
                    && !(root.TryGetProperty("isMeta", out var meta) && meta.ValueKind == JsonValueKind.True)
                    && root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        var text = content.GetString() ?? "";
                        found.Add((text, 0, text.StartsWith('<')));
                    }
                    else if (content.ValueKind == JsonValueKind.Array)
                    {
                        var parts = content.EnumerateArray().ToList();
                        if (parts.Any(part => part.TryGetProperty("type", out var kind) && kind.GetString() == "tool_result"))
                        {
                            continue;
                        }

                        var text = string.Concat(parts.Where(part => part.TryGetProperty("text", out _)).Select(part => part.GetProperty("text").GetString()));
                        var images = parts.Count(part => part.TryGetProperty("type", out var kind) && kind.GetString() == "image");
                        found.Add((text, images, text.StartsWith('<')));
                    }
                }
            }
            catch (JsonException)
            {
                // A line being written.
            }
        }

        return found;
    }

    private static string? Read(string path)
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

    private static void WriteRedSquare(string path)
    {
        var bitmap = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgr32, null, Enumerable.Repeat(new byte[] { 0, 0, 255, 0 }, 64 * 64).SelectMany(pixel => pixel).ToArray(), 64 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
