using System.Windows;

namespace Aiko.App;

/// What floats over the window: a menu, a list, a question. The ground, the line and the one shadow
/// of Aiko are set here, so every floating thing looks the same.
internal class FloatingPanel : PixelBorder
{
    public FloatingPanel()
    {
        Background = Tokens.Brush("Background");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        Effect = Tokens.FloatShadow;
    }
}
