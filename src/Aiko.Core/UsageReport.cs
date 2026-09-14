using System.Globalization;
using System.Text.Json;

namespace Aiko.Core;

public readonly record struct ModelLimit(string ModelName, int Percent, DateTimeOffset ResetsAt);

/// The answer of the usage API, used only in direct mode. The shape changes on their side:
/// new keys with code names appear regularly, so anything unknown is ignored on purpose.
public sealed record UsageReport(IReadOnlyList<LimitWindow> Windows, ModelLimit? Model)
{
    public static readonly UsageReport Empty = new([], null);

    public bool HasData => Windows.Count > 0 || Model is not null;

    public static UsageReport FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            var windows = new List<LimitWindow>(2);
            AddWindow(windows, root, "five_hour", LimitKind.FiveHour);
            AddWindow(windows, root, "seven_day", LimitKind.SevenDay);

            var model = ReadModelLimit(root);
            return windows.Count > 0 || model is not null ? new UsageReport(windows, model) : Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    private static void AddWindow(List<LimitWindow> windows, JsonElement root, string name, LimitKind kind)
    {
        if (!root.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!window.TryGetProperty("utilization", out var percent) || percent.ValueKind != JsonValueKind.Number
            || !TryReadResetTime(window, out var resetsAt))
        {
            return;
        }

        windows.Add(new LimitWindow(kind, Percentage.FromDouble(percent.GetDouble()), resetsAt));
    }

    /// The weekly limit of the heavy model. notchi looks for it the same way: an entry of
    /// limits[] with kind "weekly_scoped" and a model in its scope.
    private static ModelLimit? ReadModelLimit(JsonElement root)
    {
        if (!root.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var limit in limits.EnumerateArray())
        {
            if (limit.ValueKind != JsonValueKind.Object
                || !limit.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String
                || kind.GetString() != "weekly_scoped"
                || !limit.TryGetProperty("percent", out var percent) || percent.ValueKind != JsonValueKind.Number
                || !TryReadResetTime(limit, out var resetsAt))
            {
                continue;
            }

            var name = limit.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object
                && scope.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object
                && model.TryGetProperty("display_name", out var displayName) && displayName.ValueKind == JsonValueKind.String
                    ? displayName.GetString() ?? "model"
                    : "model";

            return new ModelLimit(name, Percentage.FromDouble(percent.GetDouble()), resetsAt);
        }

        return null;
    }

    /// resets_at is ISO 8601 here, with microseconds and an offset: 2026-09-11T15:00:00.123456+00:00.
    /// The weekly_scoped entry comes without the fractional part, so both forms must parse.
    private static bool TryReadResetTime(JsonElement element, out DateTimeOffset resetsAt)
    {
        resetsAt = default;
        if (!element.TryGetProperty("resets_at", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            value.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out resetsAt);
    }
}
