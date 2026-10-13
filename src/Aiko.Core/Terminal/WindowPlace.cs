namespace Aiko.Core.Terminal;

/// The part of a screen a window may take, in the units the window is placed in.
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// Where the window of the terminal stands (D-292).
public readonly record struct WindowPlace(double Left, double Top, double Width, double Height, bool Maximized)
{
    public const double DefaultWidth = 1100;
    public const double DefaultHeight = 760;
    public const double MinWidth = 480;
    public const double MinHeight = 360;

    /// Below this width the list of sessions goes under a button (D-308).
    public const double NarrowBelow = 720;

    /// How much of the title bar has to be on a screen for the window to be reachable.
    private const double Grip = 80;
    private const double TitleHeight = 40;

    public static bool IsNarrow(double width) => width < NarrowBelow;

    /// The place to open the window at. A saved place is kept when its title bar can still be
    /// reached on one of the screens; a screen may be gone since. Otherwise, and the first time,
    /// the window stands in the middle of the first screen. screens is never empty.
    public static WindowPlace Fit(WindowPlace? saved, IReadOnlyList<ScreenArea> screens)
    {
        if (saved is { } place && IsSane(place) && screens.FirstOrNull(screen => Reaches(place, screen)) is { } home)
        {
            var width = Math.Clamp(place.Width, MinWidth, Math.Max(MinWidth, home.Width));
            var height = Math.Clamp(place.Height, MinHeight, Math.Max(MinHeight, home.Height));
            return new WindowPlace(place.Left, Math.Max(place.Top, home.Top), width, height, place.Maximized);
        }

        var first = screens[0];
        var defaultWidth = Math.Min(DefaultWidth, first.Width);
        var defaultHeight = Math.Min(DefaultHeight, first.Height);
        return new WindowPlace(
            Math.Floor(first.Left + (first.Width - defaultWidth) / 2),
            Math.Floor(first.Top + (first.Height - defaultHeight) / 2),
            defaultWidth,
            defaultHeight,
            saved?.Maximized ?? false);
    }

    private static bool IsSane(WindowPlace place) =>
        double.IsFinite(place.Left) && double.IsFinite(place.Top) && double.IsFinite(place.Width) && double.IsFinite(place.Height)
        && place.Width > 0 && place.Height > 0;

    private static bool Reaches(WindowPlace place, ScreenArea screen)
    {
        var across = Math.Min(place.Left + place.Width, screen.Right) - Math.Max(place.Left, screen.Left);
        var down = Math.Min(place.Top + TitleHeight, screen.Bottom) - Math.Max(place.Top, screen.Top);
        return across >= Grip && down >= TitleHeight / 2;
    }
}

internal static class ScreenAreas
{
    public static ScreenArea? FirstOrNull(this IReadOnlyList<ScreenArea> screens, Func<ScreenArea, bool> wanted)
    {
        foreach (var screen in screens)
        {
            if (wanted(screen))
            {
                return screen;
            }
        }

        return null;
    }
}
