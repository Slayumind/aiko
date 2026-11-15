import Foundation
import whisper

/// whisper small on this Mac (D-314, D-427), on Metal. The twin of LocalRecognizer.cs, with the
/// same settings: greedy, the language and the hint, the model of silence. Loaded on the press and
/// freed when the recording is over: the model holds about 770 MB.
///
/// The first load on a Mac takes about 15 s while Metal builds its shaders; macOS keeps them, and
/// later loads take a quarter of a second (spike 2026-11-15).
final class LocalRecognizer: @unchecked Sendable {
    private let context: OpaquePointer
    private let language: String
    private let hint: String
    private let vadModel: String

    private init(context: OpaquePointer, language: String, hint: String, vadModel: String) {
        self.context = context
        self.language = language
        self.hint = hint
        self.vadModel = vadModel
    }

    deinit {
        whisper_free(context)
    }

    /// The model of silence ships inside the app; without it silence turns into "[Музыка]".
    static var vadModelPath: String? {
        Bundle.main.path(forResource: "ggml-silero-v5.1.2", ofType: "bin")
    }

    /// Nil when the model or the model of silence is missing, or whisper cannot load it.
    static func open(model: String, language: String, hint: String) -> LocalRecognizer? {
        guard FileManager.default.fileExists(atPath: model), let vad = vadModelPath else { return nil }
        whisper_log_set({ _, _, _ in }, nil)
        var parameters = whisper_context_default_params()
        parameters.use_gpu = true
        let started = Date()
        guard let context = whisper_init_from_file_with_params(model, parameters) else { return nil }
        Log.write(String(format: "voice input: the speech model loaded in %.1f s", Date().timeIntervalSince(started)))
        return LocalRecognizer(context: context, language: language, hint: hint, vadModel: vad)
    }

    /// The text of all the sound given. Takes under a second for a phrase on Metal; call it off
    /// the main thread, and not from two threads at once.
    func read(_ samples: [Float]) -> String {
        var parameters = whisper_full_default_params(WHISPER_SAMPLING_GREEDY)
        parameters.n_threads = Int32(min(max(ProcessInfo.processInfo.activeProcessorCount / 2, 1), 8))
        parameters.no_timestamps = true
        parameters.print_progress = false
        parameters.print_realtime = false
        parameters.print_timestamps = false
        parameters.vad = true

        let result: Int32 = language.withCString { languageText in
            hint.withCString { hintText in
                vadModel.withCString { vadText in
                    parameters.language = languageText
                    parameters.initial_prompt = hint.isEmpty ? nil : hintText
                    parameters.vad_model_path = vadText
                    return samples.withUnsafeBufferPointer {
                        whisper_full(context, parameters, $0.baseAddress, Int32($0.count))
                    }
                }
            }
        }
        guard result == 0 else { return "" }

        var text = ""
        for segment in 0..<whisper_full_n_segments(context) {
            if let piece = whisper_full_get_segment_text(context, segment) {
                text += String(cString: piece)
            }
        }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
