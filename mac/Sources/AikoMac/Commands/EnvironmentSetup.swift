import AikoKit
import Foundation

/// Puts a saved list of environments into effect outside Aiko: the status line and the reminder
/// hook in each Claude Code folder, and the command folder on PATH. The checklist's Finish comes
/// here, so a later "set it up" can never do it differently.
///
/// The twin of Commands/EnvironmentSetup.cs, less the PowerShell profile part: a zsh function that
/// shadows the shim is the person's own choice and Aiko does not rewrite it (PARITY).
enum EnvironmentSetup {
    static func applyAccess(_ settings: EnvironmentSettings) -> [PatchProblem] {
        guard let bridge = BridgePath.current() else {
            return [.bridgeUnknown]
        }

        var problems: [PatchProblem] = []
        let anyBinding = settings.environments.contains { !$0.projectFolders.isEmpty }

        for folder in settings.environments.flatMap(\.configDirectories) {
            let outcome = ClaudeSettingsFile.addBridge(folder, bridge)
            if outcome.problem != .none {
                problems.append(outcome.problem)
            }

            // The reminder about bindings is only worth a process at session start once something
            // is bound.
            if anyBinding {
                _ = ClaudeSettingsFile.addSessionHook(folder, bridge)
            }
        }

        return problems
    }

    /// The commands that now work, or nil when the shim is not there to set them up.
    static func applyCommands(_ settings: EnvironmentSettings) -> String? {
        guard CommandFolder.sync(settings) else { return nil }

        CommandFolder.addToPath()
        return settings.environments.map(\.command).joined(separator: ", ")
    }

    /// Takes back what applyCommands put outside the Claude Code folders.
    static func undo() {
        CommandFolder.remove()
        Log.write("apply: commands and the \(ZshProfile.fileName) block undone")
    }
}
