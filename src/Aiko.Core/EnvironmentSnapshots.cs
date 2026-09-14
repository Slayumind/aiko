namespace Aiko.Core;

/// Puts the files the bridge writes together with the environments the user named.
///
/// The bridge names its file after the Claude Code config folder, because that is all it knows.
/// The user calls the same thing "Personal". This is where the two meet, so the card can show the
/// name a person chose instead of the name of a folder.
public static class EnvironmentSnapshots
{
    /// One snapshot per environment, in the order the settings hold them. An environment may
    /// cover several config folders; then the freshest answer wins, the same rule two sources use.
    ///
    /// Files that belong to no environment are left out — except when no environment has been set
    /// up yet. Then everything that arrived is shown as it is: the data is already coming in, and
    /// hiding it until the wizard has run would only look broken.
    public static IReadOnlyList<LimitSnapshot> Combine(
        EnvironmentSettings settings,
        IReadOnlyDictionary<string, LimitSnapshot> byFile)
    {
        if (!settings.HasEnvironments)
        {
            return byFile.Values
                .OrderBy(snapshot => snapshot.Environment, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var combined = new List<LimitSnapshot>(settings.Environments.Count);
        foreach (var environment in settings.Environments)
        {
            var found = LimitSnapshot.NoData(environment.Name);

            foreach (var directory in environment.ConfigDirectories)
            {
                if (byFile.TryGetValue(SnapshotName.For(directory), out var snapshot))
                {
                    found = LimitSnapshot.Newer(found, snapshot);
                }
            }

            // The name on the card is the one the person gave, never the folder name.
            combined.Add(found with { Environment = environment.Name });
        }

        return combined;
    }
}
