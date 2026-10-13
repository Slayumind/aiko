using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Draws the window of the terminal into a file, on made up sessions: every state of the window
/// can be looked at without Claude Code and without touching the sessions of the person.
static class TerminalSnapshot
{
    public static readonly IReadOnlyList<string> States = ["talk", "empty", "form", "elsewhere", "login", "close"];

    /// --snapshot-terminal &lt;file&gt; &lt;state&gt; [width]
    public static bool Write(string path, string state, double width)
    {
        if (!States.Contains(state))
        {
            return false;
        }

        var panel = new TerminalPanel { Width = width, Height = 700 };
        panel.Show(View(state));
        Snapshot.Write(panel, path);
        return true;
    }

    private static TerminalView View(string state)
    {
        var now = DateTimeOffset.Now;
        var yesterday = now.AddDays(-1).ToUnixTimeSeconds();

        static TalkGlance Glance(TalkActivity activity, GlanceLine line, StepKind kind, string text) => new(activity, line, kind, "", text);

        IReadOnlyList<RowGroup> groups = state == "empty"
            ? []
            :
            [
                new RowGroup("aiko", "aiko",
                [
                    new SessionRow("a1", "office: reports", RowState.Working, Glance(TalkActivity.Working, GlanceLine.Step, StepKind.Read, "office.mjs"), 0),
                    new SessionRow("a2", "terminal", RowState.Waiting, Glance(TalkActivity.Waiting, GlanceLine.Request, StepKind.Edit, "NewSessionForm.cs"), 0),
                    new SessionRow("a3", "memory polish", RowState.Done, Glance(TalkActivity.Done, GlanceLine.Answer, StepKind.Other, "Done. Snapshots match in both locales"), 0),
                    new SessionRow("a4", "icon polish", RowState.Read, Glance(TalkActivity.Idle, GlanceLine.Answer, StepKind.Other, "Both locales checked, no differences"), 0),
                ]),
                new RowGroup("code-crawler", "code-crawler",
                [
                    new SessionRow("c1", "weapon balance", RowState.Asleep, null, yesterday),
                    new SessionRow("c2", "installer build", RowState.Stopped, Glance(TalkActivity.Failed, GlanceLine.StoppedAt, StepKind.Run, "dotnet publish"), 0),
                ]),
            ];

        var folders = new List<FolderChoice>
        {
            new(@"C:\Users\someone\Desktop\personal projects\aiko", "aiko", FolderSource.Recent),
            new(@"C:\Users\someone\Desktop\personal projects\for-work", "for-work", FolderSource.Inside),
        };
        var form = state switch
        {
            "form" => new FormView("Aiko", folders, 0, null, 0, "aiko 3"),
            "elsewhere" => new FormView("Aiko", folders, 1, "Work · Kodland", 0, "for-work 1"),
            _ => null,
        };

        return new TerminalView(
            [new TabView("Aiko", true, 0), new TabView("Work · Kodland", false, 1)],
            new BadgeView(42, LimitTone.Normal, LimitKind.FiveHour),
            groups,
            "Aiko",
            state == "empty" ? null : "a2",
            state == "empty" ? null : "terminal",
            form is not null ? MainView.Form : state == "empty" ? MainView.NoSessions : MainView.Talk,
            SignedOut: state == "login",
            form,
            state == "close" ? new AskView(string.Format(Strings.TerminalCloseTitle, "terminal"), Strings.TerminalCloseBody) : null,
            now);
    }
}
