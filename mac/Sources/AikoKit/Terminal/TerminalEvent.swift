import Foundation

/// What a session in the terminal did last, as its second line says it (D-352).
public enum TerminalEventKind: String, Sendable, Equatable, CaseIterable {
    /// A message of the person went to the model.
    case thinking
    case reading
    case editing
    case running
    case searching
    /// A page or a search on the web.
    case browsing
    /// An agent of its own was started.
    case delegating
    /// Any other tool. The detail is its name.
    case using
    /// A permission prompt: the detail is what is asked for.
    case asksPermission
    /// A question to the person.
    case asksQuestion
    case finished
    case failed
    case outOfLimit
}

/// The last hook event of a session of the terminal. The bridge writes it into the file of the
/// session; the window reads it. Detail is short and safe to show: the name of a file without its
/// folder, the start of a command, the name of a tool. The prompt and the answers are never read.
///
/// The twin of TerminalEvent.cs.
public struct TerminalEvent: Sendable, Equatable {
    /// The variable that tells the hook which session of the terminal it runs for (D-360).
    public static let sessionVariable = "AIKO_TERMINAL_SESSION"

    /// The argument the bridge answers to with this hook.
    public static let hookArgument = "terminal-event"

    /// The events the hook of the terminal is called for.
    public static let hookEvents = [
        "UserPromptSubmit", "PreToolUse", "PermissionRequest", "Notification", "Stop", "StopFailure",
    ]

    /// Characters of a detail at most; a longer one ends in an ellipsis.
    public static let detailLength = 40

    private static let questionNotifications: Set<String> = ["elicitation_dialog", "agent_needs_input"]

    public let kind: TerminalEventKind
    public let detail: String
    /// Milliseconds since 1970.
    public let at: Int64

    public init(kind: TerminalEventKind, detail: String, at: Int64) {
        self.kind = kind
        self.detail = detail
        self.at = at
    }

    public var isWaiting: Bool { kind == .asksPermission || kind == .asksQuestion }

    /// The turn is over.
    public var isEnd: Bool { kind == .finished || kind == .failed || kind == .outOfLimit }

    /// Whether this event goes into the file over the one written there. Async hooks run side by
    /// side, so the hook of the last tool can finish after Stop; a turn that ended stays
    /// ended until the person sends a message or Claude Code asks something.
    public func replaces(_ written: TerminalEvent?) -> Bool {
        guard let written, written.isEnd else { return true }
        return isEnd || isWaiting || kind == .thinking
    }

    /// The file of a session: its id and ".json". Nil for an id that is not one Aiko gives, so a
    /// variable from elsewhere can never point the bridge at another file.
    public static func fileName(_ sessionId: String?) -> String? {
        guard let id = sessionId, !id.isEmpty, id.utf16.count <= 64,
              id.unicodeScalars.allSatisfy({ $0.isASCII && (CharacterSet.alphanumerics.contains($0) || $0 == "-") })
        else {
            return nil
        }

        return id + ".json"
    }

    /// Nil for an event that changes nothing in the row, or for input that is not a hook call.
    /// now is in milliseconds since 1970.
    public static func fromHook(_ json: String?, now: Int64) -> TerminalEvent? {
        guard let root = parse(json) else {
            return nil
        }

        let tool = root["tool_name"]?.stringValue ?? ""
        let input = root["tool_input"]?.objectValue
        let found: (TerminalEventKind, String)?
        switch root["hook_event_name"]?.stringValue {
        case "UserPromptSubmit":
            found = (.thinking, "")
        case "PreToolUse":
            // Only the start of a tool: async hooks overtake each other, and the end of one tool
            // came seconds after the start of the next (a live check, D-366).
            found = toolEvent(tool, input)
        case "PermissionRequest":
            found = (.asksPermission, toolEvent(tool, input).1)
        case "Notification":
            // A permission prompt is told by PermissionRequest six seconds earlier (spike 2026-10),
            // and with the detail; the notification would only wipe it.
            found = questionNotifications.contains(root["notification_type"]?.stringValue ?? "") ? (.asksQuestion, "") : nil
        case "Stop":
            found = (.finished, "")
        case "StopFailure":
            found = (root["error_type"]?.stringValue == "rate_limit" ? .outOfLimit : .failed, "")
        default:
            found = nil
        }

        guard let (kind, detail) = found else {
            return nil
        }

        return TerminalEvent(kind: kind, detail: short(detail), at: now)
    }

    public func toJson() -> String {
        var root = JsonObject()
        root["kind"] = .string(kind.rawValue)
        root["detail"] = .string(detail)
        root["at"] = .number(String(at))
        return JsonNode.object(root).toJsonString(indented: false, escaping: .relaxed) + "\n"
    }

    /// Never throws: a broken or unknown file is no event.
    public static func fromJson(_ json: String?) -> TerminalEvent? {
        guard let root = parse(json),
              let name = root["kind"]?.stringValue,
              let kind = TerminalEventKind.allCases.first(where: { $0.rawValue.lowercased() == name.lowercased() }),
              let at = root["at"]?.int64Value
        else {
            return nil
        }

        return TerminalEvent(kind: kind, detail: short(root["detail"]?.stringValue ?? ""), at: at)
    }

