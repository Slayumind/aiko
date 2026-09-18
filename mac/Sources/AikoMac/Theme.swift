import AikoKit
import AppKit
import SwiftUI

/// The slayumind palette and the sizes of the card, put into SwiftUI. The twin of
/// Theme/Tokens.xaml on Windows: nothing here invents a colour or a number.
enum Theme {
    // ---- Colours ----

    static let background = rgb(0x0A, 0x0A, 0x0A)
    static let surface = rgb(0x17, 0x17, 0x17)
    static let raised = rgb(0x26, 0x26, 0x26)
    static let muted = rgb(0xA1, 0xA1, 0xA1)
    static let ink = rgb(0xFA, 0xFA, 0xFA)
    static let positive = rgb(0x00, 0xBC, 0x7D)
    static let caution = rgb(0xFE, 0x9A, 0x00)
    static let destructive = rgb(0xFF, 0x64, 0x67)

    /// Hairlines are white at low opacity, not a grey of their own: they have to sit on both the
    /// card and the raised surface without becoming a line of their own colour.
    static let hairline = Color.white.opacity(0.10)

    static func tone(_ tone: LimitTone) -> Color {
        colour(RingArt.colour(for: tone))
    }

    static func colour(_ rgba: Rgba) -> Color {
        Color(.sRGB,
              red: Double(rgba.red) / 255,
              green: Double(rgba.green) / 255,
              blue: Double(rgba.blue) / 255,
              opacity: rgba.alpha)
    }

    static func nsColour(_ rgba: Rgba) -> NSColor {
        NSColor(srgbRed: CGFloat(rgba.red) / 255,
                green: CGFloat(rgba.green) / 255,
                blue: CGFloat(rgba.blue) / 255,
                alpha: CGFloat(rgba.alpha))
    }

    private static func rgb(_ red: Int, _ green: Int, _ blue: Int) -> Color {
        colour(Rgba(red, green, blue))
    }

    /// The same palette for the parts Aiko draws by hand: the island is an NSView, not SwiftUI,
    /// because it changes its own size every frame while it unfolds.
    static var nsSurface: NSColor { nsColour(Rgba(0x17, 0x17, 0x17)) }
    static var nsInk: NSColor { nsColour(Rgba(0xFA, 0xFA, 0xFA)) }
    static var nsHairline: NSColor { NSColor.white.withAlphaComponent(0.10) }

    // ---- Sizes ----

    static let textTiny: CGFloat = 11
    static let textSmall: CGFloat = 12
    static let textNumber: CGFloat = 13
    static let textName: CGFloat = 14

    static let cardWidth: CGFloat = 296
    static let cardPadding: CGFloat = 14
    static let cardRadius: CGFloat = 14
    static let barHeight: CGFloat = 4
    static let barRadius: CGFloat = 2
    static let blockGap: CGFloat = 12
    static let rowGap: CGFloat = 8
    static let underBar: CGFloat = 3

    /// Only floating things get a shadow, and only this one. WPF measures the blur across the whole
    /// soft edge and SwiftUI from the middle out, so the radius is half of the 24 in Tokens.xaml.
    static let shadowBlur: CGFloat = 24
    static let shadowOffset: CGFloat = 8
    static let shadowOpacity = 0.45

    /// Room around the card inside its window, so the shadow is not cut off at the edge.
    static let shadowRoom: CGFloat = 20

    // ---- Fonts ----

    static func sans(_ size: CGFloat, weight: Font.Weight = .regular) -> Font {
        hasGeist ? .custom("Geist", size: size).weight(weight) : .system(size: size, weight: weight)
    }

    static func mono(_ size: CGFloat) -> Font {
        hasGeist ? .custom("Geist Mono", size: size) : .system(size: size, design: .monospaced)
    }

    /// The same mono font as `mono`, for text drawn straight into a view.
    static func monoFont(_ size: CGFloat) -> NSFont {
        if hasGeist,
           let font = NSFontManager.shared.font(withFamily: "Geist Mono", traits: [], weight: 5, size: size) {
            return font
        }

        return .monospacedSystemFont(ofSize: size, weight: .regular)
    }

    /// Written once at startup and read by the views afterwards, all on the main thread.
    private nonisolated(unsafe) static var hasGeist = false

    /// The app carries its own fonts, as the Windows build does. Without them Aiko still reads:
    /// the system font takes over and only the shapes of the letters change.
    static func registerFonts() {
        guard let folder = Bundle.main.resourceURL?.appendingPathComponent("Fonts"),
              let files = try? FileManager.default.contentsOfDirectory(
                  at: folder, includingPropertiesForKeys: nil)
        else {
            return
        }

        for file in files where file.pathExtension.lowercased() == "ttf" {
            CTFontManagerRegisterFontsForURL(file as CFURL, .process, nil)
        }

        hasGeist = NSFontManager.shared.availableFontFamilies.contains("Geist")
    }
}
