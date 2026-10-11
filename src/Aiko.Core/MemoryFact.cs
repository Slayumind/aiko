using System.Globalization;

namespace Aiko.Core;

/// The four shelves of the memory. Each has its own quota (MemoryRules), so a busy month of
/// projects cannot push out who the person is.
public enum MemoryCategory
{
    You,
    Work,
    Projects,
    Interests,
}

/// One thing Aiko remembers about the person.
///
/// Seen is the day the fact last came up, and it decides which fact goes first when a category is
/// full. Keys are the names the person would say when it comes up again: a key in their message
/// moves Seen by itself (MemoryEdits.TouchByPrompt).
public sealed record MemoryFact(string Id, MemoryCategory Category, string Text)
{
    public const string IdPrefix = "F";

    /// What the fact changes in Aiko's answers. Empty when the text says it all.
    public string Meaning { get; init; } = "";

    /// A pinned fact is never forgotten to make room.
    public bool Pinned { get; init; }

    public IReadOnlyList<string> Keys { get; init; } = [];

    public DateOnly Seen { get; init; }

    public DateOnly Created { get; init; }

    /// 12 for F12. Ids are compared as numbers: as text, F10 would come before F9.
    public int Number => NumberOf(Id) ?? 0;

    public static string IdFor(int number) => IdPrefix + number.ToString(CultureInfo.InvariantCulture);

    /// Null for anything that is not F and a number from 1 up, written the one way IdFor writes it:
    /// F07 is not an id, or it would be a second fact 7.
    public static int? NumberOf(string? id) =>
        id is not null
        && id.StartsWith(IdPrefix, StringComparison.Ordinal)
        && int.TryParse(id.AsSpan(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
        && number > 0
        && IdFor(number) == id
            ? number
            : null;
}
