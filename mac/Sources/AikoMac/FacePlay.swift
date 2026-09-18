import AikoKit
import AppKit

/// The way to a face and back (D-211): the rings shrink away, the face comes in on the spring,
/// stays, leaves, and the rings grow back drawing their arcs again.
///
/// The twin of IslandFaceAnimator.cs and TrayFaceAnimator.cs at once. Windows hands the tray a
/// picture and so plays eight pictures per step; macOS draws the menu bar icon itself, so both
/// surfaces get the same smooth steps, and one player serves whichever one is on screen.
@MainActor
final class FacePlay {
    private let tween = Tween()
    private var queue: [FacePhase] = []

    /// How the rings and the face stand right now.
    private(set) var frame = IconFrame.rings

    /// How far the island has closed in around the face, 0 to 1.
    private(set) var fit = 0.0

    private(set) var picture: FacePicture?

    /// Called on every step. Whoever is on screen redraws.
    var changed: (() -> Void)?

    func show(_ picture: FacePicture) {
        let shown = frame.showsFace
        self.picture = picture
        play(TrayFaceMotion.toFace(faceShown: shown))
    }

    func hide() {
        guard picture != nil else { return }
        play(TrayFaceMotion.toRings(faceShown: frame.showsFace))
    }

    /// The whole way there, or back, with no steps: for a self test that has to read one moment.
    func set(_ frame: IconFrame, _ picture: FacePicture?) {
        tween.stop()
        queue = []
        self.picture = picture
        self.frame = frame
        fit = frame.showsFace ? 1 : 0
        changed?()
    }

    /// How long the island keeps following its own size after a face begins or ends.
    static let arrival = TrayFaceMotion.arrival
    static let departure = TrayFaceMotion.duration(.faceOut) + TrayFaceMotion.duration(.ringsBack)

    private func play(_ phases: [FacePhase]) {
        tween.stop()
        queue = phases
        step()
    }

    private func step() {
        guard !queue.isEmpty else {
            // Nothing keeps a face that is not on screen: the next one comes with its own picture.
            if !frame.showsFace {
                picture = nil
            }
            changed?()
            return
        }

        let phase = queue.removeFirst()
        tween.run(TrayFaceMotion.duration(phase), nil) { [weak self] share in
            guard let self else { return }
            self.fit = TrayFaceMotion.islandFit(phase, share)
            self.frame = TrayFaceMotion.at(phase, share)
            self.changed?()
        } done: { [weak self] in
            self?.step()
        }
    }
}
