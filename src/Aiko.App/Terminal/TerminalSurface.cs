using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aiko.Core.Terminal;
using Microsoft.Terminal.Wpf;

namespace Aiko.App;

/// The terminal control of Windows Terminal with one program in it (D-360). The control draws
/// with its own renderer in its own window, so the software rendering of WPF does not touch it.
///
/// The control never tells its connection how big it is: without the size the pseudo console stays
/// 120×30 and the screen of Claude Code breaks at the bottom. The surface passes the size on after
/// every change of layout.
[SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalSurface : Grid
{
    /// Cascadia Mono ships with Windows 11. Geist Mono is only a resource of Aiko, and the
    /// renderer of the control cannot see fonts that are not installed.
    private const string Font = "Cascadia Mono";

    /// The zoom of the window in percent (D-384), set by the host for all terminals. The control knows
    /// nothing of it: it takes its font in points, turned into pixels by the screen's DPI, and counts
    /// its rows and columns from its own size in units of WPF. So the font comes from
    /// WindowZoom.TerminalPoints (D-383), and the surface undoes the scale of the window for itself.
    public static int Zoom { get; set; } = WindowZoom.Default;

    /// The font this terminal was given, to give it again only when it changed.
    private short _points;

    private readonly TerminalControl _control = new();
    private readonly PseudoConsole _console;

    /// Read only on the thread of the output, one piece after another.
    private readonly TitleReader _titles = new();
    private bool _connected;

    /// Space between the text and the left edge, in units.
    private const double TextInset = 12;

    /// Set on the thread of the output, once.
    private volatile bool _drawn;

    public TerminalSurface(PtyLaunch launch)
    {
        _console = new PseudoConsole(launch);
        _console.Exited += code => Dispatcher.BeginInvoke(() => Exited?.Invoke(code));
        _console.Typed += () => Dispatcher.BeginInvoke(() => Typed?.Invoke());
        _console.TerminalOutput += (_, e) =>
        {
            if (_titles.Read(e.Data) is { } title)
            {
                Dispatcher.BeginInvoke(() => TitleChanged?.Invoke(title));
            }

            if (!_drawn && TerminalScreen.HasText(e.Data))
            {
                _drawn = true;
                Dispatcher.BeginInvoke(() => DrawnFirst?.Invoke());
            }
        };

        // A little air between the text and the edges of the window; the background around the
        // control is the background of the terminal, so the two read as one.
        Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));

        // Tab and the arrows are the program's, not ways to move the focus of the window (D-370).
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
        KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.None);
        UseZoom();
        Children.Add(_control);

        Loaded += (_, _) =>
        {
            // Given again on every load: the control may come back without it.
            UseZoom(always: true);
            // The surface is loaded again each time the person comes back to the session.
            if (!_connected)
            {
                _connected = true;
                _control.Connection = _console;
            }

            Dispatcher.BeginInvoke(SyncSize, DispatcherPriority.Background);
            Dispatcher.BeginInvoke(AcceptDrops, DispatcherPriority.Background);
        };
        _control.SizeChanged += (_, _) => Dispatcher.BeginInvoke(SyncSize, DispatcherPriority.Background);
    }

    /// The program ended, with its exit code. Raised on the window's thread.
    public event Action<int>? Exited;

    /// The program set the title of the terminal (OSC 0 or 2). Raised on the window's thread.
    public event Action<string>? TitleChanged;

    /// The person typed into the program. Raised on the window's thread.
    public event Action? Typed;

    /// The program drew its first letter: until then a loader stands in place of the terminal
    /// (D-368). Raised on the window's thread.
    public event Action? DrawnFirst;

    public bool Drawn => _drawn;

    public bool IsRunning => _console.IsRunning;

    /// Text as if it was typed: keys of the editor that the terminal should get.
    public void Type(string text) => _console.WriteInput(text);

    public void FocusTerminal() => _control.Focus();

    /// Follows Zoom. The window grows the panel with one scale; the surface takes the opposite scale,
    /// so the control lays out at its true size on the screen and counts as many columns as fit.
    /// Without it, at 130 % the control counted columns for a width 1.3 times smaller and Claude
    /// Code filled three quarters of the terminal. The gap to the text keeps the zoom.
    public void UseZoom(bool always = false)
    {
        var scale = WindowZoom.Scale(Zoom);
        LayoutTransform = scale == 1 ? Transform.Identity : new ScaleTransform(1 / scale, 1 / scale);
        _control.Margin = new Thickness(TextInset * scale, (TextInset - 4) * scale, 0, 0);

        var points = (short)WindowZoom.TerminalPoints(Zoom);
        if (!IsLoaded || (!always && _points == points))
        {
            return;
        }

        // The columns change with the font, so the pseudo console hears the new size.
        _points = points;
        _control.SetTheme(Theme, Font, _points, Color.FromRgb(0x0A, 0x0A, 0x0A));
        Dispatcher.BeginInvoke(SyncSize, DispatcherPriority.Background);
    }

    /// What the mouse selected in the terminal, ready for the clipboard, or null. The rows Claude
    /// Code wrapped by the width are joined again (D-371).
    public string? SelectedText
    {
        get
        {
            var text = _control.GetSelectedText();
            return string.IsNullOrEmpty(text)
                ? null
                : TerminalCopy.Unwrap(text, _control.Columns).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
        }
    }

    /// Ends the program and everything it started.
    public void Close() => _console.Dispose();

    /// The picture that stands in for the control while the window moves it, or null: the sharp
    /// picture, its blurred copy over it and a dark veil over both.
    private Grid? _still;
    private Image? _blurred;
    private Border? _veil;
    private bool _focusedBeforeStill;

    /// The blur and the veil come in at the start of a motion and go before the real terminal
    /// returns, so the swap happens on a sharp picture and cannot be seen.
    private static readonly Duration HazeTime = new(TimeSpan.FromMilliseconds(140));
    private const double VeilAt = 0.35;

    /// The blurred copy is the picture made this many times smaller and stretched back: a blur that
    /// costs one small bitmap, not a blur effect drawn on every frame of the software renderer.
    private const double BlurDown = 10;

    /// The terminal becomes a picture of itself. The control is a window of its own: moved on every
    /// frame of a motion, it lags behind the drawing of WPF and jerks. A picture moves with WPF. The
    /// control stays in the layout at its size, so the pseudo console gets no resize. The control
    /// hides only after the picture is drawn: hidden at once, it left one empty frame.
    public void ShowStill()
    {
        // The real terminal is back and the old picture waits to go: it goes now, a new one is taken.
        if (_still is not null && !_clearing && _control.Visibility == Visibility.Visible)
        {
            Children.Remove(_still);
            _still = null;
            _blurred = null;
            _veil = null;
        }

        // A motion that starts while the haze clears takes the haze back: the picture still stands.
        if (_still is not null)
        {
            if (_clearing)
            {
                _clearing = false;
                Haze(in_: true);
            }

            return;
        }

        if (!IsVisible || _control.ActualWidth <= 0 || _control.ActualHeight <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var corner = _control.PointToScreen(new Point());
        var area = new Int32Rect(
            (int)Math.Round(corner.X),
            (int)Math.Round(corner.Y),
            (int)Math.Round(_control.ActualWidth * dpi.DpiScaleX),
            (int)Math.Round(_control.ActualHeight * dpi.DpiScaleY));
        if (ScreenPicture.Of(area) is not { } picture)
        {
            return;
        }

        var width = area.Width / dpi.DpiScaleX;
        var height = area.Height / dpi.DpiScaleY;
        var sharp = new Image { Source = picture, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(sharp, BitmapScalingMode.NearestNeighbor);
        var small = new System.Windows.Media.Imaging.TransformedBitmap(picture, new ScaleTransform(1 / BlurDown, 1 / BlurDown));
        small.Freeze();
        _blurred = new Image { Source = small, Stretch = Stretch.Fill, Opacity = 0 };
        RenderOptions.SetBitmapScalingMode(_blurred, BitmapScalingMode.HighQuality);
        _veil = new Border { Background = Background, Opacity = 0 };
        _still = new Grid
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = _control.Margin,
            IsHitTestVisible = false,
            Children = { sharp, _blurred, _veil },
        };
        _focusedBeforeStill = IsKeyboardFocusWithin;
        Children.Add(_still);

        AfterFrames(2, () =>
        {
            if (_still is null || _clearing)
            {
                return;
            }

            _control.Visibility = Visibility.Hidden;
            if (Motion.IsOn)
            {
                Haze(in_: true);
            }
        });
    }

    /// True while the haze clears before the real terminal returns.
    private bool _clearing;

    /// The blur and the veil come in, or go; going, it returns the real terminal at the end.
    private void Haze(bool in_, Action? then = null)
    {
        if (_blurred is null || _veil is null)
        {
            then?.Invoke();
            return;
        }

        var easing = in_ ? Motion.Standard : Motion.Exit;
        var blur = new System.Windows.Media.Animation.DoubleAnimation(in_ ? 1 : 0, HazeTime) { EasingFunction = easing };
        if (then is not null)
        {
            blur.Completed += (_, _) => then();
        }

        _blurred.BeginAnimation(OpacityProperty, blur);
        _veil.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(in_ ? VeilAt : 0, HazeTime) { EasingFunction = easing });
    }

    /// The real terminal again, in its new place: the blur and the veil go first, the control comes
    /// back over the sharp picture, and the picture goes once the control has drawn itself.
    public void ShowLive()
    {
        if (_still is not { } still)
        {
            return;
        }

        void Return()
        {
            // A new motion took the haze back while it cleared: the picture stays.
            if (!_clearing)
            {
                return;
            }

            _clearing = false;
            _control.Visibility = Visibility.Visible;
            if (_focusedBeforeStill)
            {
                FocusTerminal();
            }

            AfterFrames(2, () =>
            {
                if (_control.Visibility != Visibility.Visible)
                {
                    return;
                }

                Children.Remove(still);
                if (ReferenceEquals(_still, still))
                {
                    _still = null;
                    _blurred = null;
                    _veil = null;
                }
            });
        }

        _clearing = true;
        if (!Motion.IsOn)
        {
            Return();
            return;
        }

        Haze(in_: false, then: Return);
    }

    /// Runs after this many frames have been drawn.
    private static void AfterFrames(int frames, Action then)
    {
        var left = frames;
        void OnFrame(object? sender, EventArgs e)
        {
            if (--left > 0)
            {
                return;
            }

            CompositionTarget.Rendering -= OnFrame;
            then();
        }

        CompositionTarget.Rendering += OnFrame;
    }

    /// Files dropped on the terminal reach the window: its window is not WPF's, so WPF never sees
    /// the drop (D-370).
    private void AcceptDrops()
    {
        foreach (var host in Descendants(_control).OfType<System.Windows.Interop.HwndHost>())
        {
            if (host.Handle != IntPtr.Zero)
            {
                WindowKeys.AcceptDrops(host.Handle);
            }
        }
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

    /// The size last passed to the pseudo console.
    private (int Rows, int Columns) _sent;

    /// Only a new count of rows or columns reaches the pseudo console. A layout that moves the
    /// terminal by a part of a pixel, as the header does when it opens, would otherwise make Claude
    /// Code draw its whole screen again for nothing.
    private void SyncSize()
    {
        var size = (_control.Rows, _control.Columns);
        if (size.Rows > 0 && size.Columns > 0 && size != _sent)
        {
            _sent = size;
            _console.Resize((uint)size.Rows, (uint)size.Columns);
        }
    }

    /// One scheme of sixteen colours from the palette of slayumind (D-295). The palette has no
    /// grey between muted and raised that text can be read in, so bright black is the one colour
    /// added: halfway between them.
    private static readonly TerminalTheme Theme = new()
    {
        DefaultBackground = Rgb(0x0A0A0A),
        DefaultForeground = Rgb(0xFAFAFA),
        DefaultSelectionBackground = Rgb(0x404040),
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable =
        [
            Rgb(0x0A0A0A), Rgb(0xFF6467), Rgb(0x00BC7D), Rgb(0xFE9A00), Rgb(0x11A9F6), Rgb(0xD477CD), Rgb(0x00D3F2), Rgb(0xA1A1A1),
            Rgb(0x737373), Rgb(0xFF8270), Rgb(0xAFE773), Rgb(0xFE9A00), Rgb(0x11A9F6), Rgb(0xF7BCF0), Rgb(0x24DCC3), Rgb(0xFAFAFA),
        ],
    };

    /// 0xRRGGBB to the COLORREF of Windows, 0x00BBGGRR.
    private static uint Rgb(uint hex) => ((hex & 0xFF) << 16) | (hex & 0xFF00) | ((hex >> 16) & 0xFF);
}
