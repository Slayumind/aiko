namespace Aiko.Core.Terminal;

/// What one environment brings to the open header of the terminal window: its limits and how many
/// of its sessions wait for the person.
public sealed record HeaderInput(string Environment, LimitSnapshot? Limits, int Waiting);

/// One column of the open header. The active environment is shown in full; the others are a quiet
/// column with two numbers. A number is null when nothing is known about that window.
public sealed record HeaderColumn(string Environment, bool Active, int? FiveHour, int? SevenDay, int Waiting);

/// The open header of the terminal window (D-373, D-376): the active environment always comes
/// first, the others follow in their own order. The pills above keep their order; only the columns
/// move.
public static class HeaderStrip
{
    /// When active names no environment of the list, the first one is active: the window always
    /// has a chosen tab.
    public static IReadOnlyList<HeaderColumn> Columns(IReadOnlyList<HeaderInput> environments, string active, DateTimeOffset now)
    {
        var chosen = environments.Any(input => input.Environment == active)
            ? active
            : environments.FirstOrDefault()?.Environment;

        return
        [
            .. environments.Where(input => input.Environment == chosen).Select(input => Column(input, true, now)),
            .. environments.Where(input => input.Environment != chosen).Select(input => Column(input, false, now)),
        ];
    }

    private static HeaderColumn Column(HeaderInput input, bool active, DateTimeOffset now) =>
        new(
            input.Environment,
            active,
            input.Limits?.StatusAt(now, LimitKind.FiveHour)?.Percent,
            input.Limits?.StatusAt(now, LimitKind.SevenDay)?.Percent,
            input.Waiting);
}
