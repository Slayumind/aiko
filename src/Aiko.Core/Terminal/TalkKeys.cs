namespace Aiko.Core.Terminal;

/// The two keys of the field that mean more than a letter. Shift+Enter is a new line and never
/// comes here.
public enum TalkKey
{
    Enter,
    Escape,
}

/// What a key does. None leaves the key to the field.
public enum TalkKeyAction
{
    None,

    /// The key is taken and nothing happens.
    Swallow,
    Send,
    Allow,
    Deny,
    Stop,
    CancelVoice,
}

/// What the field knows when a key comes. CardWaits: a card the keys answer is open. Empty: no
/// words and no pictures. Dictating: the words of a recording are in the field. VoiceActive: a
/// recording warms up, listens or finishes.
public sealed record TalkKeyState(bool CardWaits, bool Empty, bool Busy, bool Dictating, bool VoiceActive);

/// Enter and Esc in the field of a conversation (D-323). The keyboard stays in the field, and a
/// card of permission or of a plan is answered from there: Enter in an empty field allows, Esc
/// denies. Enter with words in the field does nothing while a card waits, so a message is not
/// sent by mistake and the card is not allowed by mistake.
public static class TalkKeys
{
    public static TalkKeyAction For(TalkKey key, TalkKeyState state) => key switch
    {
        // The text of a recording is never sent by itself (D-313), not even half way.
        TalkKey.Enter when state.Dictating => TalkKeyAction.Swallow,
        TalkKey.Enter when state.CardWaits => state.Empty ? TalkKeyAction.Allow : TalkKeyAction.Swallow,
        TalkKey.Enter => TalkKeyAction.Send,
        TalkKey.Escape when state.VoiceActive => TalkKeyAction.CancelVoice,
        TalkKey.Escape when state.CardWaits => TalkKeyAction.Deny,
        TalkKey.Escape when state.Busy => TalkKeyAction.Stop,
        _ => TalkKeyAction.None,
    };

    /// The card the keys answer: the oldest open card, when it asks for a permission or a plan.
    /// A card of questions has its own keys (AIKO-327), so with one open the keys answer nothing.
    public static FeedItem.Card? Card(IEnumerable<FeedItem> items) =>
        items.OfType<FeedItem.Card>().FirstOrDefault(card => card.State == RequestState.Open) is { } card
        && card.Request.Kind != CardKind.Question
            ? card
            : null;
}
