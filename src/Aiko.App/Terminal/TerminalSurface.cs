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

    /// The screen of a command line and its blocks (D-412); null for Claude Code. The screen is fed
    /// on the thread of the output and read on the window's thread, each time under the gate.
    private readonly ShellScreen? _shell;
    private readonly ShellBlocksView? _blocks;
    private readonly ShellFindBar? _find;
    private readonly object _shellGate = new();
    private int _refreshQueued;

    /// blocks: the output is shown as blocks of commands, and the terminal itself only while a
    /// program takes the whole screen.
    public TerminalSurface(PtyLaunch launch, bool blocks = false)
    {
        _console = new PseudoConsole(launch);
        _console.Exited += code => Dispatcher.BeginInvoke(() => Exited?.Invoke(code));
        _console.Typed += () => Dispatcher.BeginInvoke(() => Typed?.Invoke());
        _console.TerminalOutput += (_, e) =>
        {
            Interlocked.Exchange(ref _outputAt, Clock.ElapsedMilliseconds);
            _outputAfterResize = true;
            if (_titles.Read(e.Data) is { } title)
            {
                Dispatcher.BeginInvoke(() => TitleChanged?.Invoke(title));
            }

            if (_shell is not null)
            {
                FeedShell(e.Data);
            }

            if (!_drawn && TerminalScreen.HasText(e.Data))
            {
                _drawn = true;
                Dispatcher.BeginInvoke(() => DrawnFirst?.Invoke());
            }
        };

        // A little air between the text and the edges of the window; the background around the
        // control is the background of the terminal, so the two read as one.
        Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));

        // Tab and the arrows are the program's, not ways to move the focus of the window (D-370).
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
        KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.None);
        Children.Add(_control);
        if (blocks)
        {
            _shell = new ShellScreen(120, 30);
            _blocks = new ShellBlocksView(_shell, _shellGate);
            _shell.FullScreenChanged += on => Dispatcher.BeginInvoke(() => ShowFullScreen(on));

            // Hidden, not collapsed: the control keeps its size, so the pseudo console keeps its rows.
            _control.Visibility = Visibility.Hidden;
            Children.Add(_blocks);
            _find = new ShellFindBar(_blocks);
            _find.Closed += () => FindClosed?.Invoke();
            Children.Add(_find);
        }

        UseZoom();

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

    /// A program took the whole screen, or gave it back: the keys go to the terminal while it has it.
    /// Raised on the window's thread.
    public event Action<bool>? FullScreenChanged;

    /// The program set the title of the terminal (OSC 0 or 2). Raised on the window's thread.
    public event Action<string>? TitleChanged;

    /// The person typed into the program. Raised on the window's thread.
    public event Action? Typed;

    /// The program drew its first letter: until then a loader stands in place of the terminal
    /// (D-368). Raised on the window's thread.
    public event Action? DrawnFirst;

    public bool Drawn => _drawn;

    /// The blocks of a command line, null for Claude Code.
    internal ShellBlocksView? Blocks => _blocks;

    internal ShellFindBar? FindForCheck => _find;

    /// The search in the output closed: the editor takes the keys again.
    public event Action? FindClosed;

    /// Ctrl+Shift+F: the search in the output of a command line. False for Claude Code, whose
    /// screen is its own.
    public bool OpenFind()
    {
        if (_find is null || _shell is null || _blocks!.Visibility != Visibility.Visible)
        {
            return false;
        }

        _find.Open();
        return true;
    }

    internal bool TerminalVisibleForCheck => _control.Visibility == Visibility.Visible;

    public bool IsRunning => _console.IsRunning;

    /// Text as if it was typed: keys of the editor that the terminal should get.
    public void Type(string text) => _console.WriteInput(text);

    public void FocusTerminal() => _control.Focus();

    /// Back on screen after the settings: a hidden control does not count its rows again when the
    /// window changes size, so the pseudo console is told the size it has now.
    public void Refit() => ResizeNow();

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
        _control.SetTheme(Theme, Font, _points, Color.FromRgb(0x17, 0x17, 0x17));
        _blocks?.UseFont(points, scale);
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

    /// The picture that stands in for the control while the window moves it, or null.
    private Image? _still;
    private bool _focusedBeforeStill;

    /// The terminal becomes a picture of itself. The control is a window of its own: moved on every
    /// frame of a motion, it lags behind the drawing of WPF and jerks. A picture moves with WPF. The
    /// control stays in the layout at its size, so the pseudo console gets no resize. The control
    /// hides only after the picture is drawn: hidden at once, it left one empty frame and the
    /// terminal blinked.
    public void ShowStill()
    {
        // A new motion while the terminal waits for its redraw: the picture stays for this one too.
        if (_waiting is not null)
        {
            _waiting.Stop();
            _waiting = null;
            return;
        }

        // The real terminal is back and the old picture waits to go: it goes now, a new one is taken.
        if (_still is not null && _control.Visibility == Visibility.Visible)
        {
            Children.Remove(_still);
            _still = null;
        }

        if (_still is not null || !IsVisible || _control.ActualWidth <= 0 || _control.ActualHeight <= 0)
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

        var still = new Image
        {
            Source = picture,
            Stretch = Stretch.Fill,
            Width = area.Width / dpi.DpiScaleX,
            Height = area.Height / dpi.DpiScaleY,
            // Held at the bottom of the terminal, where Claude Code keeps its input line. The area
            // grows when the header folds: held at its top, the picture jumped up by the height of
            // the header. It shrinks when the header opens: held at its top, the picture lost the
            // input line. Held at the bottom, it stands still and loses or gains only its oldest
            // rows, as the program does when it redraws for the new number of rows.
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(_control.Margin.Left, 0, 0, 0),
            IsHitTestVisible = false,
        };
        RenderOptions.SetBitmapScalingMode(still, BitmapScalingMode.NearestNeighbor);
        _still = still;
        _stillArea = new Size(_control.ActualWidth, _control.ActualHeight);
        _stillAt = Clock.ElapsedMilliseconds;
        _focusedBeforeStill = IsKeyboardFocusWithin;
        Children.Add(still);
        AfterFrames(2, () =>
        {
            if (ReferenceEquals(_still, still))
            {
                _control.Visibility = Visibility.Hidden;
            }
        });
    }

    /// The real terminal again, in its new place. The picture goes two frames later, once the
    /// control has drawn itself: taken away at once, it left the terminal blank for a frame.
    public void ShowLive()
    {
        if (_still is null || _waiting is not null)
        {
            return;
        }

        // The area changed since the picture: the program gets the new size now and redraws for it
        // behind the picture. Shown before that, the terminal showed the old screen in the new size
        // and the program redrew it in front of the person: the jerk at the end of opening. A hidden
        // control does not count its rows again (the trace: 732 units high, still 47 rows), so the
        // surface asks it to.
        ResizeNow();

        // A smaller area cuts the picture at its top, and the cut part must not cover the header.
        if (_control.ActualHeight < _stillArea.Height)
        {
            ClipToBounds = true;
        }
        if (_control.ActualWidth == _stillArea.Width && _control.ActualHeight == _stillArea.Height)
        {
            Reveal();
            return;
        }

        var started = Clock.ElapsedMilliseconds;
        _waiting = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(10) };
        _waiting.Tick += (_, _) =>
        {
            var now = Clock.ElapsedMilliseconds;
            var resized = _sentAt > _stillAt;
            var quiet = now - Interlocked.Read(ref _outputAt);
            var redrawn = resized && (_outputAfterResize ? quiet >= RedrawQuiet : now - _sentAt >= NoRedraw);
            if (redrawn || now - started >= MostWait)
            {
                _waiting.Stop();
                _waiting = null;
                Reveal();
            }
        };
        _waiting.Start();
    }

    /// The wait for the redraw after a resize: quiet for this long after output, this long without
    /// any output, and never longer than the last.
    private const long RedrawQuiet = 30;
    private const long NoRedraw = 80;
    private const long MostWait = 250;

    private DispatcherTimer? _waiting;

    /// The size of the area when the picture was taken, and the times in milliseconds of one clock:
    /// the picture, the last size sent, and the last output.
    private Size _stillArea;
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private long _stillAt;
    private long _sentAt = -1;
    private long _outputAt;
    private volatile bool _outputAfterResize;

    /// The real terminal comes over the picture, and the picture goes two frames later.
    private void Reveal()
    {
        if (_still is not { } still)
        {
            return;
        }

        _control.Visibility = Visibility.Visible;

        // Shown again, the control may count its rows without a change of its size, and then no
        // event tells the pseudo console: Claude Code stayed with 47 rows in an area of 36.
        AfterFrames(3, ResizeNow);
        if (_focusedBeforeStill)
        {
            FocusTerminal();
        }

        AfterFrames(2, () =>
        {
            if (ReferenceEquals(_still, still) && _control.Visibility == Visibility.Visible)
            {
                Children.Remove(still);
                _still = null;
                ClipToBounds = false;
            }
        });
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
    /// The control counts its rows and columns for its size now, and the pseudo console gets them.
    private void ResizeNow()
    {
        if (_control.ActualWidth <= 0 || _control.ActualHeight <= 0)
        {
            return;
        }

        var (rows, columns) = _control.TriggerResize(new Size(_control.ActualWidth, _control.ActualHeight));
        Send(rows, columns);
    }

    private void SyncSize() => Send(_control.Rows, _control.Columns);

    /// On the thread of the output: the screen takes the piece, and one redraw is asked for however
    /// many pieces come before the window gets to it.
    private void FeedShell(string data)
    {
        lock (_shellGate)
        {
            _shell!.Feed(data, Clock.ElapsedMilliseconds);
        }

        if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                _blocks!.Refresh();
            }, DispatcherPriority.Render);
        }
    }

    private void ShowFullScreen(bool on)
    {
        _control.Visibility = on ? Visibility.Visible : Visibility.Hidden;
        _blocks!.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (on)
        {
            // A hidden control did not count its rows again; the program needs the real size.
            ResizeNow();
            _control.Focus();
        }

        FullScreenChanged?.Invoke(on);
    }

    private void Send(int rows, int columns)
    {
        var size = (Rows: rows, Columns: columns);
        if (size.Rows > 0 && size.Columns > 0 && size != _sent)
        {
            _sent = size;
            _sentAt = Clock.ElapsedMilliseconds;
            _outputAfterResize = false;
            _console.Resize((uint)size.Rows, (uint)size.Columns);
            if (_shell is not null)
            {
                lock (_shellGate)
                {
                    _shell.Resize(size.Columns, size.Rows);
                }
            }
        }
    }

    /// One scheme of sixteen colours from the palette of slayumind (D-295). The palette has no
    /// grey between muted and raised that text can be read in, so bright black is the one colour
    /// added: halfway between them.
    private static readonly TerminalTheme Theme = new()
    {
        DefaultBackground = Rgb(0x171717),
        DefaultForeground = Rgb(0xFAFAFA),
        DefaultSelectionBackground = Rgb(0x404040),
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable =
        [
            Rgb(0x171717), Rgb(0xFF6467), Rgb(0x00BC7D), Rgb(0xFE9A00), Rgb(0x11A9F6), Rgb(0xD477CD), Rgb(0x00D3F2), Rgb(0xA1A1A1),
            Rgb(0x737373), Rgb(0xFF8270), Rgb(0xAFE773), Rgb(0xFE9A00), Rgb(0x11A9F6), Rgb(0xF7BCF0), Rgb(0x24DCC3), Rgb(0xFAFAFA),
        ],
    };

    /// 0xRRGGBB to the COLORREF of Windows, 0x00BBGGRR.
    private static uint Rgb(uint hex) => ((hex & 0xFF) << 16) | (hex & 0xFF00) | ((hex >> 16) & 0xFF);
}
