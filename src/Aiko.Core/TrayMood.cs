namespace Aiko.Core;

/// Aiko's faces, the same seven in every style (D-199, D-215).
public enum AikoFace
{
    Fresh,
    Tired,
    Asleep,
    Working,
    Waiting,
    Done,
    Error,
}

/// A face on the icon and when it came.
public sealed record FaceMoment(AikoFace Face, DateTimeOffset Since);

/// Which face an event brings and how long it stays (D-199, D-211). The face is a moment, not a
/// state: it replaces the rings for two seconds after an event and goes. Nothing is shown and no
/// timer runs while nothing happens.
public static class TrayMood
{
    public static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(2);

    public const int TiredAt = 90;
    public const int AsleepAt = 100;

    /// A file seen for the first time with an older event in it is history, for example a session
    /// file read when Aiko starts. History brings no face.
    public static readonly TimeSpan NewEventWithin = TimeSpan.FromSeconds(30);

    /// The face for a change in one session's file. Null when the activity did not change: a new
    /// tool call in a working session is not a new moment.
    public static AikoFace? ForActivity(ActivityRecord? before, ActivityRecord after, DateTimeOffset now)
    {
        if (before?.Activity == after.Activity || now - after.At > NewEventWithin)
        {
            return null;
        }

        return after.Activity switch
        {
            SessionActivity.Working => AikoFace.Working,
            SessionActivity.Waiting => AikoFace.Waiting,
            SessionActivity.Done => AikoFace.Done,
            SessionActivity.Error => AikoFace.Error,
            SessionActivity.OutOfLimit => AikoFace.Asleep,
            _ => null,
        };
    }

    /// The face for a limit crossing a line. Null for the first number Aiko sees: that is where the
    /// limit already was, not a change.
    public static AikoFace? ForLimit(int? before, int? after)
    {
        if (before is not { } was || after is not { } now)
        {
            return null;
        }

        if (was < AsleepAt && now >= AsleepAt)
        {
            return AikoFace.Asleep;
        }

        if (was < TiredAt && now >= TiredAt)
        {
            return AikoFace.Tired;
        }

        // The window turned over: rested again.
        return was >= TiredAt && now < TiredAt ? AikoFace.Fresh : null;
    }

    /// The fullest window of an environment, with a window past its reset counted as empty.
    public static int? HighestPercent(LimitSnapshot snapshot, DateTimeOffset now) =>
        snapshot.HasData
            ? snapshot.Windows.Max(window => snapshot.StatusAt(now, window.Kind)?.Percent ?? 0)
            : null;

    /// What is on the icon after an event. A face that asks for the person, or says something went
    /// wrong, is not pushed away by a calmer one that comes a moment later.
    public static FaceMoment Next(FaceMoment? showing, AikoFace incoming, DateTimeOffset now) =>
        showing is null || IsOver(showing, now) || Weight(incoming) >= Weight(showing.Face)
            ? new FaceMoment(incoming, now)
            : showing;

    public static bool IsOver(FaceMoment moment, DateTimeOffset now) => now - moment.Since >= ShowFor;

    /// Faces come only from environments where the persona is on (D-199).
    public static bool HasFace(EnvironmentSettings environments) => environments.Environments.Any(e => e.Persona);

    private static int Weight(AikoFace face) => face switch
    {
        AikoFace.Waiting => 6,
        AikoFace.Error => 5,
        AikoFace.Asleep => 4,
        AikoFace.Tired => 3,
        AikoFace.Done => 2,
        AikoFace.Working => 1,
        _ => 0,
    };
}
