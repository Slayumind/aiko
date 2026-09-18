import Foundation

/// The island in hand: when a press becomes a drag, and where the island goes while it is held.
///
/// The rules of IslandWindow.xaml.cs, with no window in sight. The grip is kept as a share of the
/// island's size, because the island changes shape in hand: a row that turns into a column has to
/// stay under the same spot of the pointer.
public struct IslandDrag: Sendable, Equatable {
    /// Further than this from where the press began, and it is a drag and not a click.
    public static let startsAfter = 3.0

    private let pressed: Point
    private let gripX: Double
    private let gripY: Double

    public private(set) var isDragging = false

    public init(pressedAt: Point, island: Box) {
        pressed = pressedAt
        gripX = island.width > 0 ? (pressedAt.x - island.x) / island.width : 0.5
        gripY = island.height > 0 ? (pressedAt.y - island.y) / island.height : 0.5
    }

    /// The pointer moved. True once the press has become a drag, and from then on it stays true.
    @discardableResult
    public mutating func hold(_ cursor: Point) -> Bool {
        if !isDragging,
           abs(cursor.x - pressed.x) >= IslandDrag.startsAfter
            || abs(cursor.y - pressed.y) >= IslandDrag.startsAfter {
            isDragging = true
        }

        return isDragging
    }

    /// Where the top left corner of an island of this size goes with the pointer here.
    public func corner(_ cursor: Point, _ size: Extent) -> Point {
        Point(cursor.x - (size.width * gripX), cursor.y - (size.height * gripY))
    }

    /// The island of this size held at this point, as a box, ready for IslandPlacement.
    public func held(_ cursor: Point, _ size: Extent) -> Box {
        let corner = corner(cursor, size)
        return Box(corner.x, corner.y, size.width, size.height)
    }
}
