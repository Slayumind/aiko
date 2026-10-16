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

    public sealed record Step(
        int Id,
        string ToolUseId,
        StepLine Line,
        StepState State,
        string Result,
        IReadOnlyList<StreamImage> Images,
        string Progress) : FeedItem(Id);

    public sealed record Card(int Id, RequestCard Request, RequestState State) : FeedItem(Id);

    public sealed record Notice(int Id, NoticeKind Kind, string Text) : FeedItem(Id);
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
        StreamEvent.TextDelta { ParentToolUseId: null } delta => Quiet(Streamed(delta.Text)),
        StreamEvent.Assistant { ParentToolUseId: null } assistant => Quiet(Said(assistant)),
        StreamEvent.User { ParentToolUseId: null } user => Quiet(Returned(user)),
        StreamEvent.Request request => Quiet(Add(id => new FeedItem.Card(id, RequestCard.From(request), RequestState.Open))),
        StreamEvent.Reply reply => Quiet(this with { Mode = reply.Mode ?? Mode }),
        StreamEvent.Status status => Quiet(this with { Mode = status.Mode ?? Mode, Compacting = status.Activity == "compacting" }),
        StreamEvent.Compacted => Quiet(Add(id => new FeedItem.Notice(id, NoticeKind.Compacted, ""))),
        StreamEvent.RateLimit limit => Quiet(this with { Limits = limit.Windows }),
        StreamEvent.Tasks tasks => Quiet(this with { Tasks = tasks.Count }),
        StreamEvent.TaskProgress progress => Quiet(Changed<FeedItem.Step>(
            step => step.ToolUseId == progress.ToolUseId,
            step => step with { Progress = progress.Description })),
        StreamEvent.Result result => Ended(result),
        _ => Quiet(this),
    };

    private Conversation Streamed(string text) =>
        Items.Count > 0 && Items[^1] is FeedItem.Answer { Streaming: true } writing
            ? this with { Items = [.. Items.Take(Items.Count - 1), writing with { Text = writing.Text + text }] }
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
                case AssistantBlock.ToolUse tool when StepLine.For(tool.Name, tool.Input) is { Hidden: false } line:
                    next = next.Add(id => new FeedItem.Step(id, tool.Id, line, StepState.Running, "", [], ""));
                    break;
            }
        }
        return next;
    }

    /// The whole text of an answer takes the place of the pieces that came before it.
    private Conversation Answered(string text)
    {
        var next = Items.Count > 0 && Items[^1] is FeedItem.Answer { Streaming: true } writing
            ? this with { Items = [.. Items.Take(Items.Count - 1), writing with { Text = text, Streaming = false }] }
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
                    State = result.IsError ? StepState.Failed : StepState.Done,
                    Result = result.Text,
                    Images = result.Images,
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
            .Add(id => new FeedItem.OwnerMessage(id, text, true, images));

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

        var next = Settled(StepState.Failed).Add(id => new FeedItem.Notice(id, NoticeKind.Failed, ""));
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

    /// Nothing is left half done: an answer is whole, a step has ended, a card nobody answered is closed.
    private Conversation Settled(StepState running) => this with
    {
        Items = [.. Items.Select(item => item switch
        {
            FeedItem.Answer { Streaming: true } answer => answer with { Streaming = false },
            FeedItem.Step { State: StepState.Running } step => step with { State = running },
            FeedItem.Card { State: RequestState.Open } card => card with { State = RequestState.Closed },
            _ => item,
        })],
    };

    private Conversation Add(Func<int, FeedItem> item) =>
        this with { Items = [.. Items, item(NextId)], NextId = NextId + 1 };

    private Conversation Changed<T>(Func<T, bool> which, Func<T, T> change)
        where T : FeedItem =>
        this with { Items = [.. Items.Select(item => item is T found && which(found) ? change(found) : item)] };

    private static Outcome Quiet(Conversation state) => new(state, []);
}
