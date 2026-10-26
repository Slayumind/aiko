import Foundation

/// Why a folder is offered in the form of a new session.
public enum FolderSource: Sendable, Equatable {
    /// A session ran there not long ago.
    case recent

    /// The folder is bound to the environment.
    case bound

    /// The folder lies right inside a bound one.
    case inside
}

public struct FolderChoice: Sendable, Equatable {
    public let path: String
    public let name: String
    public let source: FolderSource

    public init(path: String, name: String, source: FolderSource) {
        self.path = path
        self.name = name
        self.source = source
    }
}

/// The two groups of the folder list: where sessions ran, and the folders of the environment.
public enum FolderGroup: Sendable, Equatable {
    case recent
    case bound
}

/// A group title in the folder list, drawn above the folder at before.
public struct FolderHeader: Sendable, Equatable {
    public let before: Int
    public let group: FolderGroup
}

/// A model as Claude Code names it in its answer to "initialize": the word for --model, and the
/// name to show, like "fable" and "Fable 5.1".
public struct ClaudeModel: Sendable, Equatable {
    public let value: String
    public let displayName: String

    public init(value: String, displayName: String) {
        self.value = value
        self.displayName = displayName
    }
}

/// What the form of a new session offers (D-293, D-300): the folders, the models and the name the
/// session gets when the field is left empty.
///
/// The twin of NewSessionChoices.cs.
public enum NewSessionChoices {
    public static let mostRecent = 5

    /// The folders offered from inside one bound folder: the ones changed last (D-335).
    public static let mostInside = 8

    /// The models of the form until Claude Code named its own, as the words it takes for --model.
    /// They are the short names, which stay the same from one version of a model to the next.
    /// The form offers models only (D-308): a session always starts with one of them.
    public static let models = ["opus", "sonnet", "haiku"]

    /// The models the form offers, from the ones Claude Code named: only the short names, in its
    /// order (AIKO-387). A new family of models comes with no change in Aiko. "default" and the
    /// full ids of single versions stay out. When Claude Code named none, the list above.
    public static func offered(_ named: [ClaudeModel]) -> [ClaudeModel] {
        let shortNames = named.filter { model in
            model.value != "default" && !model.value.isEmpty
                && model.value.unicodeScalars.allSatisfy { ("a"..."z").contains($0) }
        }
        if !shortNames.isEmpty {
            return shortNames
        }
        return models.map { ClaudeModel(value: $0, displayName: $0.prefix(1).uppercased() + $0.dropFirst()) }
    }

    /// The folders, each once whatever the case of their letters: where sessions of the
    /// environment ran last, then every bound folder followed by up to mostInside folders inside
    /// it. foldersInside gives the full paths of the folders right inside one, the ones changed
    /// last first; reading the disk is not done here.
    public static func folders(
        _ sessions: TerminalSessions,
        _ environment: AikoEnvironment,
        foldersInside: (String) -> [String]
    ) -> [FolderChoice] {
        var choices: [FolderChoice] = []

        @discardableResult
        func offer(_ path: String, _ source: FolderSource) -> Bool {
            let folder = SessionName.folderPath(path)
            guard !folder.isEmpty, !choices.contains(where: { $0.path.caseInsensitiveCompare(folder) == .orderedSame }) else {
                return false
            }
            choices.append(FolderChoice(path: folder, name: SessionName.folderName(folder), source: source))
            return true
        }

        // A stable sort keeps the order of the list for sessions used at the same time.
        let used = sessions.sessions
            .filter { $0.environment == environment.name }
            .enumerated()
            .sorted { $0.element.lastUsed != $1.element.lastUsed
                ? $0.element.lastUsed > $1.element.lastUsed
                : $0.offset < $1.offset
            }

        var recent: [String] = []
        for session in used {
            let folder = SessionName.folderPath(session.element.folder)
            if !recent.contains(where: { $0.caseInsensitiveCompare(folder) == .orderedSame }) {
                recent.append(folder)
            }
        }
        for folder in recent.prefix(mostRecent) {
            offer(folder, .recent)
        }

        for bound in environment.projectFolders {
            offer(bound, .bound)
            var offered = 0
            for inside in foldersInside(bound) where offered < mostInside {
                if offer(inside, .inside) { offered += 1 }
            }
        }

        return choices
    }

    /// The group titles of a folder list: one above the first folder of each group. A folder
    /// inside a bound one is in the group of the bound folders.
    public static func headers(_ folders: [FolderChoice]) -> [FolderHeader] {
        var headers: [FolderHeader] = []
        for (at, folder) in folders.enumerated() {
            let group: FolderGroup = folder.source == .recent ? .recent : .bound
            if headers.last?.group != group {
                headers.append(FolderHeader(before: at, group: group))
            }
        }
        return headers
    }

    /// The model the form starts with: the one of the session of the environment that was used
    /// last and has a model of the list. With no such session it is the first of the list.
    /// offered are the short names offered; without them, the list above.
    public static func defaultModel(_ sessions: TerminalSessions, _ environment: String, _ offered: [String] = []) -> String {
        let list = offered.isEmpty ? models : offered
        var latest: (model: String, used: Int64)?
        for session in sessions.sessions where session.environment == environment {
            if let model = family(session.model, list), session.lastUsed > (latest?.used ?? Int64.min) {
                latest = (model, session.lastUsed)
            }
        }
        return latest?.model ?? list[0]
    }

    /// The model after this one, for a click on the model under the input: in the order of the
    /// form, and back to the first after the last. A model outside the list goes to the first one.
    public static func nextModel(_ model: String?, _ offered: [String] = []) -> String {
        let list = offered.isEmpty ? models : offered
        let at = family(model, list).flatMap { list.firstIndex(of: $0) } ?? -1
        return list[(at + 1) % list.count]
    }

    /// The model of the list a short name or a full id like "claude-sonnet-5-5" belongs to.
    private static func family(_ model: String?, _ list: [String]) -> String? {
        guard let model else { return nil }
        return list.first { model.range(of: $0, options: .caseInsensitive) != nil }
    }

    /// The name of the other environment a folder is bound to, or nil. The form says so before
    /// the session is made: Claude Code started there from a console would use that environment.
    public static func boundElsewhere(
        _ platform: PlatformConventions, _ folder: String, _ environment: AikoEnvironment,
        _ settings: EnvironmentSettings
    ) -> String? {
        guard let other = ProjectBinding.boundEnvironmentFor(platform, workingDirectory: folder, settings: settings),
              other.name != environment.name
        else {
            return nil
        }
        return other.name
    }

    /// The name the session gets when the field is left empty.
    public static func defaultName(_ sessions: TerminalSessions, _ environment: String, _ folder: String) -> String {
        SessionName.defaultName(folder, taken: sessions.namesIn(environment, folder))
    }
}
