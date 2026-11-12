namespace Aiko.Core.Terminal;

/// When a change of a session plays the sound of "waits" (D-375, D-409). Notices are counts now
/// (SessionCounts): only a session that starts to wait has a sound. Rows: spec/cases/notices.
public static class Notices
{
    /// before is null for a terminal seen for the first time. sound: the setting of the sound.
    /// inFocus: the terminal window is on screen and has the keyboard. open: the change is in the
    /// session open in it, which the person already sees.
    public static bool Sounds(TerminalActivity? before, TerminalActivity now, bool sound, bool inFocus, bool open) =>
        sound && now == TerminalActivity.Waiting && before != TerminalActivity.Waiting && !(inFocus && open);
}
