namespace Aiko.Core;

/// Which form of a word goes with a number. Russian has three (1 строка, 2 строки, 5 строк); English
/// has two and takes Few as Many.
public enum CountForm
{
    One,
    Few,
    Many,
}

public static class CountForms
{
    /// The Russian rule: the last digit decides, except for the teens, which always take Many.
    public static CountForm Of(long count)
    {
        var n = Math.Abs(count);
        var lastTwo = n % 100;
        var last = n % 10;
        if (lastTwo is >= 11 and <= 14)
        {
            return CountForm.Many;
        }

        return last switch
        {
            1 => CountForm.One,
            2 or 3 or 4 => CountForm.Few,
            _ => CountForm.Many,
        };
    }
}
