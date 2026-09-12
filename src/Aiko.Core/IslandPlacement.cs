namespace Aiko.Core;

/// A rectangle, in the core's own terms. The window layer has its own, but the core must not know
/// about it: the same rules will place the island on macOS one day.
public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CentreX => X + (Width / 2);
    public double CentreY => Y + (Height / 2);
}

public enum ScreenEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// Where the island sits: which edge it is stuck to, and how far along that edge.
///
/// Along is a share, not a number of pixels, so the island keeps its place when the screen
/// changes size or the user moves to another monitor.
public readonly record struct IslandPosition(ScreenEdge Edge, double Along)
{
    /// The top of the screen, in the middle: where the island starts and where "reset position"
    /// puts it back.
    public static readonly IslandPosition Default = new(ScreenEdge.Top, 0.5);
}

public static class IslandPlacement
{
    /// The edge the island ended up nearest after being dragged, and its share along that edge.
    public static IslandPosition Nearest(Box island, Box work)
    {
        var toTop = island.Y - work.Y;
        var toBottom = work.Bottom - island.Bottom;
        var toLeft = island.X - work.X;
        var toRight = work.Right - island.Right;

        var nearest = Math.Min(Math.Min(toTop, toBottom), Math.Min(toLeft, toRight));

        // Top and bottom win ties: a wide island reads better along a horizontal edge, and the
        // default place is the top.
        if (nearest == toTop)
        {
            return new IslandPosition(ScreenEdge.Top, AlongX(island, work));
        }
        if (nearest == toBottom)
        {
            return new IslandPosition(ScreenEdge.Bottom, AlongX(island, work));
        }
        return nearest == toLeft
            ? new IslandPosition(ScreenEdge.Left, AlongY(island, work))
            : new IslandPosition(ScreenEdge.Right, AlongY(island, work));
    }

    /// Where a island of this size goes for a saved position, pressed against its edge and kept
    /// inside the working area.
    public static Box Place(IslandPosition position, double width, double height, Box work)
    {
        var along = Math.Clamp(position.Along, 0, 1);

        return position.Edge switch
        {
            ScreenEdge.Top => new Box(SpreadX(along, width, work), work.Y, width, height),
            ScreenEdge.Bottom => new Box(SpreadX(along, width, work), work.Bottom - height, width, height),
            ScreenEdge.Left => new Box(work.X, SpreadY(along, height, work), width, height),
            _ => new Box(work.Right - width, SpreadY(along, height, work), width, height),
        };
    }

    /// Along a top or bottom edge the rings stand in a row; along a left or right edge they stand
    /// in a column, so the island takes less room along the edge.
    public static bool IsHorizontal(ScreenEdge edge) => edge is ScreenEdge.Top or ScreenEdge.Bottom;

    private static double AlongX(Box island, Box work) => Share(island.X, work.X, work.Width - island.Width);

    private static double AlongY(Box island, Box work) => Share(island.Y, work.Y, work.Height - island.Height);

    /// An island as wide as the screen has nowhere to slide, so it sits at the start.
    private static double Share(double at, double from, double room) =>
        room <= 0 ? 0 : Math.Clamp((at - from) / room, 0, 1);

    private static double SpreadX(double along, double width, Box work) =>
        work.X + (Math.Max(0, work.Width - width) * along);

    private static double SpreadY(double along, double height, Box work) =>
        work.Y + (Math.Max(0, work.Height - height) * along);
}
