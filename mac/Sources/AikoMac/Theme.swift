import AikoKit
import AppKit
import SwiftUI

/// The tokens of the window, put into SwiftUI. The twin of Theme/Tokens.xaml on Windows: nothing
/// here invents a colour or a number, and docs/DESIGN.md says what each one is for. Dark only.
enum Theme {
    // ---- Colours ----

    /// The numbers of the tokens, once: SwiftUI and AppKit both take their colours from here.
    /// AppKit gets plain sRGB numbers, never a colour turned over from SwiftUI.
    private enum Swatch {
        static let background = Rgba(0x0A, 0x0A, 0x0A)
        static let raised = Rgba(0x26, 0x26, 0x26)
        static let muted = Rgba(0xA1, 0xA1, 0xA1)
        static let ink = Rgba(0xFA, 0xFA, 0xFA)
        static let positive = Rgba(0x00, 0xBC, 0x7D)
        static let caution = Rgba(0xFE, 0x9A, 0x00)
        static let destructive = Rgba(0xFF, 0x64, 0x67)
        static let limitSoft = Rgba(0xC9, 0xB6, 0xFF)
        static let limitStrong = Rgba(0x9D, 0x7B, 0xFF)
        static let cardFill = Rgba(0x17, 0x17, 0x17)
        static let quietText = Rgba(0x7A, 0x7A, 0x7A)
        static let barTrack = Rgba(0x4D, 0x4D, 0x4D)
        static let voiceInk = Rgba(0xCF, 0xE9, 0xDF)
        static let dangerZone = Rgba(0xFF, 0x64, 0x67, alpha: 0x0F / 255.0)
        static let dangerZoneLine = Rgba(0xFF, 0x64, 0x67, alpha: 0x38 / 255.0)
        static let cautionZone = Rgba(0xFE, 0x9A, 0x00, alpha: 0x0D / 255.0)
        static let cautionZoneLine = Rgba(0xFE, 0x9A, 0x00, alpha: 0x38 / 255.0)
        static let scrim = Rgba(0x0A, 0x0A, 0x0A, alpha: 0x99 / 255.0)
        static let hairline = Rgba(0xFF, 0xFF, 0xFF, alpha: 0x38 / 255.0)
        static let hoverLayer = Rgba(0xFF, 0xFF, 0xFF, alpha: 0x12 / 255.0)
        static let blockFill = Rgba(0xFF, 0xFF, 0xFF, alpha: 0x08 / 255.0)
    }

    static let background = colour(Swatch.background)
    static let raised = colour(Swatch.raised)
    static let muted = colour(Swatch.muted)
    static let ink = colour(Swatch.ink)
    static let positive = colour(Swatch.positive)
    static let caution = colour(Swatch.caution)
    static let destructive = colour(Swatch.destructive)

    /// Limits have their own violet scale, so green, orange and red speak only about sessions
    /// (D-440). The violets are outside the site palette by the owner's decision.
    static let limitSoft = colour(Swatch.limitSoft)
    static let limitStrong = colour(Swatch.limitStrong)
    static let cardFill = colour(Swatch.cardFill)
    static let quietText = colour(Swatch.quietText)
    static let barTrack = colour(Swatch.barTrack)

    /// The box around a step that cannot be undone, and around a warning.
    static let dangerZone = colour(Swatch.dangerZone)
    static let dangerZoneLine = colour(Swatch.dangerZoneLine)
    static let cautionZone = colour(Swatch.cautionZone)
    static let cautionZoneLine = colour(Swatch.cautionZoneLine)

    /// What lies under a panel that floats over the window: still there, only quieter.
    static let scrim = colour(Swatch.scrim)

    /// Hairlines are white at low opacity, not a grey of their own: they have to sit on both the
    /// card and the raised surface without becoming a line of their own colour.
    static let hairline = colour(Swatch.hairline)

    /// Hover lightens whatever is under it by this much, as a layer that fades in.
    static let hoverLayer = colour(Swatch.hoverLayer)


    /// A block that stands a little apart from the page.
    static let blockFill = colour(Swatch.blockFill)

    /// The voice of the personality: its sample in the settings and its lines in the terminal.
    static let voiceInk = colour(Swatch.voiceInk)

    /// The colour of a limit in the window. The ring of the menu bar icon keeps its own colours.
    static func tone(_ tone: LimitTone) -> Color {
        switch tone {
        case .normal: return ink
        case .caution: return limitSoft
        case .critical: return limitStrong
        case .unknown: return muted
        }
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



    /// The same tokens for the parts Aiko draws by hand: the island is an NSView, not SwiftUI,
    /// because it changes its own size every frame while it unfolds, and the editor is AppKit text.
    static var nsBackground: NSColor { nsColour(Swatch.background) }
    static var nsInk: NSColor { nsColour(Swatch.ink) }
    static var nsMuted: NSColor { nsColour(Swatch.muted) }
    static var nsRaised: NSColor { nsColour(Swatch.raised) }
    static var nsHairline: NSColor { nsColour(Swatch.hairline) }
    static var nsHoverLayer: NSColor { nsColour(Swatch.hoverLayer) }
    static var nsDestructive: NSColor { nsColour(Swatch.destructive) }
    static var nsCaution: NSColor { nsColour(Swatch.caution) }

    // ---- Sizes ----

    static let textTiny: CGFloat = 11
    static let textSmall: CGFloat = 12
    static let textBody: CGFloat = 13
    static let textTitle: CGFloat = 16

    /// Only floating things get a shadow, and only this one. WPF measures the blur across the whole
    /// soft edge and SwiftUI from the middle out, so the radius is half of the 24 in Tokens.xaml.
    static let shadowBlur: CGFloat = 24
    static let shadowOffset: CGFloat = 8
    static let shadowOpacity = 0.45

    // ---- Fonts ----

    /// The one font of the new look (D-435): Geist Mono for every text, names and sentences too.
    static func mono(_ size: CGFloat, weight: Font.Weight = .regular) -> Font {
        hasMono ? .custom("Geist Mono", size: size).weight(weight) : .system(size: size, weight: weight, design: .monospaced)
    }

    /// The same font as `mono`, for text drawn straight into a view.
    static func monoFont(_ size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        if hasMono,
           let font = NSFontManager.shared.font(
               withFamily: "Geist Mono", traits: [], weight: weight == .regular ? 5 : 6, size: size) {
            return font
        }

        return .monospacedSystemFont(ofSize: size, weight: weight)
    }

    /// Written once at startup and read by the views afterwards, all on the main thread.
    private nonisolated(unsafe) static var hasMono = false

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

        hasMono = NSFontManager.shared.availableFontFamilies.contains("Geist Mono")
    }
}
