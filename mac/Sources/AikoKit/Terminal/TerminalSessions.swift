import Foundation

/// What runs in a session: Claude Code, or a shell in the same folder (D-350).
public enum SessionKind: String, Sendable {
    case claude
    case shell
}

/// One session of the terminal as Aiko remembers it between runs (D-293): which conversation of
/// Claude Code it is and where it runs. Not a word of the conversation is here.
public struct TerminalSession: Sendable, Equatable {
    public let id: String
    /// Changes only when its environment is renamed.
    public internal(set) var environment: String
    public let folder: String
    public var name: String

    public var kind: SessionKind = .claude

    /// Claude Code has the conversation on disk, so the next start resumes it.
    public var started = false

    /// A closed session is out of the list. Its id is kept, so the conversation can be found again.
    public var closed = false

    /// When something last happened in it, in seconds since 1970. Zero when nothing did.
    public var lastUsed: Int64 = 0

    public init(id: String, environment: String, folder: String, name: String) {
        self.id = id
        self.environment = environment
        self.folder = folder
        self.name = name
    }
}

/// The sessions of one folder, as one group of the list.
public struct SessionGroup: Sendable, Equatable {
    public let folder: String
    public let name: String
    public let sessions: [TerminalSession]
}

/// The sessions of the terminal, in the order the person put them, and the one that was open last.
///
/// This is the file terminal.json. The list changes only through the methods here; each of them
/// gives a new list.
///
/// The twin of TerminalSessions.cs.
public struct TerminalSessions: Sendable, Equatable {
    public private(set) var sessions: [TerminalSession]
    public private(set) var lastId: String?

    /// Where the window stood when it was closed. Nil until it was closed once.
    public private(set) var window: WindowPlace?

    public static let empty = TerminalSessions(sessions: [], lastId: nil)

    // MARK: - Reading the list

    public func find(_ id: String) -> TerminalSession? {
        sessions.first { $0.id == id }
    }

    /// The sessions of an environment that are in the list, in its order.
    public func open(_ environment: String) -> [TerminalSession] {
        sessions.filter { !$0.closed && $0.environment == environment }
    }

    /// The list of an environment: a group for each folder that has a session in the list. The
    /// groups stand in the order their folders first appear in the file, closed sessions counted,
    /// so a group does not jump when its first session is closed.
    public func groups(_ environment: String) -> [SessionGroup] {
        var folders: [String] = []
        for session in sessions where session.environment == environment && !folders.contains(session.folder) {
            folders.append(session.folder)
        }

        let inList = open(environment)
        return folders
            .map { folder in
                SessionGroup(
                    folder: folder,
                    name: SessionName.folderName(folder),
                    sessions: inList.filter { $0.folder == folder })
            }
            .filter { !$0.sessions.isEmpty }
    }

    /// The session the window opens on: the last one, or the first of the environment.
    public func toOpen(_ environment: String) -> TerminalSession? {
        let inList = open(environment)
        return inList.first { $0.id == lastId } ?? inList.first
    }

    // MARK: - Changing the list

    /// A new session at the end of the list. An empty name becomes the folder with a number. An
    /// id that is already taken, or one without an environment or a folder, changes nothing.
    public func add(
        id: String, environment: String, folder: String, name: String, now: Int64, kind: SessionKind = .claude
    ) -> TerminalSessions {
        let place = SessionName.folderPath(folder)
        if id.isEmpty || environment.isEmpty || place.isEmpty || find(id) != nil {
            return self
        }

        let given = SessionName.clean(name)
        var session = TerminalSession(
            id: id,
            environment: environment,
            folder: place,
            name: given.isEmpty ? SessionName.defaultName(place, taken: namesIn(environment, place)) : given)
        session.kind = kind
        session.lastUsed = now

        var next = self
        next.sessions.append(session)
        next.lastId = id
        return next
    }

    /// The names the open sessions of a folder already have.
    public func namesIn(_ environment: String, _ folder: String) -> [String] {
        let place = SessionName.folderPath(folder)
        return open(environment).filter { $0.folder == place }.map(\.name)
    }

    /// An empty name changes nothing: a session is never left without one.
    public func rename(_ id: String, _ name: String) -> TerminalSessions {
        let clean = SessionName.clean(name)
        return clean.isEmpty ? self : with(id) { $0.name = clean }
    }

    /// Claude Code has the conversation now.
    public func markStarted(_ id: String) -> TerminalSessions {
        with(id) { $0.started = true }
    }

    public func used(_ id: String, now: Int64) -> TerminalSessions {
        with(id) { $0.lastUsed = now }
    }

    /// A renamed environment takes its sessions with it: they are known by its name.
    public func renameEnvironment(_ from: String, _ to: String) -> TerminalSessions {
        guard from != to else { return self }
        var copy = self
        copy.sessions = sessions.map { session in
            var renamed = session
            if renamed.environment == from { renamed.environment = to }
            return renamed
        }
        return copy
    }

    /// The sessions after the settings changed under an open window. An environment keeps its
    /// folder of Claude Code when it is renamed, so the folder tells which old name is which new one.
    public func follow(_ before: EnvironmentSettings, _ after: EnvironmentSettings) -> TerminalSessions {
        var next = self
        for old in before.environments {
            let sameFolder = { (environment: AikoEnvironment) in
                environment.configDirectories.map { $0.lowercased() } == old.configDirectories.map { $0.lowercased() }
            }
            if let renamed = after.environments.first(where: { $0.name != old.name && sameFolder($0) }),
               !after.environments.contains(where: { $0.name == old.name }) {
                next = next.renameEnvironment(old.name, renamed.name)
            }
        }
        return next
    }

