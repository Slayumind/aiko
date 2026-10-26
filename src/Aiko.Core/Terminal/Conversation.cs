namespace Aiko.Core.Terminal;

/// What a conversation is doing, for the list of sessions (D-293).
public enum TalkActivity
{
    /// Nothing runs and nothing is new.
    Idle,

    Working,

    /// A card waits for the person.
    Waiting,

    /// The work ended and the person has not looked yet.
    Done,

    /// Claude Code stopped in the middle of the work.
    Failed,

    SignedOut,
}

public enum TalkFailure
{
    None,
    SignedOut,
    Crashed,
}

public enum StepState
{
    Running,
    Done,
    Failed,
}

public enum RequestState
{
    Open,
    Allowed,
    Denied,
    Answered,

    /// The turn ended before the person chose.
    Closed,
}

/// The size of a conversation before and after it was made shorter, in tokens.
public sealed record CompactSize(int Before, int After)
{
    /// Both numbers are needed: an older Claude Code names only the size before.
    public static CompactSize? Of(StreamEvent.Compacted compacted) =>
        compacted.Before > 0 && compacted.After > 0 ? new(compacted.Before, compacted.After) : null;

    /// Tokens in thousands, as the notice shows them: at least 1.
    public static int Thousands(int tokens) => Math.Max(1, (int)Math.Round(tokens / 1000.0, MidpointRounding.AwayFromZero));
}

public enum NoticeKind
{
    /// The conversation was made shorter.
    Compacted,

    /// The person stopped the turn.
    Interrupted,

    /// The turn or Claude Code itself failed. The text says how, when Claude Code said it.
    Failed,

    SignedOut,

    /// The output of a command that ran without the model.
    Output,
}

/// One thing in the feed of a conversation.
public abstract record FeedItem(int Id)
{
    /// The pictures stay with the message after it is sent, so the feed shows what went.
    public sealed record OwnerMessage(int Id, string Text, bool Queued, IReadOnlyList<AttachedImage> Attachments)
        : FeedItem(Id);

    /// The markdown of an answer. While it is still being written, Streaming is set.
    public sealed record Answer(int Id, string Text, bool Streaming) : FeedItem(Id);

    /// Answers: what the person chose, when the step is a card of questions they answered.
    public sealed record Step(
        int Id,
        string ToolUseId,
        StepLine Line,
        StepState State,
        string Result,
        IReadOnlyList<StreamImage> Images,
        string Progress) : FeedItem(Id)
    {
        public IReadOnlyList<QuestionAnswer> Answers { get; init; } = [];
    }

    public sealed record Card(int Id, RequestCard Request, RequestState State) : FeedItem(Id);

    /// Size is set only for a conversation made shorter, when Claude Code named both sizes.
    public sealed record Notice(int Id, NoticeKind Kind, string Text, CompactSize? Size = null) : FeedItem(Id);
}

/// A conversation after one more event or one more action, and the lines to write to Claude Code.
public sealed record Outcome(Conversation State, IReadOnlyList<string> Written);

