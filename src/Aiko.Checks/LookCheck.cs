using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.App;
using Aiko.Core;

namespace Aiko.Checks;

/// --try-look report.txt: layout faults of the new look that a snapshot of one moment can miss.
/// A style of the window must reach what a frame holds, a frame must close the gap of a label that
/// went away, and the island must keep the size of its content in any window.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class LookCheck
{
    private const double IslandMaxWidth = 400;
    private const double IslandMaxHeight = 100;

    public static int Run(string reportPath)
    {
        var application = Tips.Install(new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown });
        var report = new List<string>();
        var failed = false;
        void Check(string what, bool ok, string detail = "")
        {
            failed |= !ok;
            report.Add($"{(ok ? "PASS" : "FAIL")} {what}{(detail.Length > 0 ? $" ({detail})" : "")}");
        }

        CheckFrameStyle(Check);
        CheckFrameGap(Check);
        CheckIsland(Check);

        File.WriteAllLines(reportPath, report);
        application.Shutdown();
        return failed ? 1 : 0;
    }

    private static Window Offscreen(UIElement content) => new()
    {
        Width = 600,
        Height = 300,
        Left = -4000,
        Top = -4000,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        Content = content,
    };

    /// The terminal control takes the thin scroll bar by its type, through the frame around it.
    private static void CheckFrameStyle(Action<string, bool, string> check)
    {
        var thin = new Style(typeof(ScrollBar));
        var root = new Grid();
        root.Resources[typeof(ScrollBar)] = thin;
        var inside = new Grid();
        root.Children.Add(new TitledFrame { Content = inside });
        var window = Offscreen(root);
        window.Show();

        var terminal = new Microsoft.Terminal.Wpf.TerminalControl();
        inside.Children.Add(terminal);
        window.UpdateLayout();
        var bar = Descendants(terminal).OfType<ScrollBar>().FirstOrDefault();
        check("the scroll bar of the terminal takes the style of the window", bar is not null && ReferenceEquals(bar.Style, thin), "");
        window.Close();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = VisualTreeHelper.GetChild(parent, at);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    /// A label that goes away leaves a whole line, with no change of the frame's size to redraw it.
    private static void CheckFrameGap(Action<string, bool, string> check)
    {
        var frame = new TitledFrame
        {
            Title = new TextBlock { Text = "Session" },
            Aside = new TextBlock { Text = "Done" },
        };
        var window = Offscreen(frame);
        window.Show();
        window.UpdateLayout();

        frame.Aside = null;
        window.UpdateLayout();

        var width = (int)frame.ActualWidth;
        var bitmap = new RenderTargetBitmap(width, (int)frame.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(frame);

        // The top line is the first row with paint in the middle of the frame.
        var pixels = new byte[width * 4];
        byte Alpha(int x) => pixels[(x * 4) + 3];
        for (var row = 0; row < 40; row++)
        {
            bitmap.CopyPixels(new Int32Rect(0, row, width, 1), pixels, width * 4, 0);
            if (Alpha(width / 2) > 0)
            {
                break;
            }
        }

        check("the top line closes where a label was", Alpha(width - 24) == Alpha(width / 2) && Alpha(width / 2) > 0, $"gap {Alpha(width - 24)}, line {Alpha(width / 2)}");
        window.Close();
    }

    private static void CheckIsland(Action<string, bool, string> check)
    {
        var island = new IslandWindow();
        island.Show(CardSnapshot.Example(), IslandPosition.Default);
        island.UpdateLayout();
        var (width, height) = (island.ActualWidth, island.ActualHeight);
        island.Close();
        check("the island is as small as its content", width < IslandMaxWidth && height < IslandMaxHeight, $"{width:0}x{height:0}");

        // The panel once stood in a large window: its shape must not hold that size.
        var panel = new IslandPanel();
        panel.Show(CardSnapshot.Example(), ScreenEdge.Top);
        var window = Offscreen(panel);
        window.Width = 1200;
        window.Height = 700;
        window.Show();
        window.UpdateLayout();
        panel.MarkForMeasure();
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = panel.DesiredSize;
        window.Close();
        check("the shape of the island does not keep a larger size", size.Width < IslandMaxWidth && size.Height < IslandMaxHeight, $"{size.Width:0}x{size.Height:0}");
    }
}
