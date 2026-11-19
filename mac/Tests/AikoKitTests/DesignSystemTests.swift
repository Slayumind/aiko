import Foundation
import Testing

/// The rules of the design system, read from the source of the Mac app: the twin of
/// DesignSystemTests.cs. A screen names tokens and shared parts, so a change of the look is made
/// in one place and nothing old stays behind.
struct DesignSystemTests {
    private static let app: URL = {
        var folder = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while !FileManager.default.fileExists(atPath: folder.appendingPathComponent("Aiko.slnx").path) {
            folder.deleteLastPathComponent()
        }
        return folder.appendingPathComponent("mac/Sources/AikoMac")
    }()

    /// Every Swift file of the app, by its path inside AikoMac, without its comments.
    private static let sources: [String: String] = {
        var files: [String: String] = [:]
        let walker = FileManager.default.enumerator(at: app, includingPropertiesForKeys: nil)
        while let file = walker?.nextObject() as? URL {
            guard file.pathExtension == "swift", let text = try? String(contentsOf: file, encoding: .utf8) else { continue }
            let name = String(file.path.dropFirst(app.path.count + 1))
            files[name] = text.replacingOccurrences(of: #"(?m)^\s*//.*$"#, with: "", options: .regularExpression)
        }
        return files
    }()

    private static func filesMatching(_ pattern: String, allowed: Set<String> = []) -> [String] {
        sources
            .filter { !allowed.contains($0.key) && $0.value.range(of: pattern, options: .regularExpression) != nil }
            .map(\.key)
            .sorted()
    }

    /// The colours of the window are numbers only in Theme.swift, and a token is not made weaker on
    /// a screen: a fraction of one is a new colour. The terminal draws with the ANSI palette of
    /// TerminalColors, the face and the icon of the menu bar are pictures of their own (D-213), and
    /// a self test may paint what it checks.
    @Test
    func coloursAreWrittenOnlyAmongTheTokens() {
        let found = Self.filesMatching(
            #"Rgba\(\s*0x|Color\(\s*\.sRGB|Color\(\s*red:|NSColor\(\s*(srgbRed|calibratedRed|red|white):|Color\.white|NSColor\.white|Theme\.\w+\.opacity\(\s*0\."#,
            allowed: ["Theme.swift", "Terminal/ShellBlocksView.swift", "BadgePaint.swift", "FacePaint.swift", "Terminal/EditorCheck.swift"])
        #expect(found.isEmpty, "colours outside the tokens: \(found)")
    }

    /// Theme.swift falls back to the system mono font of a token size when Geist Mono is missing.
    @Test
    func textSizesComeFromTokens() {
        let found = Self.filesMatching(#"Theme\.mono\(\s*[0-9]|monoFont\(\s*[0-9]|\.system\(size:"#, allowed: ["Theme.swift"])
        #expect(found.isEmpty, "text sizes outside the tokens: \(found)")
    }

    /// The badge on the icon of the menu bar is a pill, as on the tray icon of Windows: it is a
    /// picture of the icon, not a part of the window.
    @Test
    func cornersAreNeverRounded() {
        let found = Self.filesMatching(#"cornerRadius|RoundedRectangle|Capsule\(|roundedRect"#, allowed: ["BadgePaint.swift"])
        #expect(found.isEmpty, "rounded corners: \(found)")
    }

    /// The marks come from the icon set of the site, as SiteIcons and Marks on Windows.
    @Test
    func marksComeFromTheIconSet() {
        let found = Self.filesMatching(#"systemName:"#)
        #expect(found.isEmpty, "system symbols: \(found)")
    }

    @Test
    func everyTokenIsInUse() throws {
        let theme = try #require(Self.sources["Theme.swift"])
        let names = theme.matches(of: /static (?:let|var|func) (\w+)/).map { String($0.output.1) }
        let everything = Self.sources.values.joined(separator: "\n")
        let whole = NSRange(everything.startIndex..., in: everything)
        let unused = try Set(names).filter { name in
            try NSRegularExpression(pattern: "(?<![\\w])\(name)(?![\\w])").numberOfMatches(in: everything, range: whole) < 2
        }
        #expect(unused.isEmpty, "unused tokens: \(unused.sorted())")
    }
}
