import Foundation
import Testing

@testable import AikoKit

/// The bridge and the app have to agree on which folder a run belongs to.
///
/// With CLAUDE_CONFIG_DIR unset the bridge wrote default.json and the app looked for the folder's
/// own name. One account and nothing configured is the commonest setup there is, and it would
/// never have shown a single limit.
struct ClaudeConfigFolderTests {
    static let home = #"C:\Users\someone"#

    @Test(arguments: [nil, "", "   "] as [String?])
    func withoutTheVariableItIsTheFolderClaudeCodeUsesByDefault(variable: String?) {
        #expect(ClaudeConfigFolder.resolve(.windows, variable, Self.home) == Self.home + #"\.claude"#)
    }

    @Test
    func aSetVariableIsTakenAsItIs() {
        #expect(
            ClaudeConfigFolder.resolve(.windows, #"C:\Users\someone\.claude-personal"#, Self.home)
                == #"C:\Users\someone\.claude-personal"#)
    }

    @Test
    func surroundingSpacesInTheVariableDoNotMakeAnotherFolder() {
        #expect(ClaudeConfigFolder.resolve(.windows, #"  D:\claude  "#, Self.home) == #"D:\claude"#)
    }

    @Test
    func theBridgeAndTheAppArriveAtTheSameFileName() {
        // The bridge sees no variable. The app knows the environment by the folder the wizard
        // found. Both have to name the same snapshot file, or the card waits forever.
        let fromBridge = SnapshotName.forConfigDirectory(ClaudeConfigFolder.resolve(.windows, nil, Self.home))
        let fromApp = SnapshotName.forConfigDirectory(Self.home + #"\.claude"#)

        #expect(fromBridge == fromApp)
        #expect(fromBridge != SnapshotName.default)
    }

    @Test
    func theAppFindsWhatTheBridgeWroteForADefaultFolder() {
        let folder = Self.home + #"\.claude"#
        let settings = EnvironmentSettings([AikoEnvironment("Work", [folder])])
        let written = LimitSnapshot(
            environment: SnapshotName.forConfigDirectory(ClaudeConfigFolder.resolve(.windows, nil, Self.home)),
            source: .statusLine,
            receivedAt: Date(),
            windows: [LimitWindow(kind: .fiveHour, percent: 40, resetsAt: Date().adding(hours: 2))])

        let combined = EnvironmentSnapshots.combine(settings, [written.environment: written])

        #expect(combined.first?.hasData == true)
    }
}
