using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The shared cases count the pictures of a message; these check that the pictures themselves
/// stay, so the feed can draw them after the message went.
public class ConversationPictureTests
{
    private static readonly AttachedImage Screenshot = new("image/png", "iVBORw0KGgo=");

    [Fact]
    public void A_sent_message_keeps_its_pictures()
    {
        var outcome = Conversation.Empty().Send("look", [Screenshot]);

        var message = Assert.IsType<FeedItem.OwnerMessage>(outcome.State.Items[^1]);
        Assert.False(message.Queued);
        Assert.Equal([Screenshot], message.Attachments);
    }

    [Fact]
    public void A_message_from_the_file_of_the_conversation_keeps_its_pictures()
    {
        const string line = """
            {"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo="}},{"type":"text","text":"look"}]}}
            """;

        var state = Transcript.Read([line], PermissionMode.Manual);

        var message = Assert.IsType<FeedItem.OwnerMessage>(Assert.Single(state.Items));
        Assert.Equal([Screenshot], message.Attachments);
    }
}
