import AikoKit
import Foundation

/// What the hint of a recording is made of: the session it starts in (D-313).
struct VoiceContext {
    let folder: String
    let files: [String]
}

/// What the field under the terminal does for voice input: the editor. The twin of IDictationField.
@MainActor
protocol DictationField: AnyObject {
    var voicePressed: (() -> Void)? { get set }
    var voiceCancelled: (() -> Void)? { get set }
    var voiceSettingsAsked: (() -> Void)? { get set }
    func showVoice(_ line: VoiceLine, _ notice: VoiceNotice)
    func pushLevel(_ level: Double)
    func beginDictation()
    func showDictation(_ text: DictationText)
    func endDictation(_ words: String?)
}

/// Voice input of the terminal window: the press, the mic, the recogniser and the field. The twin
/// of VoiceDictation.cs.
///
/// The rules are the core's (VoiceInput, DictationText); this class only moves sound and text
/// between threads. The mic reads on a thread of the audio system, the recogniser on a worker, and
/// every change to the field happens on the main thread. Nothing of it lives between recordings:
/// the mic is stopped and the model is freed.
@MainActor
final class VoiceDictation {
    private let field: DictationField
    private let context: () -> VoiceContext?
    private var timer: Timer?
    private var listenedSince: Date?
    private var state = VoiceState.forSetting(turnedOn: false)
    private var settings = AppSettings()
    private var recording: Recording?
    private var text = DictationText.empty

    /// Opens the page of the settings where voice input is set up.
    var settingsAsked: (() -> Void)? {
        get { field.voiceSettingsAsked }
        set { field.voiceSettingsAsked = newValue }
    }

    init(field: DictationField, context: @escaping () -> VoiceContext?) {
        self.field = field
        self.context = context
        field.voicePressed = { [weak self] in self?.press() }
        field.voiceCancelled = { [weak self] in self?.cue(.cancel) }
        show()
    }

    /// The settings changed: voice input was turned on or off, or the engine changed.
    func use(_ settings: AppSettings) {
        self.settings = settings
        if !state.recording && state.phase != .finishing {
            state = VoiceState.forSetting(turnedOn: settings.voiceInput)
        }
        show()
    }

    /// The window closes: whatever was being said is dropped.
    func close() {
        if state.phase != .off {
            cue(.cancel)
        }
    }

    private func readiness() -> VoiceReadiness {
        let ready = VoiceModel.readiness(settings.voiceEngine, modelBytes: ModelDownload.modelBytes, keyStored: VoiceKey.stored)
        // A model on disk is no use without the model of silence that ships with the app.
        return ready == .ready && settings.voiceEngine == .local && LocalRecognizer.vadModelPath == nil ? .needsModel : ready
    }

    private func press() {
        let before = state.phase
        state = VoiceInput.next(state, .press, before == .idle ? readiness() : .ready)

        if before == .idle && state.phase == .warming {
            start()
        } else if state.phase == .finishing {
            recording?.finish()
        }
        show()
    }

    private func cue(_ cue: VoiceCue) {
        let before = state.phase
        state = VoiceInput.next(state, cue, .ready)

        if cue == .firstSound && state.phase == .listening && before == .warming {
            listenedSince = Date()
        }

        if state.phase == .idle && before != .idle {
            // Esc, a lost mic, a refusal of OpenAI or the end: the recording is over either way. Only
            // Esc and a lost mic drop the words; a refusal keeps what came back before it (D-405).
            let dropped = !VoiceInput.keepsText(cue)
            recording?.drop()
            recording = nil
            field.endDictation(dropped ? nil : text.text)
        }
        show()
    }

    private func start() {
        let language = VoiceHint.language(settings.voiceLanguage, systemLanguage: Locale.preferredLanguages.first ?? "en")
        let hint = context().map {
            VoiceHint.build(folder: $0.folder, branch: Self.branch(of: $0.folder), files: $0.files, language: language)
        } ?? ""

        text = .empty
        listenedSince = nil
        field.beginDictation()
        let next = Recording(owner: self, engine: settings.voiceEngine, language: language, hint: hint)
        recording = next
        Task {
            // The first time macOS asks the person; the press waits for the answer. A check plays
            // a file and needs no microphone.
            let allowed = Recording.source != nil ? true : await Microphone.access()
            let opened = allowed && next.open()
            if !opened, next === recording {
                cue(.noMicrophone)
            }
        }
    }

