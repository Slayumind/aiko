import Foundation

/// The line shown when the mouse rests on the icon in the menu bar.
///
/// It used to say "Aiko" and nothing else, which wasted the one surface that always works. A
/// screen reader reads it too, and the ring and the dot say nothing to one.
///
/// The twin of TrayText.cs on Windows, including the length: Windows keeps 128 characters with the
/// closing zero, macOS has no such limit, and one text for both is one text to read and translate.
public enum TrayText {
    public static let room = 127

    public static func tooltip(
        _ cards: [CardState], newerVersion: String? = nil, timeZone: TimeZone = .current
    ) -> String {
        var parts: [String] = []

        for card in cards {
            parts.append(card.iconRow.map { "\(card.environment) \($0.percent)%" }
                ?? "\(card.environment) —")
        }

        if parts.isEmpty {
            parts.append("Aiko")
        }

        if !cards.isEmpty && cards.allSatisfy({ $0.freshness == .none }) {
            parts.append(Strings.trayOpenClaudeCode)
        } else if cards.contains(where: { $0.freshness == .stale }) {
            let newest = cards.compactMap(\.updatedAt).max()
            parts.append(Strings.format(Strings.trayLastSeen, CardText.clock(newest, timeZone)))
        }

        if let newerVersion {
            parts.append(Strings.format(Strings.trayUpdateOut, newerVersion))
        }

        let text = parts.joined(separator: " · ")
        return text.count <= room ? text : String(text.prefix(room))
    }
}
