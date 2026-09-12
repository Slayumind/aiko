using System.Globalization;
using Aiko.Core;

namespace Aiko.App;

/// Which language Aiko speaks.
///
/// The setting existed from the first version, was saved, was shown in three places, and changed
/// nothing at all: every word was written into the code. This is where it starts meaning something.
///
/// Windows is followed unless the user said otherwise, and "otherwise" is only ever English or
/// Russian, because those are the two Aiko has words for.
static class LanguageChoice
{
    public static void Apply(AikoLanguage language)
    {
        var culture = language switch
        {
            AikoLanguage.English => new CultureInfo("en"),
            AikoLanguage.Russian => new CultureInfo("ru"),
            _ => CultureInfo.CurrentUICulture,
        };

        Strings.Culture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Dates and numbers stay with Windows on purpose. Somebody reading Aiko in English on a
        // Russian computer still wants their own clock, and the card is full of times.
    }
}