    private func show() {
        let listened = listenedSince.map { Date().timeIntervalSince($0) } ?? 0
        field.showVoice(VoiceInput.line(state, listened: listened, engine: settings.voiceEngine), state.notice)
        let moving = state.recording
        if moving && timer == nil {
            // Only while a recording goes on: the wave follows the sound and the clock counts.
            timer = Timer.scheduledTimer(withTimeInterval: 0.08, repeats: true) { [weak self] _ in
                MainActor.assumeIsolated { self?.tick() }
            }
        } else if !moving, let running = timer {
            running.invalidate()
            timer = nil
        }
    }

    private func tick() {
        field.pushLevel(recording?.level ?? 0)
        show()
    }

    // ---- what the worker and the mic report, brought to the main thread ----

    fileprivate func heard(_ from: Recording) {
        if from === recording { cue(.firstSound) }
    }

    fileprivate func lost(_ from: Recording) {
        if from === recording { cue(.noMicrophone) }
    }

    fileprivate func passed(_ from: Recording, _ words: String, final: Bool) {
        guard from === recording else { return }
        text = final ? text.final(words) : text.pass(words)
        field.showDictation(text)
        if final { cue(.finished) }
    }

    /// Text from OpenAI: the phrases that are done and the one being read (D-317).
    fileprivate func phrased(_ from: Recording, done: String, reading: String, final: Bool) {
        guard from === recording else { return }
        text = DictationText.phrases(done: done, reading: reading)
        field.showDictation(text)
        if final { cue(.finished) }
    }

    /// The recording could not go on. A later one may already run: only its own end counts.
    fileprivate func gone(_ from: Recording) {
        if from === recording { cue(.cancel) }
    }

    /// OpenAI refused: why, as a cue (D-405). The phrases it already sent back stay.
    fileprivate func failed(_ from: Recording, _ why: VoiceCue) {
        if from === recording { cue(why) }
    }

    private static func branch(of folder: String) -> String? {
        let dotGit = (folder as NSString).appendingPathComponent(".git")
        var isFolder: ObjCBool = false
        guard FileManager.default.fileExists(atPath: dotGit, isDirectory: &isFolder) else { return nil }
        let gitFolder = isFolder.boolValue
            ? dotGit
            : (try? String(contentsOfFile: dotGit, encoding: .utf8)).flatMap { VoiceHint.gitFolder($0) }
        guard let gitFolder else { return nil }
        let head = ((gitFolder as NSString).isAbsolutePath ? gitFolder : (folder as NSString).appendingPathComponent(gitFolder)) + "/HEAD"
        return (try? String(contentsOfFile: head, encoding: .utf8)).flatMap { VoiceHint.branch($0) }
    }
}

/// One recording: the open mic and the worker that reads it. Dropped or finished, it is done; the
/// next press makes a new one, so a late answer of an old one is easy to tell apart.
final class Recording: @unchecked Sendable {
    /// The recogniser waits for this much sound before its first pass, and between passes when the
    /// last one came back at once (a pass over silence takes a few milliseconds with VAD).
    private static let firstSound = 0.5
    private static let breath = 0.25

    /// Set by --try-voice: a file plays in place of the microphone.
    @MainActor static var source: (() -> SoundSource)?

    private weak var owner: VoiceDictation?
    private let engine: VoiceEngine
    private let language: String
    private let hint: String
    private let mic: SoundSource
    private let lock = NSLock()
    private var finishing = false
    private var dropped = false

    @MainActor
    init(owner: VoiceDictation, engine: VoiceEngine, language: String, hint: String) {
        mic = Recording.source?() ?? Microphone()
        self.owner = owner
        self.engine = engine
        self.language = language
        self.hint = hint
    }

    var level: Double { mic.level }

    private var isFinishing: Bool { lock.withLock { finishing } }
    private var isDropped: Bool { lock.withLock { dropped } }

