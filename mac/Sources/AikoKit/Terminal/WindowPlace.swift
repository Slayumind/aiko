import Foundation

/// The part of a screen a window may take, in the units the window is placed in.
public struct ScreenArea: Sendable, Equatable {
    public let left: Double
    public let top: Double
    public let width: Double
    public let height: Double

    public init(left: Double, top: Double, width: Double, height: Double) {
        self.left = left
        self.top = top
        self.width = width
        self.height = height
    }

    var right: Double { left + width }

    var bottom: Double { top + height }
}

/// Where the window of the terminal stands (D-292).
///
/// The twin of WindowPlace.cs. Top grows downwards here as it does there; the window code turns
/// it into the coordinates of AppKit.
public struct WindowPlace: Sendable, Equatable {
    public static let defaultWidth = 1100.0
    public static let defaultHeight = 760.0
    public static let minWidth = 480.0
    public static let minHeight = 360.0

    /// Below this width the list of sessions goes under a button (D-308).
    public static let narrowBelow = 720.0

    /// How much of the title bar has to be on a screen for the window to be reachable.
    private static let grip = 80.0
    private static let titleHeight = 40.0

    public let left: Double
    public let top: Double
    public let width: Double
    public let height: Double
    public let maximized: Bool

    public init(left: Double, top: Double, width: Double, height: Double, maximized: Bool) {
        self.left = left
        self.top = top
        self.width = width
        self.height = height
        self.maximized = maximized
    }

    public static func isNarrow(_ width: Double) -> Bool {
        width < narrowBelow
    }

    /// The place to open the window at. A saved place is kept when its title bar can still be
    /// reached on one of the screens; a screen may be gone since. Otherwise, and the first time,
    /// the window stands in the middle of the first screen. screens is never empty.
    public static func fit(_ saved: WindowPlace?, _ screens: [ScreenArea]) -> WindowPlace {
        if let place = saved, isSane(place), let home = screens.first(where: { reaches(place, $0) }) {
            let width = min(max(place.width, minWidth), max(minWidth, home.width))
            let height = min(max(place.height, minHeight), max(minHeight, home.height))
            return WindowPlace(
                left: place.left, top: max(place.top, home.top), width: width, height: height, maximized: place.maximized)
        }

        let first = screens[0]
        let width = min(defaultWidth, first.width)
        let height = min(defaultHeight, first.height)
        return WindowPlace(
            left: (first.left + (first.width - width) / 2).rounded(.down),
            top: (first.top + (first.height - height) / 2).rounded(.down),
            width: width,
            height: height,
            maximized: saved?.maximized ?? false)
    }

    private static func isSane(_ place: WindowPlace) -> Bool {
        place.left.isFinite && place.top.isFinite && place.width.isFinite && place.height.isFinite
            && place.width > 0 && place.height > 0
    }

    private static func reaches(_ place: WindowPlace, _ screen: ScreenArea) -> Bool {
        let across = min(place.left + place.width, screen.right) - max(place.left, screen.left)
        let down = min(place.top + titleHeight, screen.bottom) - max(place.top, screen.top)
        return across >= grip && down >= titleHeight / 2
    }
}
