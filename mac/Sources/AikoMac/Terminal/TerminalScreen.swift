import AikoKit
import Foundation

/// The tab of one environment in the title bar.
struct TerminalTab: Equatable {
    let name: String
    let selected: Bool
    let waiting: Int
}

/// The one percentage in the title bar (D-296).
struct TerminalBadge: Equatable {
    let percent: Int
    let tone: LimitTone
    let kind: LimitKind
}

/// What the form of a new session offers and what is chosen in it.
struct SessionForm: Equatable {
    let environment: String
    let folders: [FolderChoice]
    let folder: Int
    let boundElsewhere: String?
    let model: Int
    let defaultName: String
}

/// A question over the conversation: close something that still works?
struct CloseAsk: Equatable {
    let title: String
    let body: String
}

/// Everything the window of the terminal draws at one moment. The panel only draws it; what is
/// in it is decided by the core and put together by the host.
///
/// The twin of Terminal/TerminalView.cs. The names differ where SwiftUI already has them:
/// TabView is TerminalTab here, FormView is SessionForm, AskView is CloseAsk, and the view
/// itself is TerminalScreen.
struct TerminalScreen: Equatable {
    let tabs: [TerminalTab]
    let badge: TerminalBadge?
    let groups: [RowGroup]
    let environment: String
    let sessionId: String?
    let sessionName: String?
    let main: MainView
    let signedOut: Bool
    let form: SessionForm?
    let ask: CloseAsk?
    let talk: TalkScreen?
    let now: Date
}
