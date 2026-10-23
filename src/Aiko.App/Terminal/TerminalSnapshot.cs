using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Draws the window of the terminal into a file, on made up sessions: every state of the window
/// can be looked at without Claude Code and without touching the sessions of the person.
static class TerminalSnapshot
{
    public static readonly IReadOnlyList<string> States =
    [
        "talk", "feed", "working", "compacting", "long-input", "empty", "form", "elsewhere", "login", "close",
        "voice-idle", "voice-warming", "voice-listening", "voice-finishing", "voice-nomic", "voice-model",
        "questions", "questions-preview",
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
        if (state == "long-input")
        {
            // Twelve lines: more than the field shows, so it scrolls (AIKO-245).
            panel.Input.Typed(string.Join("\n", Enumerable.Range(1, 12).Select(line => $"Line {line} of a long message.")));
        }

        Snapshot.Write(panel, path);
        return true;
    }

    /// The moments of a recording (D-315), drawn on the field under a short conversation.
    private static void Voice(TalkInput input, string state)
    {
        (VoicePhase phase, VoiceNotice notice) = state switch
        {
            "voice-idle" or "long-input" => (VoicePhase.Idle, VoiceNotice.None),
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
            TerminalLog.FeedScrollChecked(false, 0, 0, 0, 0, 0);
            return 1;
        }

        // At the end, the feed follows the answer as it grows.
        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
        Settle();
        Write(5);
        var behind = (int)Math.Round(scroll.ScrollableHeight - scroll.VerticalOffset);

        // The field grows to five lines over the feed, and the reader at the end stays at the end
        // (check of 2026-10-16: the last paragraphs went under the field).
        var field = Find<System.Windows.Controls.TextBox>(panel, box => box.AcceptsReturn);
        var fieldGrows = -1;
        if (field is not null)
        {
            field.Text = "one\ntwo\nthree\nfour\nfive";
            Settle();
            fieldGrows = (int)Math.Round(scroll.ScrollableHeight - scroll.VerticalOffset);
            field.Text = "";
            Settle();
        }

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
        TerminalLog.FeedScrollChecked(true, behind, fieldGrows, whileWriting, insideAnswer, aboveAnswer);
        return new[] { behind, fieldGrows, whileWriting, insideAnswer, aboveAnswer }.Any(moved => Math.Abs(moved) > 2) ? 1 : 0;
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

    private static System.Windows.Controls.ScrollViewer? FeedScroller(System.Windows.DependencyObject parent) =>
        Find<System.Windows.Controls.ScrollViewer>(parent, scroll => scroll.TemplatedParent is System.Windows.Controls.ListBox);

    private static T? Find<T>(System.Windows.DependencyObject parent, Func<T, bool> wanted)
        where T : System.Windows.DependencyObject
    {
        for (var at = 0; at < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, at);
            if (child is T found && wanted(found))
            {
                return found;
            }

            if (Find(child, wanted) is { } deeper)
            {
                return deeper;
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
            state.StartsWith("questions", StringComparison.Ordinal) ? Questions(state == "questions-preview")
                : state == "feed" ? Feed(withCard: true)
                : state is "working" or "long-input" ? Feed(withCard: false)
                : state == "compacting" ? Compacting()
                : state.StartsWith("voice-", StringComparison.Ordinal) ? Feed(withCard: false) with { Busy = false }
                : null,
            now);
    }

    /// The worst case of a card of questions (AIKO-327): four questions of four options, long labels
    /// and descriptions. "questions" stands on the second question, which takes several answers;
    /// "questions-preview" on the last one, whose options have previews.
    private static TalkView Questions(bool preview)
    {
        const string Json = """
            {"questions":[
              {"question":"Where should the list of commands keep what was typed last?","header":"Storage","multiSelect":false,
               "options":[{"label":"In the memory of the window","description":"Gone when Aiko closes"},
                          {"label":"In the settings file","description":"Stays after a restart"}]},
              {"question":"Which commands should the menu of / show, and in which order should they go?","header":"What to show","multiSelect":true,
               "options":[{"label":"Built-in commands of Claude Code","description":"compact, model, clear and about fifty more, without the internal ones"},
                          {"label":"Skills: your own, the project's and the plugins' together in one list","description":"Skills of plugins carry the name of their plugin, your own ones carry nothing"},
                          {"label":"Commands from .claude/commands","description":"Old command files that Claude Code still understands"},
                          {"label":"Recent first","description":"What you used last goes to the top"}]},
              {"question":"What does Tab do in the menu?","header":"Tab","multiSelect":false,
               "options":[{"label":"Puts the command into the field","description":""},{"label":"Moves to the next one","description":""}]},
              {"question":"How should the menu show the letters that match?","header":"Match","multiSelect":false,
               "options":[{"label":"Brighter","description":"Matched letters white, the rest grey","preview":"/compact   Free up context by summarizing\n/config    Open config panel\n/aiko:copy (aiko) Write and edit text"},
                          {"label":"Underlined","description":"A thin line under the matched letters","preview":"/compact\n/config"},
                          {"label":"Not at all","description":""},
                          {"label":"Bold","description":"Heavier letters where they match"}]}
            ]}
            """;
        using var input = System.Text.Json.JsonDocument.Parse(Json);
        var card = RequestCard.From(new StreamEvent.Request("q1", "AskUserQuestion", "tq", input.RootElement.Clone(), "", true, false));

        var flow = QuestionFlow.Start(card.Questions).Click(0).Flow;
        flow = preview
            ? flow.Click(0).Flow.Click(1).Flow.Forward().Flow.Click(0).Flow
            : flow.Click(0).Flow.Click(1).Flow.Press(QuestionKey.Next).Flow;
        QuestionCardView.Preset(card, flow);

        List<FeedItem> items =
        [
            new FeedItem.OwnerMessage(1, "Draw the menu of / in the terminal.", false, []),
            new FeedItem.Card(2, card, RequestState.Open),
        ];
        return new TalkView("a2", items, Busy: true, HasOpenCard: true, Compacting: false, PermissionMode.Manual, ModelName.Label(null, "claude-opus-5-5"), "Aiko");
    }

    /// A small picture the first message was sent with: a checkerboard of 32 by 32 pixels.
    private const string Picture = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAO0lEQVR42mOQ07TGiqybvmFFpKpnGLVg1IIhYAG1DMKlftSCUQuGggWjRcWoBaMWjNYHoxaMWiCnaQ0AjX5qPQtpTRsAAAAASUVORK5CYII=";

    /// The same conversation made shorter once and now made shorter again (D-325).
    private static TalkView Compacting()
    {
        var feed = Feed(withCard: false);
        return feed with
        {
            // The core puts the notice above the queued message, the last item.
            Items = [.. feed.Items.SkipLast(1), new FeedItem.Notice(30, NoticeKind.Compacted, "", new CompactSize(46425, 6302)), feed.Items[^1]],
            Compacting = true,
        };
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

        // A skill and two MCP tools, named the way the core names real calls (D-324).
        static FeedItem.Step Tool(int id, string tool, string json)
        {
            using var call = System.Text.Json.JsonDocument.Parse(json);
            return new(id, $"t{id}", StepLine.For(tool, call.RootElement), StepState.Done, "", [], "");
        }

        List<FeedItem> items =
        [
            new FeedItem.OwnerMessage(1, "Add the folder groups to the form of a new session.", false, [new AttachedImage("image/png", Picture)]),
            Step(2, StepKind.Read, "NewSessionChoices.cs", StepState.Done),
            Step(3, StepKind.Search, "FolderSource", StepState.Done),
            new FeedItem.Answer(4, string.Join("\n",
                "## Where the groups come from",
                "",
                "The groups come from the core: **`NewSessionChoices.Headers`** says where each one starts. See [the form](src/Aiko.App/Terminal/TerminalPanel.cs:502).",
                "",
                "9. A header before the first *recent* folder.",
                "10. A header before the first bound folder.",
                "11. No header when a group is empty.",
                "",
                "```csharp",
                "var headers = NewSessionChoices.Headers(form.Folders);",
                "```",
                "",
                "| Group | Starts at |",
                "|---|---:|",
                "| Recent | 0 |",
                "| Bound | 3 |"), false),
            Tool(21, "Skill", "{\"skill\":\"aiko:copy\"}"),
            Tool(22, "mcp__plugin_aiko-persona_memory__remember", "{}"),
            Tool(23, "mcp__claude_ai_Google_Calendar__list_events", "{}"),
            Step(5, StepKind.Edit, "DropDown.cs", StepState.Done, 18, 2),
            withCard ? new FeedItem.Card(6, RequestCard.From(edit), RequestState.Open) : Step(6, StepKind.Run, "dotnet test Aiko.Core.Tests", StepState.Running),
            new FeedItem.OwnerMessage(7, "And the same on the Mac, please.", true, []),
        ];

        return new TalkView("a2", items, Busy: true, HasOpenCard: withCard, Compacting: false, PermissionMode.Manual, ModelName.Label(null, "claude-opus-5-5"), "Aiko");
    }
}
