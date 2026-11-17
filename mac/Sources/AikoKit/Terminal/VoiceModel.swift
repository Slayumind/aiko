import Foundation

/// The speech model of voice input and the person's own OpenAI key: where the model comes from,
/// when each engine is ready, and how a key is shown (D-313, D-314, D-316).
public enum VoiceModel {
    /// Our own release on GitHub: its hosts are already allowed, and the file is the one of
    /// whisper.cpp on Hugging Face, checked by the hash below (D-316).
    public static let url = "https://github.com/Slayumind/aiko/releases/download/voice-model-1/ggml-small.bin"

    public static let bytes: Int64 = 487_601_967

    public static let sha256 = "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b"

    /// The OpenAI model of D-314.
    public static let cloudModel = "gpt-4o-transcribe"

    /// Whole megabytes, as the settings page and the line above the field write them: 465.
    public static func megabytes(_ bytes: Int64) -> Int64 { max(0, bytes) / (1024 * 1024) }

    /// modelBytes is the size of the model file on disk, nil when there is none. A file of
    /// another size is a download that stopped half way.
    public static func readiness(_ engine: VoiceEngine, modelBytes: Int64?, keyStored: Bool) -> VoiceReadiness {
        switch engine {
        case .openAI: return keyStored ? .ready : .needsKey
        case .local: return modelBytes == bytes ? .ready : .needsModel
        }
    }

    /// An OpenAI key as people paste it: "sk-" and then letters, digits, "-" and "_". Spaces
    /// around it are trimmed; nil when it is not a key at all.
    public static func cleanKey(_ pasted: String?) -> String? {
        guard let key = pasted?.trimmingCharacters(in: .whitespacesAndNewlines),
              key.count >= 20, key.count <= 400, key.hasPrefix("sk-") else { return nil }
        let allowed = key.unicodeScalars.allSatisfy {
            ($0.isASCII && (CharacterSet.alphanumerics.contains($0))) || $0 == "-" || $0 == "_"
        }
        return allowed ? key : nil
    }

    /// A stored key is never shown whole: its kind and its last four characters, enough to tell
    /// two keys apart. "sk-proj-…" keeps "sk-proj-", any other keeps "sk-".
    public static func maskKey(_ key: String) -> String {
        let start = key.hasPrefix("sk-proj-") ? "sk-proj-" : "sk-"
        return key.count < start.count + 8 ? start + dots : start + dots + String(key.suffix(4))
    }

    private static let dots = "••••••••••"
}
