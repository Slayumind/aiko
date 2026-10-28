using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The tab of one environment in the title bar.
sealed record TabView(string Name, bool Selected, int Waiting);

/// The one percentage in the title bar (D-296).
sealed record BadgeView(int Percent, LimitTone Tone, LimitKind Kind);

/// A question in place of the terminal: stop something that still runs? Stop and Keep are the words
/// of its two buttons.
sealed record AskView(string Title, string Body, string Stop, string Keep);

/// Everything the window of the terminal draws at one moment. The panel only draws it; what is
/// in it is decided by the core and put together by the host. Terminal is the real terminal of
/// the chosen session (D-349), started when the session was first opened.
sealed record TerminalView(
    IReadOnlyList<TabView> Tabs,
    BadgeView? Badge,
    IReadOnlyList<RowGroup> Groups,
    string Environment,
    string? SessionId,
    string? SessionName,
    MainView Main,
    AskView? Ask,
    TerminalSurface? Terminal,
    DateTimeOffset Now)
{
    /// Claude Code of the chosen session ended by itself: Enter starts it again (D-351).
    public bool Stopped { get; init; }
}
