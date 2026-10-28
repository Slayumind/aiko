using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The terminal window on made up sessions, drawn into a file for a look at it (/look). No program
/// runs and nothing is read or written: the list shows every state of a row, the first folder
/// with its buttons and the hover of "+", and the palette is drawn on its own.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalSnapshot
{
    /// --snapshot-terminal out.png [palette | folders | loader]
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

        if (part is "palette" or "folders")
        {
            Snapshot.Write(Palette(part == "folders"), path);
            return;
        }

        var panel = new TerminalPanel { Width = 1000, Height = 620 };
        var now = DateTimeOffset.Now;
        var seconds = now.ToUnixTimeSeconds();
        panel.Show(new TerminalView(
            [new TabView("Aiko", true, 1), new TabView("Work · Kodland", false, 0)],
            new BadgeView(42, LimitTone.Normal, LimitKind.FiveHour),
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
            "Aiko",
            "a1",
            "Fix the scroll of the feed",
            MainView.Terminal,
            null,
            null,
            now));
        panel.ShowGroupHoverForSnapshot();
        Snapshot.Write(panel, path);
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
