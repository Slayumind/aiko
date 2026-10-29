using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The terminal window on made up sessions, drawn into a file for a look at it (/look). No program
/// runs and nothing is read or written: the list shows every state of a row, the first folder
/// with its buttons and the hover of "+", and the palette is drawn on its own.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalSnapshot
{
    /// --snapshot-terminal out.png [palette | folders | loader | header | header-work | narrow |
    /// narrow-header | nolist]
    public static void Write(string path, string? part)
    {
        if (part == "loader")
        {
            Snapshot.Write(new System.Windows.Controls.Border
            {
                Width = 480,
                Height = 240,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Child = new TerminalLoader(),
            }, path);
            return;
        }

        if (part?.StartsWith("editor", StringComparison.Ordinal) == true)
        {
            Snapshot.Write(Editor(part), path);
            return;
        }

        if (part is "palette" or "folders")
        {
            Snapshot.Write(Palette(part == "folders"), path);
            return;
        }

        var narrow = part?.StartsWith("narrow", StringComparison.Ordinal) == true;
        var panel = new TerminalPanel { Width = narrow ? 600 : 1000, Height = 620 };
        var now = DateTimeOffset.Now;
        var seconds = now.ToUnixTimeSeconds();
        var active = part == "header-work" ? "Work · Kodland" : "Aiko";
        panel.Show(new TerminalView(
            [new TabView("Aiko", active == "Aiko", 0, 42, LimitTone.Normal), new TabView("Work · Kodland", active != "Aiko", 1, 82, LimitTone.Caution)],
            Header(part, active, now),
            [
                new RowGroup("C:\\work\\aiko", "aiko",
                [
                    new SessionRow("a1", "Fix the scroll of the feed", RowState.Working, Strings.TerminalEventReading.Replace("{0}", "office.mjs"), seconds),
                    new SessionRow("a2", "Голос в редактор", RowState.Waiting, Strings.TerminalEventAsksPermission.Replace("{0}", "npm test"), seconds),
                    new SessionRow("a3", "Меню значка", RowState.Done, "", seconds),
                    new SessionRow("a4", "bash", RowState.Read, "", seconds) { Kind = SessionKind.Shell },
                ]),
                new RowGroup("C:\\games\\code-crawler", "code-crawler",
                [
                    new SessionRow("c1", "Weapon balance", RowState.Asleep, "", seconds - 90000),
                    new SessionRow("c2", "Installer build", RowState.Stopped, "", seconds),
                ]),
            ],
            active,
            "a1",
            "Fix the scroll of the feed",
            MainView.Terminal,
            null,
            null,
            now));
        panel.ShowGroupHoverForSnapshot();
        Snapshot.Write(panel, path);
    }

    /// The header of the snapshot: open for "header", "header-work" and "narrow-header", the list
    /// hidden for "nolist". The limits are made up, the words are the card's own.
    private static HeaderView Header(string? part, string active, DateTimeOffset now)
    {
        var open = part is "header" or "header-work" or "narrow-header";
        if (!open)
        {
            return new HeaderView(false, part == "nolist", []);
        }

        // Aiko spoke just now, so it shows "working now", and it carries the weekly limit of the heavy
        // model; Work went quiet a few minutes back and has only the two usual windows.
        LimitSnapshot Limits(string name, DateTimeOffset received, int fiveHour, TimeSpan fiveHourLeft, int week, TimeSpan weekLeft) =>
            new(name, LimitSource.StatusLine, received,
            [
                new LimitWindow(LimitKind.FiveHour, fiveHour, now + fiveHourLeft),
                new LimitWindow(LimitKind.SevenDay, week, now + weekLeft),
            ]);

        var limits = new Dictionary<string, LimitSnapshot>
        {
            ["Aiko"] = Limits("Aiko", now, 42, TimeSpan.FromMinutes(143), 18, TimeSpan.FromHours(84))
                with { Model = new ModelLimit("Fable", 61, now.AddDays(5)) },
            ["Work · Kodland"] = Limits("Work · Kodland", now.AddMinutes(-4), 82, TimeSpan.FromMinutes(66), 40, TimeSpan.FromHours(98)),
        };
        var card = CardModel.From(
            [.. limits.Values.Select(snapshot => CardState.From(snapshot, now))],
            now,
            accounts: new Dictionary<string, CardAccount> { ["Aiko"] = new("Max 5x", true), ["Work · Kodland"] = new("Team", true) });
        var folders = new Dictionary<string, string> { ["Aiko"] = ".claude", ["Work · Kodland"] = ".claude-work" };

        var columns = HeaderStrip.Columns(
            [new HeaderInput("Aiko", limits["Aiko"], 0), new HeaderInput("Work · Kodland", limits["Work · Kodland"], 1)],
            active,
            now);
        return new HeaderView(true, false, [.. columns.Select(column => new HeaderColumnView(
            column,
            column.Active ? card.Blocks.First(block => block.Name == column.Environment) : null,
            folders[column.Environment],
            false))]);
    }

    /// editor (empty), editor-text, editor-compose, editor-shell, editor-voice.
    private static System.Windows.FrameworkElement Editor(string part)
    {
        var editor = new TerminalEditor { Width = 760 };
        var shell = part == "editor-shell";
        editor.Show(new EditorSession("s1", shell ? SessionKind.Shell : SessionKind.Claude, "C:\\work", '/', "C:\\work\\pictures"));
        editor.ShowVoice(new VoiceLine(VoiceWord.None, null, null, false, VoiceButton.Idle, false), VoiceNotice.None);
        switch (part)
        {
            case "editor-text":
                editor.TypedForSnapshot("Сначала проверь, что Esc в пустом редакторе доходит до карточки.\nПотом прогони тесты.", 1);
                break;
            case "editor-compose":
                editor.Compose();
                editor.TypedForSnapshot("Сначала проверь, что Esc в пустом редакторе доходит до карточки.\nПотом прогони тесты.", 1);
                break;
            case "editor-shell":
                editor.TypedForSnapshot("git log --oneline -5", 0);
                break;
            case "editor-voice":
                editor.TypedForSnapshot("Добавь тест, что Esc в пустом редакторе", 0);
                editor.ShowVoice(new VoiceLine(VoiceWord.Listening, "0:04", VoiceEngine.Local, true, VoiceButton.Recording, true), VoiceNotice.None);
                foreach (var level in new[] { 0.2, 0.6, 0.9, 0.4, 0.7, 0.3, 0.8, 0.5, 0.95, 0.35, 0.6, 0.25, 0.7, 0.45 })
                {
                    editor.PushLevel(level);
                }

                break;
        }

        return new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0A, 0x0A, 0x0A)),
            Child = editor,
        };
    }

    private static PaletteView Palette(bool folders)
    {
        static List<TextPart> Marked(string before, string match, string after) =>
            [new(before, false), new(match, true), new(after, false)];

        return folders
            ? new PaletteView(Strings.TerminalPickFolderHint, Strings.TerminalPickFolderKeys, _ =>
            [
                new PaletteGroup("",
                [
                    new PaletteRow("1", [new("aiko", false)], "personal projects", []),
                    new PaletteRow("2", [new("code-crawler", false)], "games", []),
                    new PaletteRow("3", [new("sparks", false)], "kodland", []),
                ]),
                new PaletteGroup("", [new PaletteRow("0", [new(Strings.TerminalPickOtherFolder, false)], "", [], Quiet: true)]),
            ])
            : new PaletteView(Strings.TerminalSearchHint, Strings.TerminalSearchKeys, _ =>
            [
                new PaletteGroup(Strings.TerminalSearchOpen, [new PaletteRow("1", Marked("", "Голос", " в редактор"), "Aiko · aiko · 14:02", [])]),
                new PaletteGroup(Strings.TerminalSearchClosed, [new PaletteRow("2", Marked("", "Голос", ": перевод диктовки"), "Aiko · aiko · 24 октября", [], Quiet: true)]),
                new PaletteGroup(Strings.TerminalSearchOutside,
                [
                    new PaletteRow("3", [new("Почини эхо подсказки", false)], Strings.TerminalSourceTerminal + " · aiko · 23 октября",
                        Marked("…эхо подсказки в ", "голос", "овом вводе"), Quiet: true),
                    new PaletteRow("4", Marked("", "Голос", " на маке: замер Whisper"), "VS Code · aiko · 15 октября", [], Quiet: true),
                ]),
            ]);
    }
}
