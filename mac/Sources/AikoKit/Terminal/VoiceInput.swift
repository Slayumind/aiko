import Foundation

/// Who turns speech into text (D-314): whisper small on this computer, or OpenAI with the
/// person's own key.
public enum VoiceEngine: String, Sendable, Equatable, CaseIterable {
    case local = "Local"
    case openAI = "OpenAI"
}

/// Where a recording is. Off means voice input is turned off in the settings: no button, and
/// Ctrl+Space does nothing.
public enum VoicePhase: String, Sendable, Equatable {
    case off = "Off"
    case idle = "Idle"

    /// The mic is open but no sound came yet. The Windows mic gives silence for the first 0.4 s,
    /// so "listening" is shown only from the first sound, and the first word is not lost (D-314).
    case warming = "Warming"
    case listening = "Listening"

    /// The second press closed the mic. The last words are still being recognised.
    case finishing = "Finishing"
}

/// The line above the input that voice input may show.
public enum VoiceNotice: String, Sendable, Equatable {
    case none = "None"
    case noMicrophone = "NoMicrophone"
    case needsModel = "NeedsModel"
    case needsKey = "NeedsKey"

    /// OpenAI did not take the recording: a wrong key, no money on it, no connection (D-317).
    case cloudFailed = "CloudFailed"
}

/// What happened: a press of Ctrl+Space or the button, Esc, or news from the mic and the engine.
public enum VoiceCue: String, Sendable, Equatable {
    case press = "Press"
    case firstSound = "FirstSound"
    case cancel = "Cancel"
    case finished = "Finished"
    case noMicrophone = "NoMicrophone"
    case microphoneBack = "MicrophoneBack"
    case cloudFailed = "CloudFailed"
}

/// Whether the chosen engine can work: the model is on disk, or the key is stored.
public enum VoiceReadiness: String, Sendable, Equatable {
    case ready = "Ready"
    case needsModel = "NeedsModel"
    case needsKey = "NeedsKey"
}

public struct VoiceState: Sendable, Equatable {
    public var phase: VoicePhase
    public var notice: VoiceNotice

    public init(_ phase: VoicePhase, _ notice: VoiceNotice) {
        self.phase = phase
        self.notice = notice
    }

    public static func forSetting(turnedOn: Bool) -> VoiceState {
        VoiceState(turnedOn ? .idle : .off, .none)
    }

    public var recording: Bool { phase == .warming || phase == .listening }
}

public enum VoiceButton: String, Sendable, Equatable {
    case hidden = "Hidden"
    case idle = "Idle"

    /// Round and tinted red, from the press until the second press (D-315).
    case recording = "Recording"
}

public enum VoiceWord: String, Sendable, Equatable {
    /// The line under the input shows the mode, the model and the environment as usual.
    case none = "None"
    case warming = "Warming"
    case listening = "Listening"
    case finishing = "Finishing"
}

/// What the input shows for one moment of a recording (D-315, D-319). time and the wave of the sound
/// show only while listening;
/// engine names where the sound goes while the mic is open or the text is finishing.
public struct VoiceLine: Sendable, Equatable {
    public var word: VoiceWord
    public var time: String?
    public var engine: VoiceEngine?
    public var cancelHint: Bool
    public var button: VoiceButton
    public var wave: Bool

    public init(_ word: VoiceWord, _ time: String?, _ engine: VoiceEngine?, _ cancelHint: Bool, _ button: VoiceButton, _ wave: Bool) {
        self.word = word
        self.time = time
        self.engine = engine
        self.cancelHint = cancelHint
        self.button = button
        self.wave = wave
    }
}

/// The rules of voice input in the input field (D-313, D-315). Two presses: press, speak, press.
public enum VoiceInput {
    public static func next(_ state: VoiceState, _ cue: VoiceCue, _ readiness: VoiceReadiness) -> VoiceState {
        if state.phase == .off { return state }

        switch (state.phase, cue) {
        case (.idle, .press):
            switch readiness {
            case .needsModel: return VoiceState(.idle, .needsModel)
            case .needsKey: return VoiceState(.idle, .needsKey)
            case .ready: return VoiceState(.warming, .none)
            }
        case (.warming, .firstSound):
            return VoiceState(.listening, state.notice)
        case (.warming, .press), (.listening, .press):
            return VoiceState(.finishing, state.notice)
        case (.warming, .cancel), (.listening, .cancel), (.finishing, .cancel):
            return VoiceState(.idle, state.notice)
        case (.finishing, .finished):
            return VoiceState(.idle, state.notice)
        case (.warming, .noMicrophone), (.listening, .noMicrophone):
            return VoiceState(.idle, .noMicrophone)
        case (_, .noMicrophone):
            return VoiceState(state.phase, .noMicrophone)
        case (.warming, .cloudFailed), (.listening, .cloudFailed), (.finishing, .cloudFailed):
            return VoiceState(.idle, .cloudFailed)
        case (_, .microphoneBack) where state.notice == .noMicrophone:
            return VoiceState(state.phase, .none)
        default:
            return state
        }
    }

    public static func line(_ state: VoiceState, listened: TimeInterval, engine: VoiceEngine) -> VoiceLine {
        switch state.phase {
        case .off: return VoiceLine(.none, nil, nil, false, .hidden, false)
        case .warming: return VoiceLine(.warming, nil, engine, true, .recording, false)
        case .listening: return VoiceLine(.listening, clock(listened), engine, true, .recording, true)
        case .finishing: return VoiceLine(.finishing, nil, engine, false, .idle, false)
        case .idle: return VoiceLine(.none, nil, nil, false, .idle, false)
        }
    }

    /// Minutes and seconds, the way a voice recorder counts: 0:04, 12:30.
    public static func clock(_ time: TimeInterval) -> String {
        let seconds = max(0, Int64(time.rounded(.towardZero)))
        return "\(seconds / 60):" + String(format: "%02lld", seconds % 60)
    }
}
