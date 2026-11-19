import Foundation

/// A width and a height. It is not called Size so the drawing layer can still say CGSize.
public struct Extent: Sendable, Equatable {
    public let width: Double
    public let height: Double

    public init(_ width: Double, _ height: Double) {
        self.width = width
        self.height = height
    }

    public static let zero = Extent(0, 0)
}

/// A place in the same units as Box: points, with y growing downwards, the way both the mockup and
/// the Windows app count. The Mac window layer turns it over once, at the edge of the core.
public struct Point: Sendable, Equatable {
    public let x: Double
    public let y: Double

    public init(_ x: Double, _ y: Double) {
        self.x = x
        self.y = y
    }
}

/// Room on the four sides: the hairline, or the padding inside it.
public struct Edges: Sendable, Equatable {
    public let top: Double
    public let right: Double
    public let bottom: Double
    public let left: Double

    public init(top: Double, right: Double, bottom: Double, left: Double) {
        self.top = top
        self.right = right
        self.bottom = bottom
        self.left = left
    }

    public static let zero = Edges(top: 0, right: 0, bottom: 0, left: 0)

    /// Across is left plus right, along is top plus bottom.
    public var across: Double { left + right }
    public var along: Double { top + bottom }
}

/// The four corner radii, clockwise from the top left.
public struct Corners: Sendable, Equatable {
    public let topLeft: Double
    public let topRight: Double
    public let bottomRight: Double
    public let bottomLeft: Double

    public init(topLeft: Double, topRight: Double, bottomRight: Double, bottomLeft: Double) {
        self.topLeft = topLeft
        self.topRight = topRight
        self.bottomRight = bottomRight
        self.bottomLeft = bottomLeft
    }

    public static func all(_ cut: Double) -> Corners {
        Corners(topLeft: cut, topRight: cut, bottomRight: cut, bottomLeft: cut)
    }
}

/// One environment on the island: its ring, and the percentage that slides out beside it.
public struct IslandRingSpot: Sendable, Equatable {
    public let ring: Box

    /// What is on show of the percentage. Empty while the island is folded; the words are drawn at
    /// their full size and cut to this, so they slide into view instead of being squeezed.
    public let clip: Box

    /// Where the words themselves go, at full size, inside the clip.
    public let words: Box

    public init(ring: Box, clip: Box, words: Box) {
        self.ring = ring
        self.clip = clip
        self.words = words
    }
}

/// One mark of the sessions after the rings (D-364): its sign in a field of 12, and its number.
public struct IslandMarkSpot: Sendable, Equatable {
    public let sign: Box
    public let number: Box

    public init(sign: Box, number: Box) {
        self.sign = sign
        self.number = number
    }
}

/// Everything the island is, as numbers: how big it is, where its line and its corners are, and
/// where every ring, every percentage and the face sit inside it.
public struct IslandFrame: Sendable, Equatable {
    public let size: Extent
    public let border: Edges
    public let padding: Edges
    public let corners: Corners

    /// The room the rings live in, between the padding. It closes in on the face while a face is
    /// showing, so the face keeps the same room on every side (D-211).
    public let content: Box

    /// The size the rings and their percentages ask for. While the island closes in on the face
    /// this stays as it was, and the rings are drawn smaller instead.
    public let asked: Extent

    public let spots: [IslandRingSpot]

    /// The marks of the sessions, in the order they were asked for. They go with the percentages
    /// while the island closes in on the face; marksOpacity says how much of them is left.
    public let marks: [IslandMarkSpot]
    public let marksOpacity: Double

    /// The 18 point square the face is drawn in, in the middle of the content.
    public let face: Box
}

/// The island, in numbers: the twin of IslandPanel.xaml.cs and of the sizes in Tokens.xaml.
///
/// The view draws what this says and nothing else, so the same island can be checked without a
/// screen. Every number here comes from the accepted mockup.
public enum IslandLayout {
    public static let ringSize = 18.0

    /// Between two rings, along the edge.
    public static let ringGap = 8.0

    /// How far a free corner is cut at 45 degrees (D-448), as CutSize in IslandPanel.xaml.cs.
    public static let cut = 8.0
    public static let line = 1.0

    /// The room inside the island, the line included: 22 across and 16 along, so two rings on a
    /// horizontal edge make a 66 by 34 island and one ring on a side edge a 40 by 60 one.
    public static let roomAcross = 11.0
    public static let roomAlong = 8.0

