using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The tab of one environment in the title bar.
sealed record TabView(string Name, bool Selected, int Waiting);

/// The one percentage in the title bar (D-296).
sealed record BadgeView(int Percent, LimitTone Tone, LimitKind Kind);

/// What the form of a new session offers and what is chosen in it.
sealed record FormView(
    string Environment,
    IReadOnlyList<FolderChoice> Folders,
    int Folder,
    string? BoundElsewhere,
    int Model,
    string DefaultName);

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
