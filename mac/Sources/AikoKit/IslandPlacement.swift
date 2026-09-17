import Foundation

/// A rectangle, in the core's own terms. The window layer has its own, but the core must not know
/// about it: the same rules place the island on both systems.
public struct Box: Sendable, Equatable {
    public let x: Double
    public let y: Double
    public let width: Double
    public let height: Double

    public init(_ x: Double, _ y: Double, _ width: Double, _ height: Double) {
        self.x = x
        self.y = y
        self.width = width
        self.height = height
    }

    public var right: Double { x + width }
    public var bottom: Double { y + height }
    public var centreX: Double { x + (width / 2) }
    public var centreY: Double { y + (height / 2) }
}

public enum ScreenEdge: Sendable, Equatable, CaseIterable {
    case top
    case bottom
    case left
    case right
}

/// Where the island sits: which edge it is stuck to, and how far along that edge.
///
/// Along is a share, not a number of pixels, so the island keeps its place when the screen
/// changes size or the user moves to another monitor.
public struct IslandPosition: Sendable, Equatable {
    public let edge: ScreenEdge
    public let along: Double

    public init(_ edge: ScreenEdge, _ along: Double) {
        self.edge = edge
        self.along = along
    }

    /// The top of the screen, in the middle: where the island starts and where "reset position"
    /// puts it back.
    public static let `default` = IslandPosition(.top, 0.5)
}

public enum IslandPlacement {
    /// The edge the island ended up nearest after being dragged, and its share along that edge.
    public static func nearest(_ island: Box, _ work: Box) -> IslandPosition {
        let toTop = island.y - work.y
        let toBottom = work.bottom - island.bottom
        let toLeft = island.x - work.x
        let toRight = work.right - island.right

        let nearest = min(min(toTop, toBottom), min(toLeft, toRight))

        // Top and bottom win ties: a wide island reads better along a horizontal edge, and the
        // default place is the top.
        if nearest == toTop {
            return IslandPosition(.top, alongX(island, work))
        }
        if nearest == toBottom {
            return IslandPosition(.bottom, alongX(island, work))
        }
        return nearest == toLeft
            ? IslandPosition(.left, alongY(island, work))
            : IslandPosition(.right, alongY(island, work))
    }

    /// How far a new edge has to be closer than the current one before the island in hand switches
    /// to it. Without it the landing strip flickers between two edges near a corner.
    public static let edgeStickiness = 14.0

    /// The edge nearest to the middle of an island in hand. The middle, not the sides: the island
    /// changes shape when it turns from a row into a column, and the sides jump with it.
    public static func nearestEdge(
        _ centreX: Double, _ centreY: Double, _ work: Box, _ current: ScreenEdge? = nil
    ) -> ScreenEdge {
        func distanceTo(_ edge: ScreenEdge) -> Double {
            switch edge {
            case .top: return centreY - work.y
            case .bottom: return work.bottom - centreY
            case .left: return centreX - work.x
            case .right: return work.right - centreX
            }
        }

        // Top and bottom first, so they win a tie, as in nearest.
        var best = ScreenEdge.top
        for edge in [ScreenEdge.bottom, .left, .right] where distanceTo(edge) < distanceTo(best) {
            best = edge
        }

        if let held = current, distanceTo(held) - distanceTo(best) < edgeStickiness {
            return held
        }
        return best
    }

    /// The saved position for an island of this size whose middle is let go here, on this edge.
    public static func dropAt(
        _ edge: ScreenEdge, _ centreX: Double, _ centreY: Double, _ width: Double, _ height: Double, _ work: Box
    ) -> IslandPosition {
        isHorizontal(edge)
            ? IslandPosition(edge, share(centreX - (width / 2), work.x, work.width - width))
            : IslandPosition(edge, share(centreY - (height / 2), work.y, work.height - height))
    }

    /// Where an island of this size goes for a saved position, pressed against its edge and kept
    /// inside the working area.
    public static func place(_ position: IslandPosition, _ width: Double, _ height: Double, _ work: Box) -> Box {
        let along = min(max(position.along, 0), 1)

        switch position.edge {
        case .top: return Box(spreadX(along, width, work), work.y, width, height)
        case .bottom: return Box(spreadX(along, width, work), work.bottom - height, width, height)
        case .left: return Box(work.x, spreadY(along, height, work), width, height)
        case .right: return Box(work.right - width, spreadY(along, height, work), width, height)
        }
    }

    /// Along a top or bottom edge the rings stand in a row; along a left or right edge they stand
    /// in a column, so the island takes less room along the edge.
    public static func isHorizontal(_ edge: ScreenEdge) -> Bool {
        edge == .top || edge == .bottom
    }

    private static func alongX(_ island: Box, _ work: Box) -> Double {
        share(island.x, work.x, work.width - island.width)
    }

    private static func alongY(_ island: Box, _ work: Box) -> Double {
        share(island.y, work.y, work.height - island.height)
    }

    /// An island as wide as the screen has nowhere to slide, so it sits at the start.
    private static func share(_ at: Double, _ from: Double, _ room: Double) -> Double {
        room <= 0 ? 0 : min(max((at - from) / room, 0), 1)
    }

    private static func spreadX(_ along: Double, _ width: Double, _ work: Box) -> Double {
        work.x + (max(0, work.width - width) * along)
    }

    private static func spreadY(_ along: Double, _ height: Double, _ work: Box) -> Double {
        work.y + (max(0, work.height - height) * along)
    }
}
