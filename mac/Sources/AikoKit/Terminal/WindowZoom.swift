import Foundation

/// The zoom of the terminal window in percent (D-384). The text, the icons, the gaps and the font of
/// the terminal grow together, so they keep one scale. Rows: spec/cases/window-zoom. The twin of
/// WindowZoom.cs.
public enum WindowZoom {
    public static let steps = [100, 115, 130]

    /// A little larger than the plain size: the terminal holds a lot of text to read.
    public static let defaultZoom = 115

    /// The text of the window in units at 100 %: TextNumber of the theme.
    private static let windowText = 13.0

    /// The next step up, or the same at the top.
    public static func larger(_ zoom: Int) -> Int {
        steps[min(indexOf(zoom) + 1, steps.count - 1)]
    }

    /// The next step down, or the same at the bottom.
    public static func smaller(_ zoom: Int) -> Int {
        steps[max(indexOf(zoom) - 1, 0)]
    }

    /// A value from the settings file: one of the steps, or the default.
    public static func read(_ stored: Int) -> Int {
        steps.contains(stored) ? stored : defaultZoom
    }

    /// The font of the terminal in whole points, as large as the text of the window.
    public static func terminalPoints(_ zoom: Int) -> Int {
        Int((windowText * Double(read(zoom)) / 100 * 72 / 96).rounded(.toNearestOrAwayFromZero))
    }

    /// The zoom as a factor for a scale: 1.15 for 115.
    public static func scale(_ zoom: Int) -> Double {
        Double(read(zoom)) / 100
    }

    private static func indexOf(_ zoom: Int) -> Int {
        steps.firstIndex(of: read(zoom)) ?? 0
    }
}
