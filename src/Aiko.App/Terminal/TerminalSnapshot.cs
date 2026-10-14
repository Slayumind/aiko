using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Draws the window of the terminal into a file, on made up sessions: every state of the window
/// can be looked at without Claude Code and without touching the sessions of the person.
static class TerminalSnapshot
{
    public static readonly IReadOnlyList<string> States = ["talk", "feed", "working", "empty", "form", "elsewhere", "login", "close"];

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
            state == "feed" ? Feed(withCard: true) : state == "working" ? Feed(withCard: false) : null,
            now);
    }

    /// A short made up conversation: a message, steps, an answer, a queued message, and a card that
    /// waits, or without it the turn that still works.
    private static TalkView Feed(bool withCard)
    {
        using var input = System.Text.Json.JsonDocument.Parse(
            "{\"file_path\":\"/work/aiko/NewSessionForm.cs\",\"old_string\":\"a\\nb\",\"new_string\":\"a\\nc\\nd\"}");
        var edit = new StreamEvent.Request("r1", "Edit", "t3", input.RootElement.Clone(), "", true, false);

        static FeedItem.Step Step(int id, StepKind kind, string subject, StepState state, int added = 0, int removed = 0) =>
            new(id, $"t{id}", new StepLine(kind, "", subject, added, removed, false), state, "", [], "");

        List<FeedItem> items =
        [
            new FeedItem.OwnerMessage(1, "Add the folder groups to the form of a new session.", 0, false, []),
            Step(2, StepKind.Read, "NewSessionChoices.cs", StepState.Done),
            Step(3, StepKind.Search, "FolderSource", StepState.Done),
            new FeedItem.Answer(4, string.Join("\n",
                "## Where the groups come from",
                "",
                "The groups come from the core: **`NewSessionChoices.Headers`** says where each one starts. See [the form](src/Aiko.App/Terminal/TerminalPanel.cs:502).",
                "",
                "1. A header before the first *recent* folder.",
                "2. A header before the first bound folder.",
                "",
                "```csharp",
                "var headers = NewSessionChoices.Headers(form.Folders);",
                "```",
                "",
                "| Group | Starts at |",
                "|---|---:|",
                "| Recent | 0 |",
                "| Bound | 3 |"), false),
            Step(5, StepKind.Edit, "DropDown.cs", StepState.Done, 18, 2),
            withCard ? new FeedItem.Card(6, RequestCard.From(edit), RequestState.Open) : Step(6, StepKind.Run, "dotnet test Aiko.Core.Tests", StepState.Running),
            new FeedItem.OwnerMessage(7, "And the same on the Mac, please.", 0, true, []),
        ];

        return new TalkView(items, Busy: true, HasOpenCard: withCard, PermissionMode.Manual, "Opus 5.5", "Aiko");
    }
}
