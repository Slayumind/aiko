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

    /// How tall the menu bar is. Only a menu can say, and an app with no windows has none, so one
    /// is kept here for the question. It is never shown: Aiko has no menu bar of its own.
    private static let ruler = NSMenu()

    private static var menuBarHeight: CGFloat {
        // A menu answers only once it is the app's own main menu. An app with no windows shows no
        // menu bar anyway, so this changes nothing on screen.
        if NSApp.mainMenu == nil {
            NSApp.mainMenu = ruler
        }
        return ruler.menuBarHeight
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

    /// The room the island may use on the screen a point is over. The sides and the bottom come
    /// from visibleFrame, which leaves out the Dock. The top does not: visibleFrame stops a few
    /// points below the menu bar (25 against a menu bar of 22 on this MacBook), and the island
    /// would hang there with a gap instead of touching the edge, which is the whole idea of it.
    static func work(over place: Point) -> Box {
        let at = NSPoint(x: place.x, y: top - place.y)
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(at) }) ?? NSScreen.main else {
            return box(NSRect(x: 0, y: 0, width: 1440, height: 900))
        }

        let visible = screen.visibleFrame
        // Three numbers describe the top of the screen and only one of them is the menu bar:
        // NSStatusBar.thickness is 22 (the room for status items), visibleFrame stops 25 below the
        // top (a point of air under the bar), and the bar itself is 24. Under a notch the bar is as
        // tall as the notch instead.
        let menuBar = max(menuBarHeight, screen.safeAreaInsets.top)
        let underTheMenuBar = screen.frame.maxY - (menuBar > 0 ? menuBar : screen.frame.maxY - visible.maxY)

        return box(NSRect(
            x: visible.minX,
            y: visible.minY,
            width: visible.width,
            height: max(0, underTheMenuBar - visible.minY)))
    }

    /// The room on the screen an island is over, taken from its middle: a window that hangs over
    /// two screens belongs to the one its middle is on.
    static func work(under island: Box) -> Box {
        work(over: Point(island.centreX, island.centreY))
    }
}
