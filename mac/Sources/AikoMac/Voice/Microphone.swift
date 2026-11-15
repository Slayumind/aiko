// AVAudioPCMBuffer is not marked Sendable; the tap hands each buffer to one call and keeps no
// other reference to it.
@preconcurrency import AVFoundation

/// The default microphone of the Mac while a recording goes on, turned into 16 kHz, one channel,
/// the format whisper reads. The twin of Microphone.cs. It is made on the press and stopped on the
/// second press; between recordings nothing of it exists (Aiko is not seen by the system).
///
/// AVAudioEngine gives the sound in the format of the device, so a converter makes it 16 kHz. Its
/// tap runs on a thread of the audio system; everything it touches is behind a lock.
/// Where the sound of a recording comes from: the microphone, or a file for --try-voice.
protocol SoundSource: AnyObject, Sendable {
    var firstSound: (@Sendable () -> Void)? { get set }
    var lost: (@Sendable () -> Void)? { get set }
    var level: Double { get }
    func start() -> Bool
    func heardSoFar() -> [Float]
    func stop()
}

final class Microphone: SoundSource, @unchecked Sendable {
    static let rate = 16000.0

    /// Below this a sample is digital silence. Windows gives exact zeros for about 0.4 s after the
    /// mic opens (D-314); "listening" waits for the first real sound on the Mac as well.
    private static let silence: Float = 2.0 / 32768

    /// A device that gives no sound for this long went away: unplugged mid-sentence.
    private static let quietFor: TimeInterval = 2

    private let engine = AVAudioEngine()
    private let lock = NSLock()
    private var samples: [Float] = []
    private var heard = false
    private var loudness = 0.0
    private var lastSound = Date()
    private var stopped = false
    private var lostTold = false
    private var converter: AVAudioConverter?
    private var target: AVAudioFormat?
    private var watch: DispatchSourceTimer?

    /// Raised once, on a thread of the audio system, when the first sound that is not silence comes.
    var firstSound: (@Sendable () -> Void)?

    /// Raised once when the device stops giving sound.
    var lost: (@Sendable () -> Void)?

    /// How loud the last moment was, from 0 to 1.
    var level: Double { lock.withLock { loudness } }

    /// Whether Aiko may use the microphone. The first time macOS asks the person, with the words of
    /// VoiceMicPurpose; later it answers at once.
    static func access() async -> Bool {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return true
        case .notDetermined: return await AVCaptureDevice.requestAccess(for: .audio)
        default: return false
        }
    }

    /// False when there is no microphone or it would not start.
    func start() -> Bool {
        let input = engine.inputNode
        let format = input.outputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0,
              let target = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Self.rate, channels: 1, interleaved: false),
              let converter = AVAudioConverter(from: format, to: target) else { return false }
        converter.downmix = true
        self.converter = converter
        self.target = target

        // About a tenth of a second at a time, as the buffers of Windows.
        input.installTap(onBus: 0, bufferSize: AVAudioFrameCount(format.sampleRate / 10), format: format) { [weak self] buffer, _ in
            self?.take(buffer)
        }
        do {
            try engine.start()
        } catch {
            input.removeTap(onBus: 0)
            return false
        }
        lock.withLock { lastSound = Date() }
        watchForLoss()
        return true
    }

    /// Everything heard so far, from the press.
    func heardSoFar() -> [Float] {
        lock.withLock { samples }
    }

    func stop() {
        let wasStopped = lock.withLock { () -> Bool in
            defer { stopped = true }
            return stopped
        }
        guard !wasStopped else { return }
        watch?.cancel()
        watch = nil
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
    }

    private func take(_ buffer: AVAudioPCMBuffer) {
        guard let converter, let target else { return }
        let capacity = AVAudioFrameCount(Double(buffer.frameLength) * Self.rate / buffer.format.sampleRate) + 32
        guard let out = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: capacity) else { return }

        // The converter keeps its state between buffers, so the next one goes on without a seam.
        let once = Once()
        var error: NSError?
        converter.convert(to: out, error: &error) { _, status in
            if once.given {
                status.pointee = .noDataNow
                return nil
            }
            once.given = true
            status.pointee = .haveData
            return buffer
        }
        guard error == nil, let channel = out.floatChannelData?[0] else { return }

        let count = Int(out.frameLength)
        let piece = UnsafeBufferPointer(start: channel, count: count)
        var energy = 0.0
        var first = false
        lock.withLock {
            guard !stopped else { return }
            samples.append(contentsOf: piece)
            for sample in piece {
                energy += Double(sample) * Double(sample)
                if !heard, abs(sample) > Self.silence {
                    heard = true
                    first = true
                }
            }
            // Speech sits around a tenth of full scale; times eight makes a normal voice fill the wave.
            loudness = count == 0 ? 0 : min(1, (energy / Double(count)).squareRoot() * 8)
            lastSound = Date()
        }
        if first {
            firstSound?()
        }
    }

    /// The audio system says nothing when a device goes away; the sound simply stops coming.
    private func watchForLoss() {
        let timer = DispatchSource.makeTimerSource(queue: .global(qos: .utility))
        timer.schedule(deadline: .now() + 1, repeating: 1)
        timer.setEventHandler { [weak self] in
            guard let self else { return }
            let gone = self.lock.withLock { () -> Bool in
                guard !self.stopped, !self.lostTold, Date().timeIntervalSince(self.lastSound) > Self.quietFor else { return false }
                self.lostTold = true
                return true
            }
            if gone {
                self.lost?()
            }
        }
        watch = timer
        timer.resume()
    }

    /// The converter asks for sound until it is told there is none for now.
    private final class Once: @unchecked Sendable {
        var given = false
    }
}
