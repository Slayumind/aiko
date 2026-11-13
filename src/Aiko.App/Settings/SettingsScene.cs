using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What a snapshot asks the settings pages to draw (D-420). Only Aiko.Checks sets these; in Aiko they
/// stay empty, and the pages show the person's real state. They live apart from the pages, so a
/// check can set them before WPF has started.
static class SettingsScene
{
    /// The update line at this stage, with a made-up version and no request for its notes.
    internal static UpdateStage? Update { get; set; }

    /// The memory drawn from this file in place of the person's own, so a picture for the site never
    /// shows real facts. With it the memory block only draws: nothing is written.
    internal static string? MemoryFile { get; set; }

    /// The memory block open, editing, adding or erasing, so every state can be looked at.
    internal static string? MemoryState { get; set; }

    /// The voice page with this engine chosen, without saving it.
    internal static VoiceEngine? VoiceEngine { get; set; }
}
