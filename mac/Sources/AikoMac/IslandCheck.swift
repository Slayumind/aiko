import AikoKit
import AppKit

/// The self test doors of the island: they drive it from code and write down what happened, for a
/// machine nobody is looking at. The twin of Island/IslandCheck.cs, which does the same on Windows
/// with --try-island and its friends.
///
/// Nothing here touches the person's mouse and nothing is saved. Every door quits when it is done.
@MainActor
enum IslandCheck {
    static func run(_ what: String, shell: AikoShell) {
        Log.write("self test: \(what), fewer animations: \(Motion.reduceMotion), "
            + "a full screen window in front: \(FullScreenWatch.isFullScreenInFront())")

        switch what {
        case "card-island":
            card(shell, shell.islandForCheck())
        case "island", "unfold":
            unfold(shell.islandForCheck())
        case "drag", "glass":
            drag(shell.islandForCheck())
        case "face":
            face(shell, shell.islandForCheck())
        default:
            Log.write("self test: there is no check called \(what)")
            quit(after: 0.2)
        }
    }

    /// The island unfolds its percentages and folds them again, and its size is written down on
    /// the way: the numbers have to grow and come back, and the height must not move.
    private static func unfold(_ island: IslandWindow) {
        Log.write("self test: folded \(size(island)) at \(place(island))")

        let sampler = Sampler(island)

        island.unfold(true)
        after(0.6) {
            Log.write("self test: unfolded \(size(island)) at \(place(island)), "
                + "on the way \(sampler.takeSizes())")
            island.unfold(false)
        }

        after(1.2) {
            sampler.stop()
            Log.write("self test: folded again \(size(island)), on the way \(sampler.takeSizes())")
            quit(after: 0.2)
        }
    }

    /// The island is taken from the top of the screen to the left edge, the way a person would
    /// drag it, and what it does on the way is written down: the shape in hand, the landing strip,
    /// and where it ends up.
    private static func drag(_ island: IslandWindow) {
        let from = Screens.point(NSPoint(x: island.frame.midX, y: island.frame.midY))
        let work = Screens.work(over: from)
        let to = Point(work.x + 30, work.y + (work.height / 3))

        island.press(at: from)

        let steps = 30
        for step in 1...steps {
            after(0.015 * Double(step)) {
                let share = Double(step) / Double(steps)
                island.hold(at: Point(
                    from.x + ((to.x - from.x) * share),
                    from.y + ((to.y - from.y) * share)))
            }
        }

        after(0.6) {
            let strip = island.landingStrip
            Log.write("self test: in hand \(size(island)) at \(place(island)), "
                + "column already in hand: \(island.frame.height > island.frame.width), "
                + "strip \(strip.map { "\(Int($0.width))x\(Int($0.height)) at \(Int($0.minX)),\(Int($0.minY))" } ?? "missing")")
            island.letGo()
        }

        after(1.5) {
            let landed = Screens.box(island.frame)
            Log.write("self test: landed \(size(island)) at \(place(island)) on the \(island.edge) edge, "
                + "left edge of the work area: \(Int(work.x)), island left: \(Int(landed.x)), "
                + "rings in a column: \(island.frame.height > island.frame.width), "
                + "strip gone: \(island.landingStrip == nil)")
            quit(after: 0.2)
        }
    }

    /// A face plays on the island: the rings go, the island closes in to a 34 point square around
    /// the face, and then everything comes back.
    private static func face(_ shell: AikoShell, _ island: IslandWindow) {
        let sampler = Sampler(island)

        shell.showFaceForCheck(.working)

        after(0.8) {
            Log.write("self test: with the face \(size(island)), on the way \(sampler.takeSizes())")
            shell.showFaceForCheck(nil)
        }

        after(1.8) {
            sampler.stop()
            Log.write("self test: back to the rings \(size(island)), on the way \(sampler.takeSizes())")
            quit(after: 0.2)
        }
    }

    /// The card opens from the island: under it on a top edge, over it on a bottom one (D-148).
    private static func card(_ shell: AikoShell, _ island: IslandWindow) {
        shell.openCardForCheck(pinned: true)

        after(0.6) {
            tell("from the top edge", island, shell.cardFrameForCheck)
            shell.closeCardForCheck()
            shell.placeIslandForCheck(IslandPosition(.bottom, 0.5))
        }

        after(1.2) {
            shell.openCardForCheck(pinned: true)
        }

        after(1.8) {
            tell("from the bottom edge", island, shell.cardFrameForCheck)
            shell.closeCardForCheck()
            quit(after: 0.4)
        }
    }

    private static func tell(_ what: String, _ island: IslandWindow, _ card: NSRect?) {
        guard let card else {
            Log.write("self test: card \(what) is missing")
            return
        }

        let island = island.frame
        Log.write("self test: card \(what): island \(Int(island.minY))..\(Int(island.maxY)), "
            + "card \(Int(card.minY))..\(Int(card.maxY)), "
            + "card under the island: \(Int(card.maxY) == Int(island.minY)), "
            + "card over the island: \(Int(card.minY) == Int(island.maxY))")
    }

    /// Writes down the size of the island every 30 ms, so a check can say what it did on the way
    /// and not only where it ended up.
    @MainActor
    private final class Sampler {
        private let island: IslandWindow
        private var sizes: [String] = []
        private var timer: Timer?

        init(_ island: IslandWindow) {
            self.island = island
            let timer = Timer(timeInterval: 0.03, repeats: true) { [weak self] _ in
                MainActor.assumeIsolated { self?.take() }
            }

            RunLoop.main.add(timer, forMode: .common)
            self.timer = timer
        }

        func stop() {
            timer?.invalidate()
            timer = nil
        }

        /// What has been seen so far, and then start again.
        func takeSizes() -> String {
            let seen = sizes.joined(separator: " ")
            sizes = []
            return seen
        }

        private func take() {
            sizes.append(IslandCheck.size(island))
        }
    }

    fileprivate static func size(_ island: IslandWindow) -> String {
        "\(Int(island.frame.width))x\(Int(island.frame.height))"
    }

    private static func place(_ island: IslandWindow) -> String {
        let box = Screens.box(island.frame)
        return "\(Int(box.x)),\(Int(box.y))"
    }

    private static func after(_ seconds: Double, _ step: @escaping () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) { step() }
    }

    private static func quit(after seconds: Double) {
        after(seconds) { NSApp.terminate(nil) }
    }
}
