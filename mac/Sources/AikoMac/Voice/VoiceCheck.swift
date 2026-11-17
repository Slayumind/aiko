@preconcurrency import AVFoundation
import AikoKit
import AppKit

/// --try-voice: voice input from the key to the words in the field, with a recording in place of
/// the microphone, named by the argument after --try-voice; the speech model must already be on the disk. A
/// command line is started in a temporary folder, nothing is saved, and the words heard never go to
/// the log: the lines say only what happened.
@MainActor
enum VoiceCheck {
    static func run(_ host: TerminalHost, wav: String?) {
        guard let wav, let sound = FileSound.read(wav) else {
            Log.write("self test: voice SKIP: --try-voice names no file that reads")
            NSApp.terminate(nil)
            return
        }
        Recording.source = { FileSound(sound) }

        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-voice")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        host.show()
        let id = host.startForCheck(in: folder, kind: .shell)
        var settings = Store.settings()
        settings.voiceInput = true
        settings.voiceEngine = .local
        host.voice?.use(settings)
        let editor = host.editor

        Task { @MainActor in
            check("the model is on the disk", ModelDownload.modelBytes == VoiceModel.bytes)
            check("the mic button stands in the editor", !editor.mic.isHidden && editor.mic.frame.width > 10)
            try? await Task.sleep(for: .seconds(1))

            editor.setText("", caret: 0)
            _ = host.pressForCheck(49, shift: true)
            check("Cmd+Shift+Space starts a recording", editor.dictating && editor.mic.recording)
            let listening = await until(3) { editor.voiceWord.textColor == Theme.nsDestructive }
            check("the first sound turns warming into listening, with the wave", listening && !editor.wave.isHidden,
                  editor.voiceWord.stringValue)

            // While the recording plays, the words come into the field before the end (D-354).
            let playing = Double(sound.count) / Microphone.rate
            let early = await until(playing + 0.5) { !editor.text.string.isEmpty }
            check("words show in the field while the person speaks", early)
            try? await Task.sleep(for: .seconds(max(0, playing - 0.5)))

            _ = host.pressForCheck(49, shift: true)
            let ended = await until(15) { !editor.dictating }
            let words = editor.text.string.split(separator: " ").count
            check("the second press ends it and the words stay in the field", ended && words >= 4, "\(words) words")
            check("the field is editable again and the line under it is back",
                  editor.text.isEditable && !editor.hint.isHidden && editor.voiceLine.isHidden)

            editor.setText("kept", caret: 4)
            _ = host.pressForCheck(49, shift: true)
            try? await Task.sleep(for: .seconds(1.5))
            _ = editor.press(53, [])
            let cancelled = await until(3) { !editor.dictating }
            check("Esc drops the recording and keeps what was typed", cancelled && editor.text.string == "kept",
                  editor.text.string)
            check("no line above the field", editor.noticeBar.isHidden)

            if let id { host.closeSession(id) }
            host.closeForCheck()
            try? await Task.sleep(for: .milliseconds(500))
            NSApp.terminate(nil)
        }
    }

    private static func check(_ what: String, _ ok: Bool, _ seen: String = "") {
        Log.write("self test: voice \(ok ? "ok" : "FAIL") \(what)\(seen.isEmpty ? "" : " [\(seen)]")")
    }

    private static func until(_ seconds: Double, _ done: @MainActor () -> Bool) async -> Bool {
        let end = Date().addingTimeInterval(seconds)
        while Date() < end {
            if done() { return true }
            try? await Task.sleep(for: .milliseconds(100))
        }
        return done()
    }
}

/// A recording played as if it came from the microphone: a tenth of a second at a time, in real
/// time, then silence.
final class FileSound: SoundSource, @unchecked Sendable {
    private let sound: [Float]
    private let lock = NSLock()
    private var played = 0
    private var loudness = 0.0
    private var heard = false
    private var timer: DispatchSourceTimer?

    var firstSound: (@Sendable () -> Void)?
    var lost: (@Sendable () -> Void)?

    init(_ sound: [Float]) {
        self.sound = sound
    }

    var level: Double { lock.withLock { loudness } }

    func start() -> Bool {
        let timer = DispatchSource.makeTimerSource(queue: .global(qos: .userInitiated))
        timer.schedule(deadline: .now() + 0.1, repeating: 0.1)
        timer.setEventHandler { [weak self] in self?.tick() }
        self.timer = timer
        timer.resume()
        return true
    }

    func heardSoFar() -> [Float] {
        lock.withLock { Array(sound.prefix(played)) }
    }

    func stop() {
        timer?.cancel()
        timer = nil
    }

    private func tick() {
        let step = Int(Microphone.rate / 10)
        let first = lock.withLock { () -> Bool in
            let piece = sound[min(played, sound.count)..<min(played + step, sound.count)]
            played += step
            let energy = piece.reduce(0.0) { $0 + Double($1) * Double($1) }
            loudness = piece.isEmpty ? 0 : min(1, (energy / Double(piece.count)).squareRoot() * 8)
            if !heard, piece.contains(where: { abs($0) > 2.0 / 32768 }) {
                heard = true
                return true
            }
            return false
        }
        if first { firstSound?() }
    }

    /// A WAV file as 16 kHz mono samples.
    static func read(_ path: String) -> [Float]? {
        guard let file = try? AVAudioFile(forReading: URL(fileURLWithPath: path)),
              let target = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Microphone.rate, channels: 1, interleaved: false),
              let source = AVAudioPCMBuffer(pcmFormat: file.processingFormat, frameCapacity: AVAudioFrameCount(file.length)),
              (try? file.read(into: source)) != nil,
              let converter = AVAudioConverter(from: file.processingFormat, to: target) else { return nil }
        let capacity = AVAudioFrameCount(Double(file.length) * Microphone.rate / file.processingFormat.sampleRate) + 1024
        guard let out = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: capacity) else { return nil }
        let given = Given()
        var error: NSError?
        converter.convert(to: out, error: &error) { _, status in
            if given.done {
                status.pointee = .endOfStream
                return nil
            }
            given.done = true
            status.pointee = .haveData
            return source
        }
        guard error == nil, let channel = out.floatChannelData?[0] else { return nil }
        return Array(UnsafeBufferPointer(start: channel, count: Int(out.frameLength)))
    }

    private final class Given: @unchecked Sendable {
        var done = false
    }
}
