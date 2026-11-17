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
