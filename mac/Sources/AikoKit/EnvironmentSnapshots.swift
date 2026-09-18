import Foundation

/// Puts the files the bridge writes together with the environments the user named.
///
/// The bridge names its file after the Claude Code config folder, because that is all it knows.
/// The user calls the same thing "Personal". This is where the two meet, so the card can show the
/// name a person chose instead of the name of a folder.
public enum EnvironmentSnapshots {
    /// One snapshot per environment, in the order the settings hold them. An environment may
    /// cover several config folders; then the freshest answer wins, the same rule two sources use.
    ///
    /// Files that belong to no environment are left out — except when no environment has been set
    /// up yet. Then everything that arrived is shown as it is: the data is already coming in, and
    /// hiding it until the wizard has run would only look broken.
    public static func combine(
        _ settings: EnvironmentSettings, _ byFile: [String: LimitSnapshot]
    ) -> [LimitSnapshot] {
        guard settings.hasEnvironments else {
            return byFile.values.sorted {
                let order = $0.environment.caseInsensitiveCompare($1.environment)
                return order == .orderedAscending
            }
        }

        return settings.environments.map { environment in
            var found = LimitSnapshot.noData(environment.name)

            for directory in environment.configDirectories {
                if let snapshot = byFile[SnapshotName.forConfigDirectory(directory)] {
                    found = LimitSnapshot.newer(found, snapshot)
                }
            }

            // The name on the card is the one the person gave, never the folder name.
            return LimitSnapshot(
                environment: environment.name,
                source: found.source,
                receivedAt: found.receivedAt,
                windows: found.windows,
                model: found.model)
        }
    }
}
