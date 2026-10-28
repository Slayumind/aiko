using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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
    private bool _connected;

    public TerminalSurface(PtyLaunch launch)
    {
        _console = new PseudoConsole(launch);
        _console.Exited += code => Dispatcher.BeginInvoke(() => Exited?.Invoke(code));
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
        };
        _control.SizeChanged += (_, _) => Dispatcher.BeginInvoke(SyncSize, DispatcherPriority.Background);
    }

    /// The program ended, with its exit code. Raised on the window's thread.
    public event Action<int>? Exited;

    public bool IsRunning => _console.IsRunning;

    /// Text as if it was typed: keys of the editor that the terminal should get.
    public void Type(string text) => _console.WriteInput(text);

    public void FocusTerminal() => _control.Focus();

    /// Ends the program and everything it started.
    public void Close() => _console.Dispose();

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