    private static func toolEvent(_ tool: String, _ input: JsonObject?) -> (TerminalEventKind, String) {
        switch tool {
        case "Read", "NotebookRead":
            return (.reading, fileOf(input))
        case "Edit", "MultiEdit", "Write", "NotebookEdit":
            return (.editing, fileOf(input))
        case "Bash", "PowerShell":
            return (.running, firstLine(input?["command"]?.stringValue))
        case "Grep", "Glob":
            return (.searching, firstLine(input?["pattern"]?.stringValue))
        case "WebFetch":
            return (.browsing, host(input?["url"]?.stringValue))
        case "WebSearch":
            return (.browsing, firstLine(input?["query"]?.stringValue))
        case "Task", "Agent":
            return (.delegating, firstLine(input?["description"]?.stringValue))
        default:
            return (.using, toolName(tool))
        }
    }

    /// The name of the file only: a folder may hold names the person would not show.
    private static func fileOf(_ input: JsonObject?) -> String {
        let path = input?["file_path"]?.stringValue ?? input?["notebook_path"]?.stringValue ?? ""
        var cut = Substring(path)
        while let last = cut.last, last == "/" || last == "\\" {
            cut = cut.dropLast()
        }

        if let at = cut.lastIndex(where: { $0 == "/" || $0 == "\\" }) {
            return String(cut[cut.index(after: at)...])
        }
        return String(cut)
    }

    private static func host(_ url: String?) -> String {
        guard let url, let parsed = URL(string: url), parsed.scheme != nil else {
            return ""
        }
        return parsed.host ?? ""
    }

    /// A tool of a server is named mcp__server__tool: the tool part says the most.
    private static func toolName(_ tool: String) -> String {
        guard tool.hasPrefix("mcp__"), let at = tool.range(of: "__", options: .backwards),
              tool.distance(from: tool.startIndex, to: at.lowerBound) > 4
        else {
            return tool
        }
        return String(tool[at.upperBound...])
    }

    private static func firstLine(_ text: String?) -> String {
        let line = (text ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if let end = line.firstIndex(where: { $0 == "\r" || $0 == "\n" || $0 == "\r\n" }) {
            return String(line[..<end])
        }
        return line
    }

    /// One line, spaces squeezed, at most detailLength characters with the ellipsis.
    private static func short(_ text: String) -> String {
        var line = ""
        for c in text {
            if c.isWhitespace || c.unicodeScalars.allSatisfy({ $0.properties.generalCategory == .control }) {
                if let last = line.last, last != " " {
                    line.append(" ")
                }
            } else {
                line.append(c)
            }
        }

        while line.last == " " {
            line.removeLast()
        }
        guard line.count > detailLength else {
            return line
        }

        var cut = String(line.prefix(detailLength - 1))
        while cut.last?.isWhitespace == true {
            cut.removeLast()
        }
        return cut + "\u{2026}"
    }

    private static func parse(_ json: String?) -> JsonObject? {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return JsonNode.parse(json)?.objectValue
    }
}

/// The settings a session of the terminal starts with, through --settings: the hook of the
/// terminal for every event it needs. Claude Code adds these hooks to the ones of the person and of
/// the plugins, and settings.json is not touched. async keeps the session from waiting, and an
/// async hook decides no permission.
///
/// The twin of TerminalHooks in TerminalEvent.cs.
public enum TerminalHooks {
    public static func settingsJson(_ bridgeExePath: String) -> String {
        var events = JsonObject()
        for name in TerminalEvent.hookEvents {
            var hook = JsonObject()
            hook["type"] = .string("command")
            hook["command"] = .string(bridgeExePath)
            hook["args"] = .array([.string(TerminalEvent.hookArgument)])
            hook["async"] = .bool(true)

            var matcher = JsonObject()
            matcher["hooks"] = .array([.object(hook)])
            events[name] = .array([.object(matcher)])
        }

        var root = JsonObject()
        root["hooks"] = .object(events)
        return JsonNode.object(root).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }
}

/// What a running session of Claude Code is doing, from its title and its last hook event (D-352,
/// D-360). Times are in milliseconds. seenAt is when the person last looked at the session, and
/// answeredAt when the person last typed into it: a question that came before is answered.
///
/// After Esc on a permission prompt Claude Code calls no hook (RESEARCH), so "waiting" would stay
/// for ever. Typing into the session is what ends it: that is how the person answers.
///
/// The twin of TerminalPulse in TerminalEvent.cs.
public enum TerminalPulse {
    public static func of(_ title: ClaudeTitle?, _ last: TerminalEvent?, seenAt: Int64, answeredAt: Int64) -> TerminalActivity {
        if let last, last.isWaiting, last.at > answeredAt {
            return .waiting
        }

        if title?.working == true {
            return .working
        }

        if let last, last.isEnd, last.at > seenAt {
            return .done
        }
        return .idle
    }

    /// Whether input that went to the program was typed by the person. The terminal also sends
    /// sequences of its own: focus in and out, answers about the cursor. Those are control
    /// sequences (ESC [ ... final) and say nothing. Arrows are such sequences too, but a prompt is
    /// answered with Enter, a digit or Esc, and those count.
    public static func isTyped(_ input: String) -> Bool {
        let units = Array(input.utf16)
        var at = 0
        while at < units.count {
            defer { at += 1 }
            if units[at] != 0x1B || at + 1 >= units.count {
                return true
            }

            if units[at + 1] == 0x4F, at + 2 < units.count {
                at += 2
                continue
            }

            if units[at + 1] != 0x5B {
                return true
            }

            // Parameters and intermediates, then the final character.
            var end = at + 2
            while end < units.count, (0x20...0x3F).contains(units[end]) {
                end += 1
            }

            if end >= units.count || !(0x40...0x7E).contains(units[end]) {
                return true
            }

            at = end
        }

        return false
    }
}
