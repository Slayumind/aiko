import Foundation

/// Where a conversation of Claude Code was started, as its file says.
public enum ConversationSource: String, Sendable, Equatable, CaseIterable {
    /// A terminal: Aiko, Warp, Terminal or another one.
    case terminal
    case vsCode
    case desktop
    case other
}

/// A conversation of Claude Code on disk (D-353): what the palette needs to find it and to open it
/// again in Aiko. Name is the title Claude Code or the person gave it, or the start of the first
/// message; firstMessage is that message on one line.
///
/// The twin of Conversation in ConversationFile.cs.
public struct Conversation: Sendable, Equatable {
    public let id: String
    public let environment: String
    public let folder: String
    public let name: String
    public let firstMessage: String
    public let lastUsed: Int64
    public let source: ConversationSource

    public init(
        id: String, environment: String, folder: String, name: String, firstMessage: String, lastUsed: Int64,
        source: ConversationSource
    ) {
        self.id = id
        self.environment = environment
        self.folder = folder
        self.name = name
        self.firstMessage = firstMessage
        self.lastUsed = lastUsed
        self.source = source
    }
}

/// Reads a conversation from the start and the end of its file. The file can be hundreds of
/// megabytes, so only its first and last pieces are read (spike 2026-10: 64 KB and 32 KB). A line cut
/// at the edge of a piece is skipped.
///
/// The twin of ConversationFile.cs.
public enum ConversationFile {
    /// How much of the start and of the end of a file is read.
    public static let headBytes = 64 * 1024
    public static let tailBytes = 32 * 1024

    /// Nil for a conversation of a program (claude -p, the SDK) and for one with no message of the
    /// person and no title: there is nothing to find it by.
    public static func read(id: String, environment: String, head: String, tail: String, lastUsed: Int64) -> Conversation? {
        var folder = ""
        var entrypoint = ""
        var message = ""
        for line in head.split(separator: "\n", omittingEmptySubsequences: false) {
            guard let root = parse(String(line)) else { continue }

            if folder.isEmpty, let cwd = root["cwd"]?.stringValue, !cwd.isEmpty {
                folder = cwd
            }
            if entrypoint.isEmpty, let entry = root["entrypoint"]?.stringValue, !entry.isEmpty {
                entrypoint = entry
            }
            if message.isEmpty, root["type"]?.stringValue == "user", root["isMeta"]?.boolValue != true,
               let text = userText(root), !text.isEmpty {
                message = TextMatch.oneLine(text)
            }
        }

        if entrypoint.hasPrefix("sdk") {
            return nil
        }

        // The last title wins; a title the person gave wins over the one Claude Code made.
        var aiTitle: String?
        var customTitle: String?
        for line in tail.split(separator: "\n", omittingEmptySubsequences: false) {
            guard let root = parse(String(line)) else { continue }
            switch root["type"]?.stringValue {
            case "ai-title":
                if let title = root["aiTitle"]?.stringValue, !title.isEmpty { aiTitle = title }
            case "custom-title":
                if let title = root["customTitle"]?.stringValue, !title.isEmpty { customTitle = title }
            default:
                break
            }
        }

        let given = SessionName.clean(customTitle ?? aiTitle)
        if given.isEmpty && message.isEmpty {
            return nil
        }

        let place = SessionName.folderPath(folder)
        let name = given.isEmpty ? SessionName.fromMessage(message, fallback: SessionName.folderName(place)) : given
        return Conversation(
            id: id, environment: environment, folder: place, name: name, firstMessage: message, lastUsed: lastUsed,
            source: sourceOf(entrypoint))
    }

    private static func sourceOf(_ entrypoint: String) -> ConversationSource {
        switch entrypoint {
        case "cli", "": return .terminal
        case "claude-vscode": return .vsCode
        case "claude-desktop": return .desktop
        default: return .other
        }
    }

    /// The words of a message of the person. A command, its output and the notes Claude Code adds
    /// come in tags and are not what the person wrote.
    private static func userText(_ root: JsonObject) -> String? {
        guard let content = root["message"]?.objectValue?["content"] else {
            return nil
        }

        let text: String?
        switch content {
        case .string(let value):
            text = value
        case .array(let parts):
            text = parts
                .compactMap { $0.objectValue }
                .filter { $0["type"]?.stringValue == "text" }
                .compactMap { $0["text"]?.stringValue }
                .first { !$0.isEmpty }
        default:
            text = nil
        }

        let trimmed = (text ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.hasPrefix("<") ? nil : trimmed
    }

    private static func parse(_ line: String) -> JsonObject? {
        let text = line.trimmingCharacters(in: .whitespacesAndNewlines)
        guard text.hasPrefix("{"), text.hasSuffix("}") else {
            return nil
        }
        return JsonNode.parse(text)?.objectValue
    }
}