    func open() -> Bool {
        mic.firstSound = { [weak self] in self?.tell { $0.heard($1) } }
        mic.lost = { [weak self] in self?.tell { $0.lost($1) } }
        guard mic.start() else { return false }

        if engine == .openAI {
            Task.detached { [self] in await workWithOpenAI() }
        } else {
            Thread.detachNewThread { [self] in workOnComputer() }
        }
        return true
    }

    /// The second press: the mic stops now, the worker reads what was said to the end.
    func finish() {
        lock.withLock { finishing = true }
        mic.stop()
    }

    func drop() {
        lock.withLock { dropped = true }
        mic.stop()
    }

    /// A report for the dictation, on the main thread.
    private func tell(_ report: @escaping @MainActor (VoiceDictation, Recording) -> Void) {
        DispatchQueue.main.async { [self] in
            MainActor.assumeIsolated {
                if let owner { report(owner, self) }
            }
        }
    }

    private func workOnComputer() {
        guard let recognizer = LocalRecognizer.open(model: Store.folders.voiceModelFile, language: language, hint: hint) else {
            Log.write("voice input: the speech model did not load")
            tell { $0.gone($1) }
            return
        }

        while !isFinishing && !isDropped {
            let heard = mic.heardSoFar()
            if Double(heard.count) < Microphone.rate * Self.firstSound {
                Thread.sleep(forTimeInterval: Self.breath)
                continue
            }

            let started = Date()
            let words = unechoed(recognizer.read(heard), final: false)
            if !isFinishing && !isDropped {
                tell { $0.passed($1, words, final: false) }
            }
            let took = Date().timeIntervalSince(started)
            if took < Self.breath {
                Thread.sleep(forTimeInterval: Self.breath - took)
            }
        }

        if !isDropped {
            let words = unechoed(recognizer.read(mic.heardSoFar()), final: true)
            tell { $0.passed($1, words, final: true) }
        }
    }

    /// The sound goes to OpenAI as it comes, a tenth of a second at a time; the text comes back
    /// phrase by phrase (D-317).
    private func workWithOpenAI() async {
        var refused: Int?
        guard let cloud = await CloudRecognizer.open(hint: hint, refused: { refused = $0 }) else {
            let why = VoiceInput.cloudCue(code: nil, status: refused)
            tell { $0.failed($1, why) }
            return
        }
        defer { cloud.close() }

        cloud.changed = { [weak self] done, reading in
            guard let self, !self.isDropped else { return }
            self.tell { $0.phrased($1, done: done, reading: reading, final: false) }
        }

        var sent = 0
        while !isDropped && cloud.problem == nil {
            let finishing = isFinishing
            let heard = mic.heardSoFar()
            if heard.count > sent {
                await cloud.append(heard[sent...])
                sent = heard.count
            }
            if finishing { break }
            try? await Task.sleep(for: .milliseconds(100))
        }

        if isDropped { return }
        let words = cloud.problem == nil ? await cloud.finish() : nil
        if let words {
            tell { $0.phrased($1, done: words, reading: "", final: true) }
        } else {
            let why = VoiceInput.cloudCue(code: cloud.problem, status: nil)
            tell { $0.failed($1, why) }
        }
    }

    /// whisper may give the hint back over noise as well, alone or in the middle of speech; the echo
    /// is cut out. Every pass reads the whole recording again, so only the last one is logged.
    private func unechoed(_ words: String, final: Bool) -> String {
        let result = VoiceHint.withoutEcho(words, hint: hint)
        if final {
            VoiceEchoLog.write("Whisper", result)
        }
        return result.text
    }
}

/// What the echo filter of voice input did, in the log of Aiko (AIKO-380): numbers only, never the
/// words a person said (PRIVACY.md). The twin of VoiceEchoLog.cs.
enum VoiceEchoLog {
    static func write(_ engine: String, _ result: Unechoed) {
        for cut in result.dropped {
            Log.write("voice input: \(engine) gave the hint back, cut \(cut.parts) parts, \(cut.inHint) of them in the hint")
        }
        if result.lookAlikes > 0 {
            Log.write("voice input: \(engine) kept \(result.lookAlikes) sentence(s) with three or more items of the hint")
        }
    }
}
