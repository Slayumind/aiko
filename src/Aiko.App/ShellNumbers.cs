using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The numbers the shell shows, read from where they are kept: the snapshot files the bridge
/// writes, the answers of direct mode, and the settings. The rules for what a number means are in
/// Aiko.Core; this class only brings the sources together.
sealed class ShellNumbers(SnapshotWatcher watcher)
{
    /// Direct mode, once the shell has made it. In a check it has no answers: a check asks no server.
    public DirectPoller? Direct { get; set; }

    /// Every environment we know about, in the order the settings hold them. The card shows all of
    /// them, including the ones with nothing reported yet: an empty block says so in words.
    public IReadOnlyList<CardState> Cards()
    {
        var now = DateTimeOffset.Now;

        // Read every time: the settings window can rename an environment while Aiko runs, and the
        // file is small.
        var environments = SettingsStore.LoadEnvironments();

        var direct = Direct?.Latest;

        return EnvironmentSnapshots.Combine(environments, watcher.ByFile)
            // Two sources for one environment: the fresher answer wins. Direct mode is asked
            // rarely, the status line speaks on every reply, so neither is always ahead. Whether a
            // session is working comes from the status line alone, whichever answer wins.
            .Select(snapshot => direct is not null && direct.TryGetValue(snapshot.Environment, out var fromApi)
                ? CardState.From(LimitSnapshot.Newer(snapshot, fromApi), now) with { WorkingUntil = CardState.WorkingUntilFor(snapshot) }
                : CardState.From(snapshot, now))
            .ToList();
    }

    /// The card for these environments, with what the card alone needs: the plan and whether
    /// Aiko's line is in the settings.
    public CardModel CardFor(IReadOnlyList<CardState> cards, DateTimeOffset now) =>
        CardModel.From(cards, now, _noAccess, _accounts);

    /// The ring shows one environment and the dot the other.
    public (CardRow? Ring, CardRow? Dot) IconRows() =>
        Core.IconRows.Choose(Cards(), SettingsStore.LoadEnvironments().RingEnvironment);

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    private IReadOnlyDictionary<string, CardAccount> _accounts = new Dictionary<string, CardAccount>();

    public void ReadAccounts() =>
        _accounts = SettingsStore.LoadEnvironments().Environments.ToDictionary(
            environment => environment.Name,
            environment => new CardAccount(
                ClaudeAccounts.Read(environment.ConfigDirectories[0]).PlanLabel,
                File.Exists(ClaudeInstall.CredentialsPathIn(ThisComputer.Platform, environment.ConfigDirectories[0]))));

    /// Environments where Aiko's line is not in the Claude Code settings, so no numbers can ever
    /// arrive. Worked out when the settings change rather than every time the card is drawn.
    private IReadOnlySet<string> _noAccess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public void NoteWhereWeHaveNoAccess()
    {
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in SettingsStore.LoadEnvironments().Environments)
        {
            // Direct mode fetches the numbers itself, so the status line is not needed there.
            if (environment.DirectMode)
            {
                continue;
            }

            if (environment.ConfigDirectories.Any(folder => !ClaudeSettingsFile.HasOurLine(folder)))
            {
                missing.Add(environment.Name);
            }
        }

        _noAccess = missing;
    }

    /// The environment the ring does not show, when there is one. Null with a single environment,
    /// and then the menu leaves the item out rather than showing one that does nothing.
    public static string? OtherEnvironment() => SettingsStore.LoadEnvironments().Dot?.Name;

    /// The chosen environment is kept in the environments file, not in a field here. It is a
    /// choice the user made, and a choice that quietly goes back to the other environment on the
    /// next start is worse than no choice at all.
    ///
    /// True when the ring now shows the other environment, so the caller draws the icon again.
    public static bool SwapRing()
    {
        var environments = SettingsStore.LoadEnvironments();
        var swapped = environments.SwapRing();
        if (ReferenceEquals(swapped, environments))
        {
            // One environment: there is nothing to swap with.
            return false;
        }

        SettingsStore.SaveEnvironments(swapped);
        return true;
    }
}
