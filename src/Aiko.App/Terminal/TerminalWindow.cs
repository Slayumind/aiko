using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The window of the terminal (D-292): an ordinary window the system sees, with Aiko's own title
/// bar in the place of the standard one.
///
/// WindowChrome keeps what Windows gives a window for free: dragging by the title bar, resizing by
/// the edges, a double click to maximize, Snap and the rounded corners of Windows 11.
sealed class TerminalWindow : Window
{
    /// A maximized window with its own frame goes past the work area by the width of the frame
    /// it does not draw (spike 2026-10-13: 11 px on each side at 150%). The content steps back.
    private const double MaximizedInset = 8;

    private readonly Border _root = new();
    private readonly TerminalPanel _panel;

    public TerminalWindow(TerminalPanel panel, WindowPlace place)
    {
        _panel = panel;
        Title = Strings.TerminalTitle;
        Background = Tokens.Brush("Background");
        MinWidth = WindowPlace.MinWidth;
        MinHeight = WindowPlace.MinHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = place.Left;
        Top = place.Top;
        Width = place.Width;
        Height = place.Height;
        UseLayoutRounding = true;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = TerminalPanel.BarHeight,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        _root.Child = panel;
        Content = _root;

        panel.MinimizeAsked += () => WindowState = WindowState.Minimized;
        panel.MaximizeAsked += () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        panel.CloseAsked += Close;
        StateChanged += (_, _) => FollowState();

        if (place.Maximized)
        {
            WindowState = WindowState.Maximized;
        }

        FollowState();
    }

    /// Whether the window is on screen: open and not minimized.
    public bool InView => IsVisible && WindowState != WindowState.Minimized;

    /// Says no to closing while something still works; the host then asks the person.
    public Func<bool>? MayClose { get; set; }

    /// Where the window stands now, to open it there the next time.
    public WindowPlace Place =>
        WindowState == WindowState.Normal
            ? new WindowPlace(Left, Top, Width, Height, false)
            : new WindowPlace(RestoreBounds.Left, RestoreBounds.Top, RestoreBounds.Width, RestoreBounds.Height, WindowState == WindowState.Maximized);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MayClose is { } may && !may())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void FollowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        _root.Padding = new Thickness(maximized ? MaximizedInset : 0);
        _panel.ShowMaximized(maximized);
    }
}
