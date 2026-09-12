using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiko.Core;

/// Where Aiko sits: down by the clock, or in a small island at the edge of the screen.
public enum AikoPlace
{
    Tray,
    Island,
}

public enum AikoLanguage
{
    /// Follow Windows. The other two are a deliberate choice by the user.
    System,
    English,
    Russian,
}

/// What the settings window changes. Environments are not here: they live in their own file,
/// because the first run wizard writes them and this window does not.
public sealed record AppSettings
{
    public static readonly AppSettings Default = new();

    public AikoPlace Place { get; init; } = AikoPlace.Tray;

    /// On by default: a tray app that has to be started by hand is no use (decided 2026-09-12).
    public bool RunAtStartup { get; init; } = true;

    /// Off by default: checking asks slayumind.org and that is a connection the user agrees to.
    public bool CheckUpdates { get; init; }

    public AikoLanguage Language { get; init; } = AikoLanguage.System;

    /// Games, video and presentations take the whole screen, and the island would sit on top.
    public bool HideIslandInFullScreen { get; init; } = true;

    /// A settings file we cannot read is not a reason to stop. Aiko starts with the defaults, and
    /// the next save writes a clean file.
    public static AppSettings FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options) + Environment.NewLine;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Words, not numbers: a settings file is read by people too.
        Converters = { new JsonStringEnumConverter() },
    };
}
