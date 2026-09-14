using Aiko.Core;

namespace Aiko.App;

/// Draws the card on made up numbers. Started with --snapshot, so the card can be looked at after
/// every change without waiting for real limits to move.
static class CardSnapshot
{
    /// The made up numbers every snapshot uses, so the card and the island show the same thing.
    public static IReadOnlyList<CardState> Example()
    {
        var now = DateTimeOffset.Now;
        return
        [
            Card("Personal", now, fiveHour: 42, sevenDay: 18, fiveHourLeft: 2.4, model: 61),
            Card("Work", now, fiveHour: 82, sevenDay: 64, fiveHourLeft: 1.1, model: null),
        ];
    }

    public static void Write(string path)
    {
        var now = DateTimeOffset.Now;
        var cards = Example();

        var accounts = new Dictionary<string, CardAccount>
        {
            ["Personal"] = new("Max 5x", SignedIn: true),
            ["Work"] = new("Team", SignedIn: true),
        };

        var panel = new CardPanel();
        panel.Show(CardModel.From(cards, now, accounts: accounts));

        Snapshot.Write(panel, path);
    }

    /// The same picture, but on what Aiko really has right now: the files the bridge wrote and the
    /// environments as they are set up. This is how to see what the user is seeing.
    public static void WriteLive(string path)
    {
        using var watcher = new SnapshotWatcher();
        var now = DateTimeOffset.Now;

        var cards = EnvironmentSnapshots.Combine(SettingsStore.LoadEnvironments(), watcher.ByFile)
            .Select(snapshot => CardState.From(snapshot, now))
            .ToList();

        var accounts = SettingsStore.LoadEnvironments().Environments.ToDictionary(
            environment => environment.Name,
            environment => new CardAccount(
                ClaudeAccounts.Read(environment.ConfigDirectories[0]).PlanLabel,
                System.IO.File.Exists(ClaudeInstall.CredentialsPathIn(environment.ConfigDirectories[0]))));

        var panel = new CardPanel();
        panel.Show(CardModel.From(cards, now, accounts: accounts));

        Snapshot.Write(panel, path);
    }

    private static CardState Card(
        string name,
        DateTimeOffset now,
        int fiveHour,
        int sevenDay,
        double fiveHourLeft,
        int? model)
    {
        var windows = new List<LimitWindow>
        {
            new(LimitKind.FiveHour, fiveHour, now.AddHours(fiveHourLeft)),
            new(LimitKind.SevenDay, sevenDay, now.AddDays(3.5)),
        };

        // Personal spoke a moment ago and shows "working now"; Work went quiet a few minutes back.
        var receivedAt = name == "Personal" ? now.AddSeconds(-20) : now.AddMinutes(-4);
        var snapshot = new LimitSnapshot(name, LimitSource.StatusLine, receivedAt, windows)
        {
            Model = model is { } percent ? new ModelLimit("Fable", percent, now.AddDays(5)) : null,
        };

        return CardState.From(snapshot, now);
    }
}
