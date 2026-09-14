using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiko.Core;

/// The little file the bridge writes and the tray reads: numbers and times, nothing else.
///
/// Claude Code hands the bridge much more than limits — the working folder, the transcript path,
/// the session id. None of that is kept: what is not written cannot leak.
public static class SnapshotFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(LimitSnapshot snapshot) =>
        JsonSerializer.Serialize(
            new File
            {
                Environment = snapshot.Environment,
                Source = snapshot.Source,
                ReceivedAt = snapshot.ReceivedAt,
                Windows = snapshot.Windows
                    .Select(w => new WindowFile { Kind = w.Kind, Percent = w.Percent, ResetsAt = w.ResetsAt })
                    .ToList(),
                Model = snapshot.Model is { } model
                    ? new ModelFile { Name = model.ModelName, Percent = model.Percent, ResetsAt = model.ResetsAt }
                    : null,
            },
            Options) + System.Environment.NewLine;

    /// The tray may read the file while the bridge is writing the next one, so anything
    /// unreadable simply means "nothing new yet".
    public static LimitSnapshot FromJson(string json, string fallbackEnvironment = "")
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LimitSnapshot.NoData(fallbackEnvironment);
        }

        try
        {
            var file = JsonSerializer.Deserialize<File>(json, Options);
            if (file?.Windows is null || file.Windows.Count == 0)
            {
                return LimitSnapshot.NoData(file?.Environment ?? fallbackEnvironment);
            }

            var windows = file.Windows
                .Select(w => new LimitWindow(w.Kind, w.Percent, w.ResetsAt))
                .ToList();

            return new LimitSnapshot(
                file.Environment ?? fallbackEnvironment,
                file.Source,
                file.ReceivedAt,
                windows)
            {
                Model = file.Model is { } model ? new ModelLimit(model.Name ?? "model", model.Percent, model.ResetsAt) : null,
            };
        }
        catch (JsonException)
        {
            return LimitSnapshot.NoData(fallbackEnvironment);
        }
    }

    private sealed class File
    {
        public string? Environment { get; set; }
        public LimitSource Source { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public List<WindowFile>? Windows { get; set; }
        public ModelFile? Model { get; set; }
    }

    private sealed class WindowFile
    {
        public LimitKind Kind { get; set; }
        public int Percent { get; set; }
        public DateTimeOffset ResetsAt { get; set; }
    }

    private sealed class ModelFile
    {
        public string? Name { get; set; }
        public int Percent { get; set; }
        public DateTimeOffset ResetsAt { get; set; }
    }
}
