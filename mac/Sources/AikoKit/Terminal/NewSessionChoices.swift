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
}

/// What the form of a new session offers (D-293, D-300): the folders, the models and the name the
/// session gets when the field is left empty.
///
/// The twin of NewSessionChoices.cs.
public enum NewSessionChoices {
    public static let mostRecent = 5

    /// The models of the form, as the words Claude Code takes for --model. They are the short
    /// names, which stay the same from one version of a model to the next. The form offers models
    /// only (D-308): a session always starts with one of them.
    public static let models = ["opus", "sonnet", "haiku"]

    /// The folders, each once: where sessions of the environment ran last, then every bound
    /// folder followed by the folders inside it. foldersInside gives the full paths of the folders
    /// right inside one; reading the disk is not done here.
    public static func folders(
        _ sessions: TerminalSessions,
        _ environment: AikoEnvironment,
        foldersInside: (String) -> [String]
    ) -> [FolderChoice] {
        var choices: [FolderChoice] = []

        func offer(_ path: String, _ source: FolderSource) {
            let folder = SessionName.folderPath(path)
            if !folder.isEmpty && !choices.contains(where: { $0.path == folder }) {
                choices.append(FolderChoice(path: folder, name: SessionName.folderName(folder), source: source))
            }
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
        for session in used where !recent.contains(session.element.folder) {
            recent.append(session.element.folder)
        }
        for folder in recent.prefix(mostRecent) {
            offer(folder, .recent)
        }

        for bound in environment.projectFolders {
            offer(bound, .bound)
            for inside in foldersInside(bound) {
                offer(inside, .inside)
            }
        }

        return choices
    }

    /// The model the form starts with: the one of the session of the environment that was used
    /// last and has a model of the list. With no such session it is the first of the list.
    public static func defaultModel(_ sessions: TerminalSessions, _ environment: String) -> String {
        var latest: (model: String, used: Int64)?
        for session in sessions.sessions where session.environment == environment {
            if let model = session.model, models.contains(model), session.lastUsed > (latest?.used ?? Int64.min) {
                latest = (model, session.lastUsed)
            }
        }
        return latest?.model ?? models[0]
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
