import AikoKit
import AppKit

/// One short animation, driven by a timer that exists only while something moves (D-161). When the
/// system is asked for fewer animations it lands at the end at once and no timer starts at all.
///
/// The card is SwiftUI and animates itself; the island is a window that changes size every frame,
/// and AppKit has nothing that animates a window's whole shape. So the island counts the frames.
@MainActor
final class Tween {
    /// Near enough to a screen's own rate. A step that lands late simply reads the clock and jumps
    /// to where it should be.
    static let frameTime = 1.0 / 60

    private var timer: Timer?
    private var startedAt = Date()
    private var length = 0.0
    private var curve: CubicBezier?
    private var onStep: ((Double) -> Void)?
    private var onDone: (() -> Void)?

    var isRunning: Bool { timer != nil }

    /// Runs from 0 to 1 over this many seconds. A curve of nil hands over the plain share of the
    /// time, for a motion that eases itself.
    func run(
        _ length: Double,
        _ curve: CubicBezier?,
        step: @escaping (Double) -> Void,
        done: @escaping () -> Void = {}
    ) {
        stop()

        guard !Motion.reduceMotion, length > 0 else {
            step(1)
            done()
            return
        }

        self.length = length
        self.curve = curve
        onStep = step
        onDone = done
        startedAt = Date()
        step(0)

        // A common mode timer: while the island is in hand AppKit runs the loop in its tracking
        // mode, and a plain timer would stop firing exactly when the island is moving.
        let timer = Timer(timeInterval: Tween.frameTime, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        onStep = nil
        onDone = nil
    }

    private func tick() {
        let share = min(1, Date().timeIntervalSince(startedAt) / length)
        onStep?(curve?.ease(share) ?? share)

        if share >= 1 {
            let done = onDone
            stop()
            done?()
        }
    }
}
