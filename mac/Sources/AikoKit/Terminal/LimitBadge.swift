import Foundation

/// The one percentage in the title bar of the terminal: the limit that is closer to its end (D-296).
public struct LimitBadge: Sendable, Equatable {
    public let kind: LimitKind
    public let percent: Int
    public let tone: LimitTone

    public init(kind: LimitKind, percent: Int, tone: LimitTone) {
        self.kind = kind
        self.percent = percent
        self.tone = tone
    }

    private static let kinds: [LimitKind] = [.fiveHour, .sevenDay]

    /// Nil when nothing is known. When both windows are equally full, the five hour one is named:
    /// it is the one that turns over first.
    public static func forSnapshot(_ snapshot: LimitSnapshot, _ now: Date) -> LimitBadge? {
        var fullest: LimitStatus?
        for kind in kinds {
            if let status = snapshot.statusAt(now, kind), status.percent > (fullest?.percent ?? -1) {
                fullest = status
            }
        }

        guard let chosen = fullest else { return nil }
        return LimitBadge(
            kind: chosen.kind,
            percent: chosen.percent,
            tone: CardState.toneFor(chosen.percent, snapshot.freshnessAt(now)))
    }
}
