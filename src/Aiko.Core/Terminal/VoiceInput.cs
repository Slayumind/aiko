namespace Aiko.Core.Terminal;

/// Who turns speech into text (D-314): whisper small on this computer, or OpenAI with the
/// person's own key.
public enum VoiceEngine
{
    Local,
    OpenAI,
}

/// Where a recording is. Off means voice input is turned off in the settings: no button, and
/// Ctrl+Space does nothing.
public enum VoicePhase
{
    Off,
    Idle,

    /// The mic is open but no sound came yet. The Windows mic gives silence for the first 0.4 s,
    /// so "listening" is shown only from the first sound, and the first word is not lost (D-314).
    Warming,
    Listening,

    /// The second press closed the mic. The last words are still being recognised.
    Finishing,
}

/// The line above the input that voice input may show.
public enum VoiceNotice
{
    None,
    NoMicrophone,
    NeedsModel,
    NeedsKey,
}

/// What happened: a press of Ctrl+Space or the button, Esc, or news from the mic and the engine.
public enum VoiceCue
{
    Press,
    FirstSound,
    Cancel,
    Finished,
    NoMicrophone,
    MicrophoneBack,
}

/// Whether the chosen engine can work: the model is on disk, or the key is stored.
public enum VoiceReadiness
{
    Ready,
    NeedsModel,
    NeedsKey,
}

public sealed record VoiceState(VoicePhase Phase, VoiceNotice Notice)
{
    public static VoiceState For(bool turnedOn) =>
        new(turnedOn ? VoicePhase.Idle : VoicePhase.Off, VoiceNotice.None);

    public bool Recording => Phase is VoicePhase.Warming or VoicePhase.Listening;
}

public enum VoiceButton
{
    Hidden,
    Idle,

    /// Round and tinted red, from the press until the second press (D-315).
    Recording,
}

public enum VoiceWord
{
    /// The line under the input shows the mode, the model and the environment as usual.
    None,
    Warming,
    Listening,
    Finishing,
}

/// What the input shows for one moment of a recording (D-315). Time is set only while listening;
/// Engine names where the sound goes while the mic is open or the text is finishing.
public sealed record VoiceLine(
    VoiceWord Word,
    string? Time,
    VoiceEngine? Engine,
    bool CancelHint,
    VoiceButton Button,
    bool Ring);

/// The rules of voice input in the input field (D-313, D-315). Two presses: press, speak, press.
public static class VoiceInput
{
    public static VoiceState Next(VoiceState state, VoiceCue cue, VoiceReadiness readiness)
    {
        if (state.Phase == VoicePhase.Off)
        {
            return state;
        }

        return (state.Phase, cue) switch
        {
            (VoicePhase.Idle, VoiceCue.Press) => readiness switch
            {
                VoiceReadiness.NeedsModel => state with { Notice = VoiceNotice.NeedsModel },
                VoiceReadiness.NeedsKey => state with { Notice = VoiceNotice.NeedsKey },
                _ => new VoiceState(VoicePhase.Warming, VoiceNotice.None),
            },
            (VoicePhase.Warming, VoiceCue.FirstSound) => state with { Phase = VoicePhase.Listening },
            (VoicePhase.Warming or VoicePhase.Listening, VoiceCue.Press) => state with { Phase = VoicePhase.Finishing },
            (VoicePhase.Warming or VoicePhase.Listening or VoicePhase.Finishing, VoiceCue.Cancel) =>
                state with { Phase = VoicePhase.Idle },
            (VoicePhase.Finishing, VoiceCue.Finished) => state with { Phase = VoicePhase.Idle },
            (VoicePhase.Warming or VoicePhase.Listening, VoiceCue.NoMicrophone) =>
                new VoiceState(VoicePhase.Idle, VoiceNotice.NoMicrophone),
            (_, VoiceCue.NoMicrophone) => state with { Notice = VoiceNotice.NoMicrophone },
            (_, VoiceCue.MicrophoneBack) when state.Notice == VoiceNotice.NoMicrophone =>
                state with { Notice = VoiceNotice.None },
            _ => state,
        };
    }

    public static VoiceLine Line(VoiceState state, TimeSpan listened, VoiceEngine engine) => state.Phase switch
    {
        VoicePhase.Off => new VoiceLine(VoiceWord.None, null, null, false, VoiceButton.Hidden, false),
        VoicePhase.Warming => new VoiceLine(VoiceWord.Warming, null, engine, true, VoiceButton.Recording, false),
        VoicePhase.Listening => new VoiceLine(VoiceWord.Listening, Clock(listened), engine, true, VoiceButton.Recording, true),
        VoicePhase.Finishing => new VoiceLine(VoiceWord.Finishing, null, engine, false, VoiceButton.Idle, false),
        _ => new VoiceLine(VoiceWord.None, null, null, false, VoiceButton.Idle, false),
    };

    /// Minutes and seconds, the way a voice recorder counts: 0:04, 12:30.
    public static string Clock(TimeSpan time)
    {
        var seconds = Math.Max(0, (long)time.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
