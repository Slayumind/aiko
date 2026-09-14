using Aiko.Core;

namespace Aiko.Core.Tests;

public class IslandPlacementTests
{
    /// A 1920 by 1080 screen with the taskbar at the bottom.
    private static readonly Box Work = new(0, 0, 1920, 1040);

    private const double Width = 160;
    private const double Height = 34;

    [Fact]
    public void An_island_near_the_top_sticks_to_the_top()
    {
        var dropped = new Box(880, 12, Width, Height);

        Assert.Equal(ScreenEdge.Top, IslandPlacement.Nearest(dropped, Work).Edge);
    }

    [Fact]
    public void An_island_near_the_right_sticks_to_the_right()
    {
        var dropped = new Box(1740, 500, Width, Height);

        Assert.Equal(ScreenEdge.Right, IslandPlacement.Nearest(dropped, Work).Edge);
    }

    [Fact]
    public void An_island_near_the_bottom_sticks_to_the_bottom()
    {
        var dropped = new Box(400, 1000, Width, Height);

        Assert.Equal(ScreenEdge.Bottom, IslandPlacement.Nearest(dropped, Work).Edge);
    }

    [Fact]
    public void The_middle_of_the_top_edge_is_a_half()
    {
        var middle = new Box((Work.Width - Width) / 2, 0, Width, Height);

        Assert.Equal(0.5, IslandPlacement.Nearest(middle, Work).Along, 3);
    }

    [Fact]
    public void The_place_is_kept_as_a_share_so_a_smaller_screen_keeps_it()
    {
        var position = new IslandPosition(ScreenEdge.Top, 0.5);

        var onBig = IslandPlacement.Place(position, Width, Height, Work);
        var onSmall = IslandPlacement.Place(position, Width, Height, new Box(0, 0, 1280, 700));

        Assert.Equal(Work.CentreX, onBig.CentreX, 3);
        Assert.Equal(640, onSmall.CentreX, 3);
    }

    [Fact]
    public void A_saved_place_comes_back_the_same()
    {
        var dropped = new Box(1500, 3, Width, Height);

        var position = IslandPlacement.Nearest(dropped, Work);
        var back = IslandPlacement.Place(position, Width, Height, Work);

        Assert.Equal(dropped.X, back.X, 3);
        Assert.Equal(Work.Y, back.Y, 3);
    }

    [Fact]
    public void The_island_is_pressed_against_its_edge()
    {
        Assert.Equal(Work.Y, IslandPlacement.Place(new IslandPosition(ScreenEdge.Top, 0.3), Width, Height, Work).Y, 3);
        Assert.Equal(Work.Bottom - Height, IslandPlacement.Place(new IslandPosition(ScreenEdge.Bottom, 0.3), Width, Height, Work).Bottom - Height, 3);
        Assert.Equal(Work.X, IslandPlacement.Place(new IslandPosition(ScreenEdge.Left, 0.3), Width, Height, Work).X, 3);
        Assert.Equal(Work.Right - Width, IslandPlacement.Place(new IslandPosition(ScreenEdge.Right, 0.3), Width, Height, Work).X, 3);
    }

    [Fact]
    public void An_island_wider_than_the_screen_has_nowhere_to_slide()
    {
        var narrow = new Box(0, 0, 100, 800);

        var placed = IslandPlacement.Place(new IslandPosition(ScreenEdge.Top, 0.8), Width, Height, narrow);

        Assert.Equal(narrow.X, placed.X, 3);
    }

    [Fact]
    public void A_share_outside_the_screen_is_pulled_back_in()
    {
        var placed = IslandPlacement.Place(new IslandPosition(ScreenEdge.Top, 4), Width, Height, Work);

        Assert.Equal(Work.Right - Width, placed.X, 3);
    }

    [Fact]
    public void Rings_stand_in_a_row_on_top_and_bottom_and_in_a_column_on_the_sides()
    {
        Assert.True(IslandPlacement.IsHorizontal(ScreenEdge.Top));
        Assert.True(IslandPlacement.IsHorizontal(ScreenEdge.Bottom));
        Assert.False(IslandPlacement.IsHorizontal(ScreenEdge.Left));
        Assert.False(IslandPlacement.IsHorizontal(ScreenEdge.Right));
    }

    [Fact]
    public void A_second_screen_to_the_left_has_negative_coordinates_and_still_works()
    {
        // Windows puts a monitor left of the main one at a negative x.
        var second = new Box(-1920, 0, 1920, 1080);
        var dropped = new Box(-1000, 8, Width, Height);

        var position = IslandPlacement.Nearest(dropped, second);
        var back = IslandPlacement.Place(position, Width, Height, second);

        Assert.Equal(ScreenEdge.Top, position.Edge);
        Assert.Equal(dropped.X, back.X, 3);
    }

    // ---- the island in hand ----

    [Theory]
    [InlineData(960, 30, ScreenEdge.Top)]
    [InlineData(960, 1000, ScreenEdge.Bottom)]
    [InlineData(40, 520, ScreenEdge.Left)]
    [InlineData(1880, 520, ScreenEdge.Right)]
    public void The_landing_strip_goes_to_the_edge_nearest_the_middle(double x, double y, ScreenEdge edge)
    {
        Assert.Equal(edge, IslandPlacement.NearestEdge(x, y, Work));
    }

    [Fact]
    public void Near_a_corner_the_edge_in_use_holds_until_another_is_clearly_closer()
    {
        // 60 from the top, 50 from the left: the left is closer, but by less than the stickiness.
        Assert.Equal(ScreenEdge.Top, IslandPlacement.NearestEdge(50, 60, Work, ScreenEdge.Top));
        Assert.Equal(ScreenEdge.Left, IslandPlacement.NearestEdge(50, 60, Work));

        // 80 from the top: now the left is closer by 30, and the strip moves.
        Assert.Equal(ScreenEdge.Left, IslandPlacement.NearestEdge(50, 80, Work, ScreenEdge.Top));
    }

    [Fact]
    public void Letting_go_keeps_the_place_along_the_edge_where_the_middle_was()
    {
        var position = IslandPlacement.DropAt(ScreenEdge.Top, 960, 200, Width, Height, Work);
        var placed = IslandPlacement.Place(position, Width, Height, Work);

        Assert.Equal(ScreenEdge.Top, position.Edge);
        Assert.Equal(960, placed.CentreX, 3);
        Assert.Equal(0, placed.Y);
    }

    [Fact]
    public void Letting_go_past_the_end_of_an_edge_stops_at_the_end()
    {
        var position = IslandPlacement.DropAt(ScreenEdge.Right, 1900, 1200, 34, 60, Work);

        Assert.Equal(1, position.Along);
        Assert.Equal(1040 - 60, IslandPlacement.Place(position, 34, 60, Work).Y, 3);
    }
}
