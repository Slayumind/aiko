namespace Aiko.Core;

/// The letter of an environment beside its ring on the open island (D-421): two rings with only
/// numbers did not say which account is which.
public static class EnvironmentInitial
{
    /// The first letter or digit of the name, in capitals; a name without one gets none.
    public static string Of(string name)
    {
        foreach (var letter in name)
        {
            if (char.IsLetterOrDigit(letter))
            {
                return char.ToUpperInvariant(letter).ToString();
            }
        }

        return "";
    }
}