    /// The person went into this session.
    public func opened(_ id: String) -> TerminalSessions {
        guard let session = find(id), !session.closed else {
            return self
        }
        var next = self
        next.lastId = id
        return next
    }

    /// Takes the session out of the list. If it was the one the window was on, the window goes to
    /// its neighbour in the same environment: the next one, or the one before.
    public func close(_ id: String) -> TerminalSessions {
        guard let closing = find(id), !closing.closed else {
            return self
        }

        let around = open(closing.environment)
        let at = around.firstIndex { $0.id == id } ?? 0
        let neighbour = at + 1 < around.count ? around[at + 1] : at > 0 ? around[at - 1] : nil

        var next = with(id) { $0.closed = true }
        if lastId == id {
            next.lastId = neighbour?.id
        }
        return next
    }

    /// The window was closed at this place. It is kept in whole units; a place that is not a
    /// place at all changes nothing.
    public func placed(_ place: WindowPlace) -> TerminalSessions {
        guard let whole = Self.whole(place) else {
            return self
        }
        var next = self
        next.window = whole
        return next
    }

    /// A place in the file needs all four numbers; a width or a height of zero or less is no place.
    private static func placeIn(_ node: JsonNode?) -> WindowPlace? {
        guard let node, node.isObject,
              let left = node["left"]?.doubleValue, let top = node["top"]?.doubleValue,
              let width = node["width"]?.doubleValue, let height = node["height"]?.doubleValue else {
            return nil
        }
        return whole(WindowPlace(
            left: left, top: top, width: width, height: height, maximized: node["maximized"]?.boolValue == true))
    }

    private static func whole(_ place: WindowPlace) -> WindowPlace? {
        let numbers = [place.left, place.top, place.width, place.height]
        guard numbers.allSatisfy(\.isFinite), place.width > 0, place.height > 0 else {
            return nil
        }
        return WindowPlace(
            left: place.left.rounded(), top: place.top.rounded(),
            width: place.width.rounded(), height: place.height.rounded(), maximized: place.maximized)
    }

    private func with(_ id: String, _ change: (inout TerminalSession) -> Void) -> TerminalSessions {
        var next = self
        for index in next.sessions.indices where next.sessions[index].id == id {
            change(&next.sessions[index])
        }
        return next
    }

    // MARK: - The file

    /// Never fails. A broken file is an empty list; a session without an id, an environment or
    /// a folder is left out, and so is a second session with an id that was already read.
    public static func fromJson(_ json: String?) -> TerminalSessions {
        guard let root = JsonNode.parse(json), root.isObject else {
            return empty
        }

        var sessions: [TerminalSession] = []
        for item in root["sessions"]?.arrayValue ?? [] {
            if let session = sessionIn(item), !sessions.contains(where: { $0.id == session.id }) {
                sessions.append(session)
            }
        }

        let last = root["last"]?.stringValue
        return TerminalSessions(
            sessions: sessions,
            lastId: sessions.contains { $0.id == last && !$0.closed } ? last : nil,
            window: placeIn(root["window"]))
    }

    public func toJson() -> String {
        var root = JsonObject()
        root["sessions"] = .array(sessions.map { session in
            var file = JsonObject()
            file["id"] = .string(session.id)
            file["environment"] = .string(session.environment)
            file["folder"] = .string(session.folder)
            file["name"] = .string(session.name)
            // A session of Claude Code is the old shape of the file: only a shell is named.
            if session.kind == .shell {
                file["kind"] = .string("shell")
            }
            file["started"] = .bool(session.started)
            file["closed"] = .bool(session.closed)
            file["lastUsed"] = .number(String(session.lastUsed))
            return .object(file)
        })
        if let lastId {
            root["last"] = .string(lastId)
        }
        if let window {
            var place = JsonObject()
            place["left"] = .number(String(Int64(window.left)))
            place["top"] = .number(String(Int64(window.top)))
            place["width"] = .number(String(Int64(window.width)))
            place["height"] = .number(String(Int64(window.height)))
            place["maximized"] = .bool(window.maximized)
            root["window"] = .object(place)
        }

        return JsonNode.object(root).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }

    private static func sessionIn(_ item: JsonNode) -> TerminalSession? {
        let id = item["id"]?.stringValue ?? ""
        let environment = item["environment"]?.stringValue ?? ""
        let folder = SessionName.folderPath(item["folder"]?.stringValue)
        if id.isEmpty || environment.isEmpty || folder.isEmpty {
            return nil
        }

        let name = SessionName.clean(item["name"]?.stringValue)
        var session = TerminalSession(
            id: id, environment: environment, folder: folder,
            name: name.isEmpty ? SessionName.folderName(folder) : name)

        session.kind = item["kind"]?.stringValue == "shell" ? .shell : .claude
        session.started = item["started"]?.boolValue == true
        session.closed = item["closed"]?.boolValue == true
        session.lastUsed = item["lastUsed"]?.int64Value ?? 0
        return session
    }
}
