using System.Windows;

namespace Aiko.App;

/// The tips of every window share one look (Theme/ToolTip.xaml). The style is merged into the
/// application before any window is made, so the island, which takes only the tokens, gets it too.
static class Tips
{
    public static Application Install(Application application)
    {
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Aiko.App;component/Theme/ToolTip.xaml", UriKind.Absolute),
        });
        return application;
    }
}
