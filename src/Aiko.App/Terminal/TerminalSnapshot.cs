using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Draws the window of the terminal into a file, on made up sessions: every state of the window
/// can be looked at without Claude Code and without touching the sessions of the person.
static class TerminalSnapshot
{
    public static readonly IReadOnlyList<string> States =
    [
        "talk", "feed", "working", "empty", "form", "elsewhere", "login", "close",
        "voice-idle", "voice-warming", "voice-listening", "voice-finishing", "voice-nomic", "voice-model",
    ];

    /// --snapshot-terminal &lt;file&gt; &lt;state&gt; [width]
    public static bool Write(string path, string state, double width)
    {
        if (!States.Contains(state))
        {
            return false;
        }

        var panel = new TerminalPanel { Width = width, Height = 700 };
        panel.Show(View(state));
        Voice(panel.Input, state);
        Snapshot.Write(panel, path);
        return true;
    }

    /// The moments of a recording (D-315), drawn on the field under a short conversation.
    private static void Voice(TalkInput input, string state)
    {
        (VoicePhase phase, VoiceNotice notice) = state switch
        {
            "voice-idle" => (VoicePhase.Idle, VoiceNotice.None),
            "voice-warming" => (VoicePhase.Warming, VoiceNotice.None),
            "voice-listening" => (VoicePhase.Listening, VoiceNotice.None),
            "voice-finishing" => (VoicePhase.Finishing, VoiceNotice.None),
            "voice-nomic" => (VoicePhase.Idle, VoiceNotice.NoMicrophone),
            "voice-model" => (VoicePhase.Idle, VoiceNotice.NeedsModel),
            _ => (VoicePhase.Off, VoiceNotice.None),
        };

        input.ShowVoice(VoiceInput.Line(new VoiceState(phase, notice), TimeSpan.FromSeconds(4.2), VoiceEngine.Local), notice);
        if (phase is VoicePhase.Warming or VoicePhase.Listening or VoicePhase.Finishing)
        {
            input.BeginDictation();
            var said = phase == VoicePhase.Warming
                ? DictationText.Empty
                : DictationText.Empty
                    .Pass("Сделай ветку feature/voice-input от in-progress и прогони")
                    .Pass("Сделай ветку feature/voice-input от in-progress и прогони dotnet test");
            input.ShowDictation(said);
            foreach (var level in new[] { 0.05, 0.1, 0.5, 0.8, 0.6, 0.9, 0.4, 0.7, 0.3, 0.05, 0.05, 0.6, 0.85, 0.5, 0.7, 0.35 })
            {
                input.PushLevel(level);
            }
        }
    }

    /// --try-feed-scroll: a long answer is written in pieces into the real feed, in a window off
    /// the screen. Twice the reader scrolls up, into the answer and above it, and the feed must stay
    /// where it was left while the answer goes on (check of 2026-10-15). The log says how far it
    /// moved; the exit code is 0 when it did not.
    public static int FeedScroll()
    {
        _ = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        var panel = new TerminalPanel();
        var window = new System.Windows.Window
        {
            Content = panel,
            Width = 1100,
            Height = 700,
            Left = -4000,
            Top = 0,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        window.Show();

        var view = View("working");
        var talk = view.Talk!;
        var answer = "";

        void Write(int pieces)
        {
            for (var i = 0; i < pieces; i++)
            {
                answer += $"Paragraph {answer.Length / 90 + 1}: the answer goes on and on, the way a long one does, line after line.\n\n";
                panel.Show(view with { Talk = talk with { Items = [.. talk.Items, new FeedItem.Answer(100, answer, true)] } });
                Settle();
            }
        }

        Write(30);
        var scroll = FeedScroller(panel);
        if (scroll is null)
        {
            TerminalLog.FeedScrollChecked(false, 0, 0, 0, 0);
            return 1;
        }

        // At the end, the feed follows the answer as it grows.
        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
        Settle();
        Write(5);
        var behind = (int)Math.Round(scroll.ScrollableHeight - scroll.VerticalOffset);

        // The reader at the end scrolls up while a piece is on its way: between the draw and the
        // moment the window settles.
        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
        Settle();
        answer += "One more piece.\n\n";
        panel.Show(view with { Talk = talk with { Items = [.. talk.Items, new FeedItem.Answer(100, answer, true)] } });
        // The move lands on the next layout, and a virtualizing feed may land it elsewhere than
        // asked: where it landed is where the reader is.
        ReaderScrolls(scroll, scroll.VerticalOffset - 300);
        Settle();
        var leftAtWheel = scroll.VerticalOffset;
        Write(5);
        var whileWriting = (int)Math.Round(scroll.VerticalOffset - leftAtWheel);

        var insideAnswer = MovedAfterScroll(500);
        var aboveAnswer = MovedAfterScroll(scroll.ScrollableHeight - 150);

        // The reader scrolls up so far from the end, and the answer goes on.
        int MovedAfterScroll(double up)
        {
            ReaderScrolls(scroll, Math.Max(0, scroll.ScrollableHeight - up));
            Settle();
            var left = scroll.VerticalOffset;
            Write(15);
            return (int)Math.Round(scroll.VerticalOffset - left);
        }

        window.Close();
        TerminalLog.FeedScrollChecked(true, behind, whileWriting, insideAnswer, aboveAnswer);
        return new[] { behind, whileWriting, insideAnswer, aboveAnswer }.Any(moved => Math.Abs(moved) > 2) ? 1 : 0;
    }

    /// The reader turns the wheel up, as a hand does: the feed hears the wheel, then the offset moves.
    private static void ReaderScrolls(System.Windows.Controls.ScrollViewer scroll, double to)
    {
        scroll.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, 120)
        {
            RoutedEvent = System.Windows.UIElement.PreviewMouseWheelEvent,
        });
        scroll.ScrollToVerticalOffset(to);
    }

    private static void Settle() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    private static System.Windows.Controls.ScrollViewer? FeedScroller(System.Windows.DependencyObject parent)
    {
        for (var at = 0; at < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, at);
            if (child is System.Windows.Controls.ScrollViewer { TemplatedParent: System.Windows.Controls.ListBox } scroll)
            {
                return scroll;
            }

            if (FeedScroller(child) is { } found)
            {
                return found;
            }
        }

        return null;
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
            state == "feed" ? Feed(withCard: true)
                : state == "working" ? Feed(withCard: false)
                : state.StartsWith("voice-", StringComparison.Ordinal) ? Feed(withCard: false) with { Busy = false }
                : null,
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
            new FeedItem.OwnerMessage(1, "Add the folder groups to the form of a new session.", false, []),
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
            new FeedItem.OwnerMessage(7, "And the same on the Mac, please.", true, []),
        ];

        return new TalkView("a2", items, Busy: true, HasOpenCard: withCard, PermissionMode.Manual, "Opus 5.5", "Aiko");
    }
}
