using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aiko.Core;

/// The one number the author of Aiko wants to know: roughly how many people use it on a given day.
///
/// It rides along with the update check, which already asks the same server once a day, so there
/// is one connection to explain instead of two and one switch to turn both off.
///
/// What makes this bearable is that the identifier changes every day. A random value is made once
/// on this computer and never leaves it; what travels is a hash of that value together with the
/// date. Two days of traffic cannot be joined into one person, so the count is of days, not of
/// people followed over time. That is the number that was wanted anyway.
public static class Heartbeat
{
    /// Long enough that two installs do not collide in a day, short enough to be useless for
    /// anything else.
    public const int IdLength = 16;

    public static string DailyId(string installId, DateOnly day)
    {
        if (string.IsNullOrWhiteSpace(installId))
        {
            return string.Empty;
        }

        var material = $"{installId}:{day:yyyy-MM-dd}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(digest)[..IdLength].ToLowerInvariant();
    }

    /// A week and a month cannot be counted from daily identifiers: two days never join up, which
    /// is the whole point of them. So the copy answers the question itself. It remembers on this
    /// computer which week and which month it has already reported, and sends one bit with the
    /// daily ping: "this is my first run in that period". The server adds the ones up.
    ///
    /// The cost is that the windows are calendar ones, not the last 7 or 30 days. A rolling window
    /// would need an identifier that outlives it, and that is a name.
    public static string WeekKey(DateOnly day)
    {
        var moment = day.ToDateTime(TimeOnly.MinValue);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ISOWeek.GetYear(moment)}-W{ISOWeek.GetWeekOfYear(moment):00}");
    }

    public static string MonthKey(DateOnly day) => day.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// Null or an empty value means nothing was reported yet, so this run is the first one.
    public static bool FirstThisWeek(string? reported, DateOnly day) =>
        !string.Equals(reported, WeekKey(day), StringComparison.Ordinal);

    public static bool FirstThisMonth(string? reported, DateOnly day) =>
        !string.Equals(reported, MonthKey(day), StringComparison.Ordinal);
}
