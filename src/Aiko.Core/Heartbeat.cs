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
}