    /// The marks of the sessions: 12 after the rings, a sign in a field of 12 and its number 4 after
    /// it, 10 between marks along a top or bottom edge and 6 between them in a column along a side.
    /// The numbers of IslandPanel.DrawSessions on Windows.
    public static let marksGap = 12.0
    public static let signSize = 12.0
    public static let numberGap = 4.0
    public static let numberLeast = 7.0
    public static let markGapAlong = 10.0
    public static let markGapAcross = 6.0

    /// The gap between a ring and its percentage: beside it on a horizontal edge, under it on a
    /// side edge.
    public static let wordsGapBeside = 6.0
    public static let wordsGapUnder = 4.0

    /// Docked, the side against the screen has no corners and no line: the island reads as part of
    /// the edge. In hand it is a whole thing of its own.
    public static func border(_ edge: ScreenEdge, docked: Bool) -> Edges {
        guard docked else {
            return Edges(top: line, right: line, bottom: line, left: line)
        }

        switch edge {
        case .top: return Edges(top: 0, right: line, bottom: line, left: line)
        case .bottom: return Edges(top: line, right: line, bottom: 0, left: line)
        case .left: return Edges(top: line, right: line, bottom: line, left: 0)
        case .right: return Edges(top: line, right: 0, bottom: line, left: line)
        }
    }

    public static func corners(_ edge: ScreenEdge, docked: Bool) -> Corners {
        guard docked else {
            return .all(cut)
        }

        switch edge {
        case .top: return Corners(topLeft: 0, topRight: 0, bottomRight: cut, bottomLeft: cut)
        case .bottom: return Corners(topLeft: cut, topRight: cut, bottomRight: 0, bottomLeft: 0)
        case .left: return Corners(topLeft: 0, topRight: cut, bottomRight: cut, bottomLeft: 0)
        case .right: return Corners(topLeft: cut, topRight: 0, bottomRight: 0, bottomLeft: cut)
        }
    }

    /// The missing line goes into the padding, so the rings do not move by a pixel when the island
    /// lands and the line on that side goes away.
    public static func padding(_ edge: ScreenEdge, docked: Bool) -> Edges {
        let line = border(edge, docked: docked)
        return Edges(
            top: roomAlong - line.top,
            right: roomAcross - line.right,
            bottom: roomAlong - line.bottom,
            left: roomAcross - line.left)
    }

    /// The room the face asks for: the island closes in on the side where its chrome is wider, so
    /// the face ends up with the same 8 points on every side, on any edge.
    public static let faceContent = Extent(
        max(0, ringSize - max(0, (roomAcross * 2) - (roomAlong * 2))),
        max(0, ringSize - max(0, (roomAlong * 2) - (roomAcross * 2))))

    /// How much of the words is painted at this share of the way open. They fade in over the last
    /// 60 % of the way, so a percentage that is only a sliver wide is not yet readable.
    public static func wordsOpacity(_ share: Double) -> Double {
        min(max((share - 0.4) / 0.6, 0), 1)
    }

