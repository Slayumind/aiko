namespace Aiko.Core.Terminal;

/// What the colour of the terminal window says about the chosen environment (D-435, D-441).
public enum WindowSignal
{
    /// Nothing needs the person and nothing runs: the window has no colour.
    Quiet,
    Working,
    Stopped,
    LimitNearEnd,
    Waiting,
}

public static class WindowSignals
{
    /// A question has to be answered now, so it goes first. A limit near its end comes next, then a
    /// session that stopped by itself, then work.
    public static WindowSignal Of(IEnumerable<RowState> sessions, LimitTone limit)
    {
        var states = sessions as ICollection<RowState> ?? [.. sessions];
        return states.Contains(RowState.Waiting) ? WindowSignal.Waiting
            : limit == LimitTone.Critical ? WindowSignal.LimitNearEnd
            : states.Contains(RowState.Stopped) ? WindowSignal.Stopped
            : states.Contains(RowState.Working) ? WindowSignal.Working
            : WindowSignal.Quiet;
    }
}
