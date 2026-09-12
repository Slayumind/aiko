namespace Aiko.Core;

public enum CountdownUnit
{
    LessThanMinute,
    Minutes,
    HoursAndMinutes,
    DaysAndHours,
}

/// Time left until a limit window resets, already split the way the card shows it.
/// The core decides what to show; the words come from the UI, so both languages stay out of here.
public readonly record struct ResetCountdown(bool IsReset, int Days, int Hours, int Minutes, CountdownUnit Unit)
{
    public static readonly ResetCountdown Reset = new(true, 0, 0, 0, CountdownUnit.LessThanMinute);

    public static ResetCountdown Between(DateTimeOffset now, DateTimeOffset resetsAt)
    {
        var left = resetsAt - now;
        if (left <= TimeSpan.Zero)
        {
            return Reset;
        }

        var days = (int)left.TotalDays;
        var hours = left.Hours;
        var minutes = left.Minutes;

        // A window that ends in 3h 34m 50s reads as "3h 34m": we round down, never promise more.
        var unit = days > 0
            ? CountdownUnit.DaysAndHours
            : hours > 0
                ? CountdownUnit.HoursAndMinutes
                : minutes > 0
                    ? CountdownUnit.Minutes
                    : CountdownUnit.LessThanMinute;

        return new ResetCountdown(false, days, hours, minutes, unit);
    }
}
