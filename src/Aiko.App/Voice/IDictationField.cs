using Aiko.Core.Terminal;

namespace Aiko.App;

/// The field voice input writes into: the editor under the terminal (D-354). Voice input only
/// talks to the field through these, so the recording does not depend on how the field is drawn.
interface IDictationField
{
    /// The thread of the window the field is in.
    System.Windows.Threading.Dispatcher Dispatcher { get; }

    /// The key or the button of voice input was pressed.
    event Action? VoicePressed;

    /// The person cancelled the recording.
    event Action? VoiceCancelled;

    /// The person asked for the settings of voice input from the field.
    event Action? VoiceSettingsAsked;

    /// The line under the field while voice input works, and a notice above it.
    void ShowVoice(VoiceLine line, VoiceNotice notice);

    /// The loudness of the last moment, from 0 to 1, for the wave.
    void PushLevel(double level);

    void BeginDictation();

    /// The words heard so far: the part that will not change and the part that still may.
    void ShowDictation(DictationText dictation);

    /// The recording is over. Null when it was dropped and the field gets nothing.
    void EndDictation(string? words);
}