/// One conversation with Claude Code: its feed and what it is doing (D-294, D-304, D-305).
///
/// Nothing here starts a process or draws anything. Every event of the stream and every action of
/// the person gives a new conversation and the lines to write back; the window does the rest.
///
/// What a subagent says and does inside its own work is left out of the feed. Its permission
/// requests are not: they come as cards like any other.
public sealed record Conversation(
    IReadOnlyList<FeedItem> Items,
    bool InTurn,
    int Tasks,
    PermissionMode Mode,
    string Model,
    string SessionId,
    bool Unread,
    TalkFailure Failure,
    bool Compacting,
    IReadOnlyList<LimitWindow> Limits,
    int NextId,
    bool AnsweredInTurn,
    bool FailedInTurn,
    string AskedModel)
{
    public static Conversation Empty(PermissionMode mode = PermissionMode.Manual) =>
        new([], false, 0, mode, "", "", false, TalkFailure.None, false, [], 1, false, false, "");

    public TalkActivity Activity =>
        Failure == TalkFailure.SignedOut ? TalkActivity.SignedOut
        : Failure == TalkFailure.Crashed ? TalkActivity.Failed
        : HasOpenCard ? TalkActivity.Waiting
        : IsBusy ? TalkActivity.Working
        : Unread ? TalkActivity.Done
        : TalkActivity.Idle;

    /// A turn can end while a subagent still works in the background (D-305).
    public bool IsBusy => InTurn || Tasks > 0;

    /// The steps whose subagent still works. Such a step runs until its subagent is over, even
    /// when the tool already gave back that the subagent went to the background.
    public IReadOnlyList<string> Agents { get; init; } = [];

    /// The model asked last, or the one Claude Code named when nothing is asked.
    public string LatestModel => AskedModel.Length > 0 ? AskedModel : Model;

    public bool HasOpenCard => Items.Any(item => item is FeedItem.Card { State: RequestState.Open });

    // ---- what Claude Code says ----

    public Outcome On(StreamEvent streamEvent) => streamEvent switch
    {
        StreamEvent.Ready ready => Quiet(this with
        {
            SessionId = ready.SessionId,
            Model = ready.Model.Length > 0 ? ready.Model : Model,
            AskedModel = ready.Model.Length > 0 ? "" : AskedModel,
            Mode = ready.Mode ?? Mode,
        }),
        StreamEvent.TextDelta { ParentToolUseId: null } delta => Quiet(Begun().Streamed(delta.Text)),
        StreamEvent.Assistant { ParentToolUseId: null } assistant => Quiet(Begun().Said(assistant)),
        StreamEvent.User { ParentToolUseId: null } user => Quiet(Returned(user)),
        StreamEvent.Request request => Quiet(Add(id => new FeedItem.Card(id, RequestCard.From(request), RequestState.Open))),
        StreamEvent.Reply reply => Quiet(this with { Mode = reply.Mode ?? Mode }),
        StreamEvent.Status status => Quiet(this with { Mode = status.Mode ?? Mode, Compacting = status.Activity == "compacting" }),
        StreamEvent.Compacted compacted => Quiet(Add(id => new FeedItem.Notice(id, NoticeKind.Compacted, "", CompactSize.Of(compacted)))),
        StreamEvent.RateLimit limit => Quiet(this with { Limits = limit.Windows }),
        StreamEvent.Tasks tasks => Quiet(this with { Tasks = tasks.Count }),
        StreamEvent.TaskProgress progress => Quiet(Changed<FeedItem.Step>(
            step => step.ToolUseId == progress.ToolUseId,
            step => step with { Progress = progress.Description })),
        StreamEvent.TaskStarted started => Quiet(this with { Agents = [.. Agents.Where(id => id != started.ToolUseId), started.ToolUseId] }),
        StreamEvent.TaskEnded ended => Quiet(AgentEnded(ended)),
        StreamEvent.Result result => Ended(result),
        _ => Quiet(this),
    };

    /// Claude Code also starts a turn on its own, with no message of the person: when a subagent
    /// in the background is over, Claude reads what it did and answers. That is work too.
    private Conversation Begun() => InTurn ? this : this with { InTurn = true };

    private Conversation AgentEnded(StreamEvent.TaskEnded ended) =>
        (this with { Agents = [.. Agents.Where(id => id != ended.ToolUseId)] }).Changed<FeedItem.Step>(
            step => step.ToolUseId == ended.ToolUseId && step.State == StepState.Running,
            step => step with { State = ended.How == "completed" ? StepState.Done : StepState.Failed });

    private Conversation Streamed(string text) =>
        Writing() is { } writing
            ? Changed<FeedItem.Answer>(answer => answer.Id == writing.Id, answer => answer with { Text = answer.Text + text })
            : Add(id => new FeedItem.Answer(id, text, true));

    private Conversation Said(StreamEvent.Assistant assistant)
    {
        if (assistant.Error is { } error)
        {
            // Claude Code answered in the place of Claude. Its words are a notice, not an answer.
            var words = string.Join('\n', assistant.Blocks.OfType<AssistantBlock.Text>().Select(text => text.Value));
            return error == "authentication_failed"
                ? Add(id => new FeedItem.Notice(id, NoticeKind.SignedOut, "")) with { Failure = TalkFailure.SignedOut }
                : Add(id => new FeedItem.Notice(id, NoticeKind.Failed, words)) with { FailedInTurn = true };
        }

        var next = this;
        foreach (var block in assistant.Blocks)
        {
            switch (block)
            {
                case AssistantBlock.Text text:
                    next = next.Answered(text.Value);
                    break;
                // The step of a card of questions stays in the feed, hidden: its answers come to it
                // with its result, and the feed shows them after the card is gone.
                case AssistantBlock.ToolUse tool when StepLine.For(tool.Name, tool.Input) is var line
                    && (!line.Hidden || tool.Name == QuestionAnswers.Tool):
                    next = next.Add(id => new FeedItem.Step(id, tool.Id, line, StepState.Running, "", [], ""));
                    break;
            }
        }
        return next;
    }

    /// The whole text of an answer takes the place of the pieces that came before it.
    private Conversation Answered(string text)
    {
        var next = Writing() is { } writing
            ? Changed<FeedItem.Answer>(answer => answer.Id == writing.Id, answer => answer with { Text = text, Streaming = false })
            : Add(id => new FeedItem.Answer(id, text, false));
        return next with { AnsweredInTurn = true };
    }

    private Conversation Returned(StreamEvent.User user)
    {
        // After a change of model Claude Code names its full id at once, before the next turn.
        var next = ModelName.FromSwitch(user.Text) is { } model ? this with { Model = model, AskedModel = "" } : this;
        foreach (var result in user.Results)
        {
            next = next.Changed<FeedItem.Step>(
                step => step.ToolUseId == result.ToolUseId,
                step => step with
                {
                    State = result.IsError ? StepState.Failed
                        : Agents.Contains(step.ToolUseId) ? StepState.Running
                        : StepState.Done,
                    Result = result.Text,
                    Images = result.Images,
                    Answers = result.Answers,
                });
        }
        return next;
    }

    private Outcome Ended(StreamEvent.Result result)
    {
        var next = Settled(result.IsError ? StepState.Failed : StepState.Done);

        // When Claude Code already said in this turn what went wrong, the feed has that notice.
        var stopped = result.TerminalReason.StartsWith("aborted", StringComparison.Ordinal);
        if (result.IsError && stopped)
        {
            next = next.Add(id => new FeedItem.Notice(id, NoticeKind.Interrupted, ""));
        }
        else if (result.IsError && !FailedInTurn && Failure != TalkFailure.SignedOut)
        {
            next = next.Add(id => new FeedItem.Notice(id, NoticeKind.Failed, result.Text));
        }
        else if (!result.IsError && !AnsweredInTurn && result.Text.Length > 0)
        {
            next = next.Add(id => new FeedItem.Notice(id, NoticeKind.Output, result.Text));
        }

        next = next with { InTurn = false, AnsweredInTurn = false, FailedInTurn = false, Unread = true, Compacting = false };

        // The queue is kept here, not in Claude Code: the next message goes when the turn is over (D-304).
        return next.Items.OfType<FeedItem.OwnerMessage>().FirstOrDefault(message => message.Queued) is { } queued
            ? next.Sent(queued)
            : Quiet(next);
    }

    // ---- what the person does ----

    /// A message sent while Claude works, or while a card waits, goes into the queue.
    public Outcome Send(string text, IReadOnlyList<AttachedImage> images)
    {
        if (text.Trim().Length == 0 && images.Count == 0)
        {
            return Quiet(this);
        }

        var waits = InTurn || HasOpenCard;
        var next = (this with { Failure = TalkFailure.None, Unread = false })
            .Queue(new FeedItem.OwnerMessage(NextId, text, true, images));

        return waits ? Quiet(next) : next.Sent((FeedItem.OwnerMessage)next.Items[^1]);
    }

    public Outcome Choose(string requestId, CardChoice choice)
    {
        if (Items.OfType<FeedItem.Card>().FirstOrDefault(card => card.State == RequestState.Open && card.Request.RequestId == requestId)
            is not { } open)
        {
            return Quiet(this);
        }

        var state = choice switch
        {
            CardChoice.Deny => RequestState.Denied,
            CardChoice.Answers => RequestState.Answered,
            _ => RequestState.Allowed,
        };

        return new Outcome(
            Changed<FeedItem.Card>(card => card.Id == open.Id, card => card with { State = state }),
            [open.Request.LineFor(choice)]);
    }

    public Outcome Interrupt() =>
        IsBusy
            ? new Outcome(this with { NextId = NextId + 1 }, [HostLine.Interrupt($"int-{NextId}")])
            : Quiet(this);

    public Outcome SetMode(PermissionMode mode) =>
        new(this with { Mode = mode, NextId = NextId + 1 }, [HostLine.SetMode($"mode-{NextId}", mode)]);

    /// The model stays asked until Claude Code names its full id: in its answer to the change,
    /// or in the init of the next start.
    public Outcome SetModel(string model) =>
        new(this with { AskedModel = model, NextId = NextId + 1 }, [HostLine.SetModel($"model-{NextId}", model)]);

    public Conversation MarkRead() => this with { Unread = false };

    /// Claude Code is gone. If it was in the middle of something, that is a failure to show.
    public Conversation ProcessExited()
    {
        if (!IsBusy && !HasOpenCard)
        {
            return this;
        }

        var next = (this with { Agents = [] }).Settled(StepState.Failed).Add(id => new FeedItem.Notice(id, NoticeKind.Failed, ""));
        return next with
        {
            InTurn = false,
            Tasks = 0,
            AnsweredInTurn = false,
            FailedInTurn = false,
            Compacting = false,
            Failure = Failure == TalkFailure.SignedOut ? Failure : TalkFailure.Crashed,
        };
    }

    // ---- small steps ----

    private Outcome Sent(FeedItem.OwnerMessage message)
    {
        // The message moves to the end of the feed: the answer to it comes right under it, not
        // under whatever was said while it waited.
        var sent = message with { Queued = false };
        var next = this with { Items = [.. Items.Where(item => item.Id != message.Id), sent], InTurn = true };

        return new Outcome(next, [HostLine.UserMessage(message.Text, message.Attachments)]);
    }

    /// Nothing is left half done: an answer is whole, a step has ended, a card nobody answered is
    /// closed. Only the step of a subagent that still works goes on.
    private Conversation Settled(StepState running) => this with
    {
        Items = [.. Items.Select(item => item switch
        {
            FeedItem.Answer { Streaming: true } answer => answer with { Streaming = false },
            FeedItem.Step { State: StepState.Running } step when !Agents.Contains(step.ToolUseId) => step with { State = running },
            FeedItem.Card { State: RequestState.Open } card => card with { State = RequestState.Closed },
            _ => item,
        })],
    };

    /// What Claude Code says goes above the messages that wait in the queue, so they stay at the
    /// end of the feed and an answer is not cut in two (D-304).
    private Conversation Add(Func<int, FeedItem> item)
    {
        var at = Items.Count - QueuedAtEnd();
        return this with { Items = [.. Items.Take(at), item(NextId), .. Items.Skip(at)], NextId = NextId + 1 };
    }

    private Conversation Queue(FeedItem.OwnerMessage message) =>
        this with { Items = [.. Items, message], NextId = NextId + 1 };

    /// The answer Claude is writing now: the last item above the queued messages.
    private FeedItem.Answer? Writing()
    {
        var at = Items.Count - QueuedAtEnd() - 1;
        return at >= 0 && Items[at] is FeedItem.Answer { Streaming: true } writing ? writing : null;
    }

    private int QueuedAtEnd() =>
        Items.Reverse().TakeWhile(item => item is FeedItem.OwnerMessage { Queued: true }).Count();

    private Conversation Changed<T>(Func<T, bool> which, Func<T, T> change)
        where T : FeedItem =>
        this with { Items = [.. Items.Select(item => item is T found && which(found) ? change(found) : item)] };

    private static Outcome Quiet(Conversation state) => new(state, []);
}
