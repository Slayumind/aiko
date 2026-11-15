using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The window of the terminal (D-292): an ordinary window the system sees, with Aiko's own title
/// bar in the place of the standard one.
///
/// WindowChrome keeps what Windows gives a window for free: dragging by the title bar, resizing by
/// the edges, a double click to maximize, Snap and the rounded corners of Windows 11.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalWindow : Window
{
    /// A maximized window with its own frame goes past the work area by the width of the frame
    /// it does not draw (spike 2026-10-13: 11 px on each side at 150%). The content steps back.
    private const double MaximizedInset = 8;

    private readonly Border _root = new();
    private readonly TerminalPanel _panel;

    private readonly WindowChrome _chrome = new()
    {
        CaptionHeight = TerminalPanel.BarHeight,
        ResizeBorderThickness = new Thickness(6),
        GlassFrameThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
        UseAeroCaptionButtons = false,
    };

    /// The zoom of the window (D-384): the whole panel grows with one scale, so the text, the icons
    /// and the gaps keep their sizes to each other. Popups that open from it grow with it. The title
    /// bar that drags the window grows too.
    public void UseZoom(int zoom)
    {
        var scale = Aiko.Core.Terminal.WindowZoom.Scale(zoom);
        _panel.LayoutTransform = scale == 1 ? System.Windows.Media.Transform.Identity : new System.Windows.Media.ScaleTransform(scale, scale);
        _chrome.CaptionHeight = TerminalPanel.BarHeight * scale;
    }

    public TerminalWindow(TerminalPanel panel, WindowPlace place)
    {
        _panel = panel;
        Title = Strings.TerminalTitle;
        Background = Tokens.Brush("Surface");
        MinWidth = WindowPlace.MinWidth;
        MinHeight = WindowPlace.MinHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = place.Left;
        Top = place.Top;
        Width = place.Width;
        Height = place.Height;
        UseLayoutRounding = true;

        WindowChrome.SetWindowChrome(this, _chrome);

        // Added after the hook of WindowChrome, so it comes first (HwndSource calls the newest first).
        // A window closed before its first frame has no source any more, and needs no hook.
        ContentRendered += (_, _) => (PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource)?.AddHook(TakeIcon);

        _root.Child = panel;
        Content = _root;

        // The window hears the keys of the conversation first, wherever the keyboard is (D-328).

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
            : new WindowPlace(RestoreBounds.Left, RestoreBounds.Top, RestoreBounds.Width, RestoreBounds.Height, _maximized);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MayClose is { } may && !may())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    /// Whether the window was maximized before it was minimized: closed from the taskbar while
    /// minimized, it opens maximized again.
    private bool _maximized;

    private void FollowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (WindowState != WindowState.Minimized)
        {
            _maximized = maximized;
        }

        _root.Padding = new Thickness(maximized ? MaximizedInset : 0);
        _panel.ShowMaximized(maximized);
    }

    private const int SetIcon = 0x0080;
    private const int GetIcon = 0x007F;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool InSendMessage();
    private const int StyleIndex = -16;
    private const long Caption = 0x00C00000;

    /// WindowChrome takes WS_VISIBLE off the window while Windows stores a new icon, so that Windows
    /// draws no old caption over the custom one. The terminal is a window of its own inside this one:
    /// hidden for that moment, it missed a frame, and the chat flickered on every new icon (RESEARCH,
    /// D-416). Windows draws the old caption only for a window with WS_CAPTION, so this takes off that
    /// style alone for the moment and leaves the window, and the terminal, visible. With the window
    /// left as it was, the old caption showed over the title bar.
    private static IntPtr TakeIcon(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == GetIcon)
        {
            handled = true;
            var answer = DefWindowProc(window, (uint)message, wParam, lParam);
            IconProbe.Asked(wParam, answer, InSendMessage());
            return answer;
        }

        if (message != SetIcon)
        {
            return IntPtr.Zero;
        }

        IconProbe.Set(wParam, lParam);
        handled = true;
        var style = GetWindowLongPtr(window, StyleIndex);
        var plain = (IntPtr)((long)style & ~Caption);
        if (plain == style)
        {
            return DefWindowProc(window, (uint)message, wParam, lParam);
        }

        SetWindowLongPtr(window, StyleIndex, plain);
        try
        {
            return DefWindowProc(window, (uint)message, wParam, lParam);
        }
        finally
        {
            SetWindowLongPtr(window, StyleIndex, style);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