    /// The island for these environments.
    ///
    /// - labels: the size of the words of each ring, as the font measures them. No labels at all
    ///   means the island nobody has set up yet: one dashed ring and nothing else.
    /// - unfold: 0 folded, 1 with the percentages fully out.
    /// - faceFit: 0 rings, 1 closed in around the face.
    public static func frame(
        labels: [Extent],
        edge: ScreenEdge,
        docked: Bool = true,
        unfold: Double = 0,
        faceFit: Double = 0,
        sessions: [Extent] = []
    ) -> IslandFrame {
        let horizontal = IslandPlacement.isHorizontal(edge)
        let share = min(max(unfold, 0), 1)
        let fit = min(max(faceFit, 0), 1)
        let line = border(edge, docked: docked)
        let pad = padding(edge, docked: docked)

        // A ring with no percentage: the words are the whole item's second half, and here there is
        // none at all.
        let words = labels.map { full($0, horizontal: horizontal) }
        let items = words.isEmpty
            ? [Extent(ringSize, ringSize)]
            : words.map { item($0, horizontal: horizontal, share: share) }

        let asked = row(items, horizontal: horizontal)

        // The marks of the sessions after the rings. A face takes them away with the percentages.
        let markItems = sessions.map {
            Extent(signSize + numberGap + max(numberLeast, $0.width), max(signSize, $0.height))
        }
        let block = marksBlock(markItems, horizontal: horizontal)
        let keep = 1 - fit
        let lead = markItems.isEmpty ? 0 : marksGap
        let whole = horizontal
            ? Extent(asked.width + ((lead + block.width) * keep), max(asked.height, block.height * keep))
            : Extent(max(asked.width, block.width * keep), asked.height + ((lead + block.height) * keep))

        let box = Extent(
            whole.width + ((faceContent.width - whole.width) * fit),
            whole.height + ((faceContent.height - whole.height) * fit))

        let content = Box(line.left + pad.left, line.top + pad.top, box.width, box.height)
        let size = Extent(
            content.width + pad.across + line.across,
            content.height + pad.along + line.along)

        // The rings keep the size they asked for and sit in the middle of the room, so closing in
        // on the face moves nothing sideways.
        var spots: [IslandRingSpot] = []
        let start = horizontal
            ? content.centreX - (whole.width / 2)
            : content.centreY - (whole.height / 2)
        var at = start
        let across = horizontal
            ? content.centreY - (asked.height / 2)
            : content.centreX - (asked.width / 2)

        for (index, item) in items.enumerated() {
            let itemBox = horizontal
                ? Box(at, across, item.width, asked.height)
                : Box(across, at, asked.width, item.height)
            let ring = horizontal
                ? Box(itemBox.x, itemBox.centreY - (ringSize / 2), ringSize, ringSize)
                : Box(itemBox.centreX - (ringSize / 2), itemBox.y, ringSize, ringSize)

            let full = index < words.count ? words[index] : .zero
            let shown = Extent(full.width * share, full.height * share)
            let clip = horizontal
                ? Box(ring.right, itemBox.centreY - (shown.height / 2), shown.width, shown.height)
                : Box(itemBox.centreX - (shown.width / 2), ring.bottom, shown.width, shown.height)

            spots.append(IslandRingSpot(
                ring: ring,
                clip: clip,
                words: horizontal
                    ? Box(clip.x + wordsGapBeside, clip.y, max(0, full.width - wordsGapBeside), full.height)
                    : Box(clip.x, clip.y + wordsGapUnder, full.width, max(0, full.height - wordsGapUnder))))

            at += (horizontal ? item.width : item.height) + ringGap
        }

        var marks: [IslandMarkSpot] = []
        var markAt = start + (horizontal ? asked.width : asked.height) + lead
        for (index, item) in markItems.enumerated() {
            let number = sessions[index]
            let left = horizontal ? markAt : content.centreX - (item.width / 2)
            let middle = horizontal ? content.centreY : markAt + (item.height / 2)
            marks.append(IslandMarkSpot(
                sign: Box(left, middle - (signSize / 2), signSize, signSize),
                number: Box(left + signSize + numberGap, middle - (number.height / 2), max(numberLeast, number.width), number.height)))
            markAt += (horizontal ? item.width + markGapAlong : item.height + markGapAcross)
        }

        return IslandFrame(
            size: size,
            border: line,
            padding: pad,
            corners: corners(edge, docked: docked),
            content: content,
            asked: asked,
            spots: spots,
            marks: marks,
            marksOpacity: keep,
            face: Box(
                content.centreX - (ringSize / 2),
                content.centreY - (ringSize / 2),
                ringSize,
                ringSize))
    }

    /// The words plus the gap that holds them off the ring.
    private static func full(_ label: Extent, horizontal: Bool) -> Extent {
        horizontal
            ? Extent(label.width + wordsGapBeside, label.height)
            : Extent(label.width, label.height + wordsGapUnder)
    }

    /// One ring with as much of its percentage as is out.
    private static func item(_ words: Extent, horizontal: Bool, share: Double) -> Extent {
        horizontal
            ? Extent(ringSize + (words.width * share), max(ringSize, words.height * share))
            : Extent(max(ringSize, words.width * share), ringSize + (words.height * share))
    }

    /// The marks in a row along a top or bottom edge, in a column along a side one.
    private static func marksBlock(_ items: [Extent], horizontal: Bool) -> Extent {
        guard !items.isEmpty else { return .zero }
        let widths = items.map(\.width)
        let heights = items.map(\.height)
        return horizontal
            ? Extent(widths.reduce(0, +) + (markGapAlong * Double(items.count - 1)), heights.max() ?? 0)
            : Extent(widths.max() ?? 0, heights.reduce(0, +) + (markGapAcross * Double(items.count - 1)))
    }

    /// The rings in a row along a top or bottom edge, in a column along a side one.
    private static func row(_ items: [Extent], horizontal: Bool) -> Extent {
        let gaps = ringGap * Double(max(0, items.count - 1))
        let widths = items.map(\.width)
        let heights = items.map(\.height)

        return horizontal
            ? Extent(widths.reduce(0, +) + gaps, heights.max() ?? 0)
            : Extent(widths.max() ?? 0, heights.reduce(0, +) + gaps)
    }
}
