using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
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
    private const short FontSize = 13;

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
        _control.Margin = new Thickness(TextInset, TextInset - 4, 0, 0);
        Children.Add(_control);

        Loaded += (_, _) =>
        {
            _control.SetTheme(Theme, Font, FontSize, Color.FromRgb(0x0A, 0x0A, 0x0A));
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

    /// What the mouse selected in the terminal, or null.
    public string? SelectedText
    {
        get
        {
            var text = _control.GetSelectedText();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }

    /// Ends the program and everything it started.
    public void Close() => _console.Dispose();

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

    private void SyncSize()
    {
        if (_control.Rows > 0 && _control.Columns > 0)
        {
            _console.Resize((uint)_control.Rows, (uint)_control.Columns);
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
