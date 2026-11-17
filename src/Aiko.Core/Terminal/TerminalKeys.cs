namespace Aiko.Core.Terminal;

/// What Ctrl and a key do in the terminal window (D-353), with Shift or without. None leaves the
/// key to the program in the terminal.
public enum TerminalKeyAction
{
    None,
    ToggleSettings,
    Compose,
    Paste,
    Copy,
    ZoomIn,
    ZoomOut,
    ZoomReset,
    NewSessionQuickly,
    Palette,
    Find,
    ToggleList,
    NextSession,
    PreviousSession,
    Voice,
}

/// What the window knows when a key comes.
public readonly record struct TerminalKeyState(bool SettingsOpen, bool ClaudeSession, bool InTerminal, bool HasSelection);

/// The table of the keys of the terminal window. Keys are named without the platform: a letter,
/// a digit, or Comma, Plus, Minus, Tab, Space. Rows: spec/cases/terminal-keys.
public static class TerminalKeys
{
    public static TerminalKeyAction For(string key, bool shift, TerminalKeyState state)
    {
        // Ctrl+, opens the settings and closes them (D-374), as in VS Code and Warp.
        if (key == "Comma" && !shift)
        {
            return TerminalKeyAction.ToggleSettings;
        }

        // While the settings are open the keys of the sessions wait: the fields of a page take
        // Ctrl+C and Ctrl+V themselves, and nothing changes behind the settings. Zoom still works.
        if (state.SettingsOpen && key is not ("Plus" or "Minus" or "0"))
        {
            return TerminalKeyAction.None;
        }

        return (key, shift) switch
        {
            // A long text for Claude Code in the editor, as the rich input of Warp (D-370).
            ("G", false) when state.ClaudeSession => TerminalKeyAction.Compose,

            // A picture goes as a picture, a file as its path, text as text (D-370).
            ("V", false) when state.InTerminal => TerminalKeyAction.Paste,

            // With a selection Ctrl+C copies, as in Windows Terminal; without one it stops the program.
            ("C", false) when state.InTerminal && state.HasSelection => TerminalKeyAction.Copy,
            ("C", true) => TerminalKeyAction.Copy,

            // Ctrl+= and Ctrl++ make the window larger, as in Windows Terminal (D-384).
            ("Plus", _) => TerminalKeyAction.ZoomIn,
            ("Minus", false) => TerminalKeyAction.ZoomOut,
            ("0", false) => TerminalKeyAction.ZoomReset,

            ("T", true) => TerminalKeyAction.NewSessionQuickly,
            ("K", true) => TerminalKeyAction.Palette,

            // The search in the output of a command line (D-358); Claude Code keeps its own keys.
            ("F", true) => TerminalKeyAction.Find,

            // Ctrl+B belongs to Claude Code (a command to the background) and to bash (D-376).
            ("B", true) => TerminalKeyAction.ToggleList,

            ("Tab", false) => TerminalKeyAction.NextSession,
            ("Tab", true) => TerminalKeyAction.PreviousSession,
            ("Space", true) => TerminalKeyAction.Voice,
            _ => TerminalKeyAction.None,
        };
    }
}
