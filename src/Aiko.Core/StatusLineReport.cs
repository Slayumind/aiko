using System.Text.Json;

namespace Aiko.Core;

public enum LimitKind
{
    FiveHour,
    SevenDay,
}

public readonly record struct LimitWindow(LimitKind Kind, int Percent, DateTimeOffset ResetsAt);

/// What Claude Code reports to the status line: the five hour and the seven day window.
/// The model limit (Fable) is not here — it only comes from the usage API in direct mode.
public sealed record StatusLineReport(IReadOnlyList<LimitWindow> Windows)
{
    public static readonly StatusLineReport Empty = new([]);

    public bool HasData => Windows.Count > 0;

    /// Never throws: a broken line must not break the tray. Anything unexpected reads as "no data".
    public static StatusLineReport FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("rate_limits", out var limits)
                || limits.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            var windows = new List<LimitWindow>(2);
            AddWindow(windows, limits, "five_hour", LimitKind.FiveHour);
            AddWindow(windows, limits, "seven_day", LimitKind.SevenDay);
            return windows.Count > 0 ? new StatusLineReport(windows) : Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public LimitWindow? Find(LimitKind kind)
    {
        foreach (var window in Windows)
        {
            if (window.Kind == kind)
            {
                return window;
            }
        }
        return null;
    }

    private static void AddWindow(List<LimitWindow> windows, JsonElement limits, string name, LimitKind kind)
    {
        if (!limits.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Both fields are needed: a percentage without a reset time cannot be counted down,
        // and a reset time without a percentage has nothing to show.
        if (!window.TryGetProperty("used_percentage", out var percent) || percent.ValueKind != JsonValueKind.Number
            || !window.TryGetProperty("resets_at", out var resetsAt) || resetsAt.ValueKind != JsonValueKind.Number)
        {
            return;
        }

        windows.Add(new LimitWindow(kind, ToPercent(percent.GetDouble()), DateTimeOffset.FromUnixTimeSeconds(resetsAt.GetInt64())));
    }

    /// Claude Code sends values like 28.000000000000004, so the noise is rounded away.
    /// Above 100 is clamped: a bar cannot be longer than full.
    private static int ToPercent(double value) =>
        Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
}
