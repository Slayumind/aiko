namespace Aiko.Core;

/// The query of the daily version check (PRIVACY.md, "Counting"). With counting off it carries the
/// version only. With counting on it adds the system, the identifier of the day, whether the persona
/// is on, and a flag for the first check of a week and of a month: six fields, nothing more.
/// Rows: spec/cases/count-query.
public static class CountQuery
{
    /// day: the identifier of the day (Heartbeat.DailyId), null when counting is off or the computer
    /// has no steady value to make one from.
    public static string For(string version, string system, string? day, bool persona, bool firstOfWeek, bool firstOfMonth)
    {
        var plain = $"?v={Uri.EscapeDataString(version)}";
        if (string.IsNullOrEmpty(day))
        {
            return plain;
        }

        return plain
            + $"&os={Uri.EscapeDataString(system)}&day={Uri.EscapeDataString(day)}&p={(persona ? 1 : 0)}"
            + (firstOfWeek ? "&w=1" : string.Empty)
            + (firstOfMonth ? "&m=1" : string.Empty);
    }
}
