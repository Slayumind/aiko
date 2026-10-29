using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The pill of one environment in the title bar (D-376): its ring and percentage are the limit that
/// is closer to its end (D-355), null when nothing is known yet.
sealed record TabView(string Name, bool Selected, int Waiting, int? Percent, LimitTone Tone);

/// One column of the open header. The active one carries the words of the card (Block) and the
/// profile line; a quiet one only shows its two numbers.
sealed record HeaderColumnView(HeaderColumn Column, EnvironmentBlock? Block, string Folder, bool Direct);

/// The header under the title bar (D-373, D-376): open or closed, and its columns, the active
/// environment first. ListHidden is the session list hidden with its icon or Ctrl+B.
sealed record HeaderView(bool Open, bool ListHidden, IReadOnlyList<HeaderColumnView> Columns);

/// A question in place of the terminal: stop something that still runs? Stop and Keep are the words
/// of its two buttons.
sealed record AskView(string Title, string Body, string Stop, string Keep);

/// Everything the window of the terminal draws at one moment. The panel only draws it; what is
/// in it is decided by the core and put together by the host. Terminal is the real terminal of
/// the chosen session (D-349), started when the session was first opened.
sealed record TerminalView(
    IReadOnlyList<TabView> Tabs,
    HeaderView Header,
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
