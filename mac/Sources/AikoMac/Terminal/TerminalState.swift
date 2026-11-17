import AikoKit
import Combine
import Foundation

/// What the terminal window shows, for its SwiftUI parts: the pills of the environments, the rows of
/// the list and which session is chosen. TerminalHost changes it; the views only read it and call back.
@MainActor
final class TerminalState: ObservableObject {
    @Published var environments: [String] = []
    @Published var environment = ""
    @Published var tabs: [TabItem] = []
    @Published var header: [HeaderColumnItem] = []
    @Published var groups: [RowGroup] = []
    @Published var chosen: String?
    @Published var headerOpen = false
    @Published var listHidden = false
    /// The window is narrower than WindowPlace.narrowBelow: the list opens under its button, over the
    /// terminal, and the pills not chosen keep only their mark (D-376).
    @Published var narrow = false
    /// The list of a narrow window is open under its button.
    @Published var listPopOpen = false
    /// A row being dragged (D-400): the list keeps its order until the copy has landed.
    var dragging: String?
    /// Where each row stands in the list, for a self test that drags one.
    var rowFramesForCheck: [String: CGRect] = [:]
    @Published var settingsOpen = false
    /// A quiet dot on the gear while a newer version is out (D-391).
    @Published var updateDot = false
    @Published var stage = StageMessage.none

    var onRetry: (() -> Void)?
    var onInstallPage: (() -> Void)?

    /// The answer to the question before closing: true stops the program.
    var onAnswer: ((Bool) -> Void)?

    var onEnvironment: ((String) -> Void)?
    var onChoose: ((String) -> Void)?
    /// A session moved in its folder, before the one named, or to the end of it (D-400).
    var onMove: ((String, String?) -> Void)?
    /// A drag ended where it began: the list follows the sessions again.
    var onDragEnded: (() -> Void)?
    var onNew: ((String, SessionKind) -> Void)?
    var onNewInOtherFolder: (() -> Void)?
    var onRename: ((String, String) -> Void)?
    var onClose: ((String) -> Void)?

    /// Opens the folder in Finder; false when the folder is gone from the disk.
    var onOpenFolder: ((String) -> Bool)?
    var onSignIn: ((String) -> Void)?
    var onToggleList: (() -> Void)?
    var onToggleHeader: (() -> Void)?
    var onSearch: (() -> Void)?
    var onSettings: (() -> Void)?
}
