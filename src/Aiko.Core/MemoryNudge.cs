using System.Text.RegularExpressions;

namespace Aiko.Core;

/// A short reminder added to a turn when the person talks about themselves.
///
/// Without it the model starts the task and forgets to save what the message said about the
/// person. The check is a list of first person words and nothing smarter: a false alarm costs one
/// short note the model is told to ignore, and a miss costs a fact.
public static partial class MemoryNudge
{
    public const string Text =
        "Aiko memory check: this message may tell something about the user (life, how they like "
        + "to work, a project they work on, taste). If it does and the memory does not have it yet, "
        + "call `remember` before you start the task. A project in this repository is also the "
        + "user's project. If there is nothing like that, ignore this note and do not mention it.";

    /// True when the message speaks in the first person: «я хочу», «у меня», "my", "I like".
    public static bool Applies(string? prompt) => !string.IsNullOrEmpty(prompt) && SelfTalk().IsMatch(prompt);

    /// A hyphen counts as part of a word here, so «по-моему» and "i-node" are not first person.
    [GeneratedRegex(
        @"(?<![\w-])(я|мне|меня|мой|моя|моё|мои|мы|нам|нас|наш|наша|наше|наши"
        + @"|i|i'm|i've|my|we|our|me)(?![\w-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelfTalk();
}
