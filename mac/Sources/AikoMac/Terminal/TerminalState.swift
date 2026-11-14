import AikoKit
import Combine
import Foundation

/// What the terminal window shows, for its SwiftUI parts: the environments, the rows of the list
/// and which session is chosen. TerminalHost changes it; the views only read it and call back.
@MainActor
final class TerminalState: ObservableObject {
    @Published var environments: [String] = []
    @Published var environment = ""
    @Published var groups: [RowGroup] = []
    @Published var chosen: String?

    var onEnvironment: ((String) -> Void)?
    var onChoose: ((String) -> Void)?
    var onNew: ((String, SessionKind) -> Void)?
    var onNewInOtherFolder: (() -> Void)?
}
