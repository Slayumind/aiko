using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Aiko.App;

/// The zoom of the terminal window is a transform on its panel. A popup is a window of its own,
/// outside that panel, so without help a menu, a list or a tip over a zoomed window comes out at
/// 100%. Every popup takes the zoom of the place it opens from: the popups of Aiko through Fit,
/// the menus and the tips through their styles in Controls.xaml.
static class PopupZoom
{
    /// For the styles of menus and tips: their PlacementTarget in, the zoom of that place out.
    public static IValueConverter Converter { get; } = new OfTarget();

    /// The zoom the element is drawn at inside its window; none outside a window.
    public static Transform Of(Visual? element)
    {
        if (element is null || Window.GetWindow(element) is not { } window || !window.IsAncestorOf(element))
        {
            return Transform.Identity;
        }

        var toWindow = element.TransformToAncestor(window);
        var scale = (toWindow.Transform(new Point(1, 0)) - toWindow.Transform(new Point(0, 0))).Length;
        return Math.Abs(scale - 1) < 0.001 ? Transform.Identity : new ScaleTransform(scale, scale);
    }

    /// Called right before the popup opens: its contents take the zoom of its target. Set before
    /// the popup is laid out, so it opens at its size and does not jump.
    public static void Fit(Popup popup)
    {
        if (popup.Child is FrameworkElement child)
        {
            child.LayoutTransform = Of(popup.PlacementTarget);
        }
    }

    private sealed class OfTarget : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Of(value as Visual);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
