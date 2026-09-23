import AppKit

/// Where Aiko's always-on-top windows sit next to the panels of macOS. The twin of TaskbarOrder.cs,
/// which does the same next to the Windows taskbar.
///
/// The two panels are not alike, and a spike measured it (RESEARCH, 2026-09-18): a window below
/// level 25 is pushed out of the menu bar's strip, so under the menu bar there is no going; a window
/// below level 20 slides under the Dock and hides behind it, exactly as a window slides under the
/// taskbar on Windows.
@MainActor
enum WindowOrder {
    /// Above every ordinary window and above the menu bar, so the island can touch the bar: at rest
    /// it starts where the bar ends and covers nothing.
    static let onTop = NSWindow.Level.statusBar

    /// Above every ordinary window but under the Dock, so an overshoot at the bottom edge goes under
    /// it instead of over it.
    static let underTheDock = NSWindow.Level(rawValue: 19)
}
