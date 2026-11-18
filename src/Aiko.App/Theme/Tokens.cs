using System.Windows;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// The one place that reads the palette. Loaded straight from the dictionary, so a colour is
/// never written down twice.
public static class Tokens
{
    private static readonly ResourceDictionary Dictionary = new()
    {
        Source = new Uri("pack://application:,,,/Aiko.App;component/Theme/Tokens.xaml", UriKind.Absolute),
    };

    public static Brush Brush(string key) => (Brush)Dictionary[key];

    /// Any token, for controls built in code: they are drawn before they sit in a window, and
    /// FindResource finds nothing there.
    public static T Get<T>(string key) => (T)Dictionary[key];

    public static Color Colour(string key) => (Color)Dictionary[key];

    public static Brush ToneBrush(LimitTone tone) => tone switch
    {
        LimitTone.Normal => Brush("Ink"),
        LimitTone.Caution => Brush("LookLimitSoft"),
        LimitTone.Critical => Brush("LookLimit"),
        _ => Brush("Muted"),
    };
}
