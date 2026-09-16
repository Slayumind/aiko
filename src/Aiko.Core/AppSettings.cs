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
    /// The shape of the file. Nothing reads it yet. It is written from the very first version
    /// because a version number added later says nothing about the files already on disk, and
    /// renaming a field would then have no safe way back.
    /// 2 added MeetAikoShown. A file of schema 1 comes from 0.1, so it reads as not shown yet.
    /// 3 split the counting off the update check. A file of schema 2 reads as "not asked, not
    /// sending": the consent given to the old single switch covered a smaller thing than 0.2.1
    /// sends, so it is asked again rather than carried over.
    public const int CurrentSchema = 3;

    public static readonly AppSettings Default = new();

    public int SchemaVersion { get; init; } = CurrentSchema;

    public AikoPlace Place { get; init; } = AikoPlace.Tray;

    /// On by default: a tray app that has to be started by hand is no use (decided 2026-09-12).
    public bool RunAtStartup { get; init; } = true;

    /// Off by default: checking asks slayumind.org and that is a connection the user agrees to.
    public bool CheckUpdates { get; init; }

    /// Off by default, and its own switch since 0.2.1. It used to ride on CheckUpdates, so turning
    /// off the count meant turning off update checks with it, and there was no way to keep one
    /// without the other. The request is still one request: with this off, the identifier and the
    /// three flags are simply left out of it and the server writes nothing.
    public bool SendStats { get; init; }

    /// Whether the privacy question has been answered at all. Without it, "off" cannot be told
    /// apart from "never asked", and somebody updating from 0.2.0 would never see the question.
    public bool PrivacyAsked { get; init; }

    public AikoLanguage Language { get; init; } = AikoLanguage.System;

    /// Games, video and presentations take the whole screen, and the island would sit on top.
    public bool HideIslandInFullScreen { get; init; } = true;

    /// Where the island was left. Kept as an edge and a share along it, so it survives a change
    /// of screen size.
    public IslandPosition Island { get; init; } = IslandPosition.Default;

    /// The checklist opens once on "Meet Aiko" for people who update from 0.1 (D-200).
    public bool MeetAikoShown { get; init; }

    /// A settings file we cannot read is not a reason to stop: Aiko starts with the defaults.
    /// Putting the unreadable file aside before that happens is the store's job, so that the next
    /// save does not quietly write over choices somebody may still want back.
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
