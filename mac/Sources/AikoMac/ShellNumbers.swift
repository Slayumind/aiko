import AikoKit
import Foundation

/// The numbers the shell shows, read from where they are kept: the snapshot files the bridge writes
/// and the settings. The rules for what a number means are in AikoKit; this class only brings the
/// sources together. The twin of ShellNumbers.cs.
@MainActor
final class ShellNumbers {
    /// The snapshot files, once the shell has started the watch.
    var watch: SnapshotWatch?

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    private var accounts: [String: CardAccount] = [:]

    /// Environments where Aiko's line is not in the Claude Code settings, so no numbers can ever
    /// arrive. Worked out when the settings change rather than every time the card is drawn.
    private var noAccess: Set<String> = []

    /// What every environment last reported, in the order the settings hold them.
    func snapshots() -> [LimitSnapshot] {
        EnvironmentSnapshots.combine(Store.environments(), watch?.byFile ?? [:])
    }

    /// Every environment we know about, in the order the settings hold them. The card shows all of
    /// them, including the ones with nothing reported yet: an empty block says so in words.
    func cards() -> [CardState] {
        let now = Date()
        // Read every time: the settings can rename an environment while Aiko runs, and the file
        // is small.
        // TODO: direct mode has no poller on macOS yet, so the status line is the only source.
        return snapshots().map { CardState.from($0, now) }
    }

    /// The card for all the environments, with what the card alone needs: the plan and whether
    /// Aiko's line is in the settings.
    func card() -> CardModel {
        CardModel.from(cards(), Date(), noAccess: noAccess, accounts: accounts)
    }

    /// The ring shows one environment and the dot the other.
    func iconRows() -> (ring: CardRow?, dot: CardRow?) {
        IconRows.choose(cards(), ringEnvironment: Store.environments().ringEnvironment)
    }

    func readAccounts() {
        accounts = Store.accounts(Store.environments())
    }

    func noteWhereWeHaveNoAccess() {
        let patch = SettingsJsonPatch(.macOS)
        var missing: Set<String> = []

        for environment in Store.environments().environments where !environment.directMode {
            for folder in environment.configDirectories {
                let path = ClaudeSettingsEditor.pathIn(folder)
                let text = (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
                if !patch.hasOurLine(text) {
                    missing.insert(environment.name)
                }
            }
        }

        noAccess = missing
    }

    /// The environment the ring does not show, when there is one. Nil with a single environment,
    /// and then the menu leaves the item out rather than showing one that does nothing.
    static func otherEnvironment() -> String? {
        Store.environments().dot?.name
    }

    /// The chosen environment is kept in the environments file, not in a field here. It is a choice
    /// the user made, and a choice that quietly goes back on the next start is worse than none.
    ///
    /// True when the ring now shows the other environment, so the caller draws the icon again.
    static func swapRing() -> Bool {
        let environments = Store.environments()
        let swapped = environments.swapRing()
        guard swapped != environments else { return false }

        Store.saveEnvironments(swapped)
        return true
    }
}
