using System.Diagnostics;
using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The keys of the window: the core's table says what a key does (TerminalKeys), this part does it.
sealed partial class TerminalHost
{
    /// Ctrl and the key, with Shift or without (D-353). What a key does is the core's table
    /// (TerminalKeys); here it is done. False leaves the key to the program.
    private bool KeyPressed(System.Windows.Input.Key key, bool shift, bool inTerminal)
    {
        // Taking the selection clears it in the control, so it is taken once, only for C, and the
        // same text is copied. Taken for every key, Ctrl alone wiped it before C came.
        var name = KeyName(key);
        var selected = name == "C" && (inTerminal || shift) && !SettingsOpen ? OpenTerminal()?.TakeSelectedText() : null;
        var state = new TerminalKeyState(
            SettingsOpen,
            _model.Session(_sessions)?.Kind == SessionKind.Claude,
            inTerminal,
            inTerminal && selected is not null);

        switch (TerminalKeys.For(name, shift, state))
        {
            case TerminalKeyAction.ToggleSettings:
                if (SettingsOpen)
                {
                    CloseSettings();
                }
                else
                {
                    OpenSettings();
                }

                return true;
            case TerminalKeyAction.Compose:
                _panel.Editor.Compose();
                return true;
            case TerminalKeyAction.Paste:
                if (OpenTerminal() is { } pasteInto && _model.Session(_sessions) is { } pasted)
                {
                    _ = WriteInTurn(pasteInto, TerminalPaste.FromClipboard(ThisComputer.Folders.PicturesFolder(pasted.Id)));
                }

                return true;
            case TerminalKeyAction.Copy:
                if (selected is not null)
                {
                    CopyText(selected);
                }

                return true;
            case TerminalKeyAction.ZoomIn:
                Zoom(WindowZoom.Larger);
                return true;
            case TerminalKeyAction.ZoomOut:
                Zoom(WindowZoom.Smaller);
                return true;
            case TerminalKeyAction.ZoomReset:
                Zoom(_ => WindowZoom.Default);
                return true;
            case TerminalKeyAction.NewSessionQuickly:
                PickFolderQuickly();
                return true;
            case TerminalKeyAction.Palette:
                OpenSessionPalette();
                return true;
            case TerminalKeyAction.Find:
                return OpenTerminal()?.OpenFind() == true;
            case TerminalKeyAction.ToggleList:
                if (_panel.ToggleListFromKeys())
                {
                    Remember(settings => settings with { SessionListHidden = !settings.SessionListHidden });
                }

                return true;
            case TerminalKeyAction.NextSession:
                GoRound(1);
                return true;
            case TerminalKeyAction.PreviousSession:
                GoRound(-1);
                return true;
            case TerminalKeyAction.Voice:
                _panel.Editor.PressVoice();
                return true;
            default:
                return false;
        }
    }

    /// The name of a WPF key in the table of the core.
    private static string KeyName(System.Windows.Input.Key key) => key switch
    {
        System.Windows.Input.Key.OemComma => "Comma",
        System.Windows.Input.Key.OemPlus or System.Windows.Input.Key.Add => "Plus",
        System.Windows.Input.Key.OemMinus or System.Windows.Input.Key.Subtract => "Minus",
        System.Windows.Input.Key.D0 or System.Windows.Input.Key.NumPad0 => "0",
        _ => key.ToString(),
    };

    private void GoRound(int step)
    {
        var next = _model.ToNeighbour(step, _sessions);
        if (next.SessionId is not { } id || next == _model)
        {
            return;
        }

        _sessions = _sessions.Opened(id);
        Save();
        Change(next);
        _window.Dispatcher.BeginInvoke(FocusOpenTerminal, System.Windows.Threading.DispatcherPriority.Input);
    }
}
