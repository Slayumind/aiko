import AikoKit
import Foundation

/// Voice input with the person's own OpenAI key (D-317): the Realtime API in transcription mode.
/// The twin of CloudRecognizer.cs.
///
/// The sound goes in pieces of a tenth of a second while the person speaks; the server cuts it at
/// pauses and sends the text of each phrase when the phrase is over. The text never goes to the
/// log; the key is read from the Keychain right before the socket opens and goes nowhere but the
/// header of that request.
final class CloudRecognizer: @unchecked Sendable {
    private static let address = "wss://api.openai.com/v1/realtime?intent=transcription"

    /// How long the last phrase may take after the second press (0.9 s in the spike).
    private static let lastPhrase: TimeInterval = 10

    private let socket: CloudSocket
    private let hint: String
    private let lock = NSLock()
    private var done = ""
    private var reading = ""
    private var speaking = false
    private var waiting = 0
    private var finishing = false
    private var allIn = false
    private var closing = false
    private var failure: String?

    /// The phrases that are done and the one being read, on a thread of the system.
    var changed: (@Sendable (String, String) -> Void)?

    /// Set when OpenAI refused or the connection broke: a short reason for the log, never the text.
    var problem: String? { lock.withLock { failure } }

    private init(socket: CloudSocket, hint: String) {
        self.socket = socket
        self.hint = hint
    }

    /// Nil when no key is stored or the socket did not open; refused hears the HTTP status when
    /// OpenAI answered the handshake with an error.
    ///
    /// No language is named. With a language and a hint, gpt-4o-transcribe translated an English
    /// sentence into Russian, or answered with the hint itself (D-318).
    static func open(hint: String, refused: (Int) -> Void) async -> CloudRecognizer? {
        guard let bearer = VoiceKey.load() else { return nil }
        let (socket, status) = await AikoHttp.socket(address, bearer: bearer)
        guard let socket else {
            Log.write(status > 0 ? "voice input: OpenAI refused the connection: \(status)" : "voice input: OpenAI did not open")
            if status > 0 { refused(status) }
            return nil
        }

        let recognizer = CloudRecognizer(socket: socket, hint: hint)
        recognizer.listen()
        await recognizer.send([
            "type": "session.update",
            "session": [
                "type": "transcription",
                "audio": [
                    "input": [
                        "format": ["type": "audio/pcm", "rate": 24000],
                        "transcription": ["model": VoiceModel.cloudModel, "prompt": hint],
                        "turn_detection": [
                            "type": "server_vad", "threshold": 0.5, "prefix_padding_ms": 300, "silence_duration_ms": 500,
                        ] as [String: Any],
                    ] as [String: Any],
                ],
            ] as [String: Any],
        ])
        return recognizer
    }

    /// Sound from the mic, 16 kHz: OpenAI takes 16-bit PCM at 24 kHz.
    func append(_ sound: ArraySlice<Float>) async {
        await send(["type": "input_audio_buffer.append", "audio": Self.to24k(sound).base64EncodedString()])
    }

    /// After the second press: the phrase still being spoken is cut, and the text of every phrase
    /// is awaited. Nil when OpenAI failed.
    func finish() async -> String? {
        let cut = lock.withLock { () -> Bool in
            finishing = true
            if !speaking && waiting == 0 { allIn = true }
            return speaking
        }
        if cut {
            await send(["type": "input_audio_buffer.commit"])
        }

        let end = Date().addingTimeInterval(Self.lastPhrase)
        while Date() < end, !lock.withLock({ allIn }) {
            try? await Task.sleep(for: .milliseconds(50))
        }
        return lock.withLock {
            failure == nil && allIn ? done.trimmingCharacters(in: .whitespaces) : nil
        }
    }

    func close() {
        lock.withLock { closing = true }
        socket.close()
    }

    private func send(_ message: [String: Any]) async {
        guard let data = try? JSONSerialization.data(withJSONObject: message),
              let text = String(data: data, encoding: .utf8) else { return }
        do {
            try await socket.send(text)
        } catch {
            fail("send")
        }
    }

    private func listen() {
        Task.detached { [self] in
            while true {
                do {
                    guard let message = try await socket.receive() else { return }
                    read(message)
                } catch {
                    if !lock.withLock({ closing }) { fail("closed") }
                    return
                }
            }
        }
    }

    private func read(_ json: Data) {
        guard let root = try? JSONSerialization.jsonObject(with: json) as? [String: Any] else { return }
        let type = root["type"] as? String
        let shown: (String, String)? = lock.withLock {
            switch type {
            case "input_audio_buffer.speech_started":
                speaking = true
            case "input_audio_buffer.speech_stopped":
                speaking = false
            case "input_audio_buffer.committed":
                speaking = false
                waiting += 1
            case "conversation.item.input_audio_transcription.delta":
                reading += root["delta"] as? String ?? ""
            case "conversation.item.input_audio_transcription.completed":
                let phrase = VoiceHint.withoutEcho(root["transcript"] as? String ?? "", hint: hint)
                VoiceEchoLog.write("OpenAI", phrase)
                if !phrase.text.isEmpty {
                    done += phrase.text + " "
                }
                reading = ""
                waiting = max(0, waiting - 1)
                if finishing && waiting == 0 { allIn = true }
            case "conversation.item.input_audio_transcription.failed", "error":
                let code = (root["error"] as? [String: Any])?["code"] as? String ?? type ?? "error"
                // The server had cut the last phrase itself a moment before the commit.
                if code == "input_audio_buffer_commit_empty" {
                    if waiting == 0 { allIn = true }
                    return nil
                }
                failure = failure ?? code
                allIn = true
                Log.write("voice input: OpenAI failed: \(code)")
                return nil
            default:
                return nil
            }
            return (done, reading)
        }
        if let shown {
            changed?(shown.0, shown.1)
        }
    }

    private func fail(_ reason: String) {
        lock.withLock {
            failure = failure ?? reason
            allIn = true
        }
        Log.write("voice input: OpenAI failed: \(reason)")
    }

    /// 16 kHz to 24 kHz by straight lines between samples, and to 16-bit little endian.
    static func to24k(_ sound: ArraySlice<Float>) -> Data {
        let samples = Array(sound)
        guard !samples.isEmpty else { return Data() }
        let count = samples.count * 3 / 2
        var bytes = Data(count: count * 2)
        bytes.withUnsafeMutableBytes { raw in
            let out = raw.bindMemory(to: Int16.self)
            for index in 0..<count {
                let x = Double(index) / 1.5
                let low = Int(x)
                let part = Float(x - Double(low))
                let a = samples[min(low, samples.count - 1)]
                let b = samples[min(low + 1, samples.count - 1)]
                let value = max(-32768, min(32767, (a * (1 - part) + b * part) * 32767))
                out[index] = Int16(value).littleEndian
            }
        }
        return bytes
    }
}
