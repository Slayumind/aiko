using System.Windows;

namespace Aiko.App;

/// Tells an element that draws on pixels when layout may have moved it. It listens only while the
/// element is in a window and acts only while it is visible.
internal static class PixelWatch
{
    public static void Follow(FrameworkElement element, Action check)
    {
        void Moved(object? sender, EventArgs e)
        {
            if (element.IsVisible)
            {
                check();
            }
        }

        element.Loaded += (_, _) =>
        {
            element.LayoutUpdated -= Moved;
            element.LayoutUpdated += Moved;
        };
        element.Unloaded += (_, _) => element.LayoutUpdated -= Moved;
    }
}
