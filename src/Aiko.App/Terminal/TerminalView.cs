using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The tab of one environment in the title bar.
sealed record TabView(string Name, bool Selected, int Waiting);

/// The one percentage in the title bar (D-296).
sealed record BadgeView(int Percent, LimitTone Tone, LimitKind Kind);

/// A new session before its first message (D-342): what it offers and what is chosen. There is no
/// form any more: the session shows as an empty conversation, and these choices sit in the line
/// under its field. DefaultName is the name when the first message has no words.
sealed record FormView(
    string Environment,
    IReadOnlyList<FolderChoice> Folders,
    int Folder,
    string? BoundElsewhere,
    int Model,
    string DefaultName)
{
    /// The models to choose from; Model is a place in this list.
    public IReadOnlyList<ClaudeModel> Models { get; init; } = NewSessionChoices.Offered([]);

    /// The mode the session starts in: the one the folder's settings name, until changed.
    public PermissionMode Mode { get; init; } = PermissionMode.Manual;

    /// The commands of "/" in the environment, so the menu works before the first message.
    public IReadOnlyList<SlashCommand> Commands { get; init; } = [];
}

/// A question over the conversation: close something that still works?
sealed record AskView(string Title, string Body);

/// Everything the window of the terminal draws at one moment. The panel only draws it; what is
/// in it is decided by the core and put together by the host.
sealed record TerminalView(
    IReadOnlyList<TabView> Tabs,
    BadgeView? Badge,
    IReadOnlyList<RowGroup> Groups,
    string Environment,
    string? SessionId,
    string? SessionName,
    MainView Main,
    bool SignedOut,
    FormView? Form,
    AskView? Ask,
    TalkView? Talk,
    DateTimeOffset Now);
