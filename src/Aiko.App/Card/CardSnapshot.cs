using Aiko.Core;

namespace Aiko.App;

/// Made up limits for the snapshots and checks of the island, so they can be looked at after every
/// change without waiting for real limits to move.
static class CardSnapshot
{
    /// The made up numbers every snapshot of the island uses.
    public static IReadOnlyList<CardState> Example()
    {
        var now = DateTimeOffset.Now;
        return
        [
            Card("Personal", now, fiveHour: 42, sevenDay: 18, fiveHourLeft: 2.4, model: 61),
            Card("Work", now, fiveHour: 82, sevenDay: 64, fiveHourLeft: 1.1, model: null),
        ];
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
