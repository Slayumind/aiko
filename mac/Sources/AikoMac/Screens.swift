import AikoKit
import AppKit

/// AppKit counts screen points from the bottom left of the first screen; the core counts from the
/// top left, the way the mockup and the Windows app do. The turn happens here and nowhere else, so
/// the placement rules stay the same on both systems.
@MainActor
enum Screens {
    /// The top of AppKit's own coordinates: the top of the screen that holds the menu bar.
    private static var top: CGFloat {
        NSScreen.screens.first?.frame.maxY ?? 0
    }

    static func box(_ rect: NSRect) -> Box {
        Box(rect.minX, top - rect.maxY, rect.width, rect.height)
    }

    static func rect(_ box: Box) -> NSRect {
        NSRect(x: box.x, y: top - box.bottom, width: box.width, height: box.height)
    }

    static func point(_ point: NSPoint) -> Point {
        Point(point.x, top - point.y)
    }

    /// The room the island may use on the screen a point is over. visibleFrame already leaves out
    /// the menu bar and the Dock, which is what the Windows work area means.
    static func work(over place: Point) -> Box {
        let at = NSPoint(x: place.x, y: top - place.y)
        let screen = NSScreen.screens.first { $0.frame.contains(at) } ?? NSScreen.main
        return box(screen?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900))
    }

    /// The room on the screen an island is over, taken from its middle: a window that hangs over
    /// two screens belongs to the one its middle is on.
    static func work(under island: Box) -> Box {
        work(over: Point(island.centreX, island.centreY))
    }
}
