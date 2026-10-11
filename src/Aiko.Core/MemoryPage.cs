namespace Aiko.Core;

/// One fact as the settings window shows it. TooLong marks a fact that the model does not get:
/// it can only come from a file edited by hand, and the window asks the person to shorten it.
public sealed record MemoryPageFact(MemoryFact Fact, int Length)
{
    public bool TooLong => !MemoryRules.Fits(Length);
}

/// One category with its count against its quota.
public sealed record MemoryPageSection(MemoryCategory Category, int Quota, IReadOnlyList<MemoryPageFact> Facts)
{
    public int Count => Facts.Count;

    /// No place for a fact added by hand. A file edited by hand may hold more than the quota.
    public bool Full => Count >= Quota;
}

/// What the memory block of the settings window shows: every number and mark on it. The window
/// only draws this, so the decisions can be tested without a window.
public sealed record MemoryPage(IReadOnlyList<MemoryPageSection> Sections, int SetAside)
{
    public int Count => Sections.Sum(s => s.Count);

    public int Capacity => Sections.Sum(s => s.Quota);

    /// Facts the model does not get because they are over the limit.
    public int TooLong => Sections.Sum(s => s.Facts.Count(f => f.TooLong));

    /// Nothing to show and nothing to erase.
    public bool Empty => Count == 0 && SetAside == 0;

    public static MemoryPage From(MemoryFile memory) =>
        new(
            MemoryRules.Categories
                .Select(category => new MemoryPageSection(
                    category,
                    MemoryRules.Quota(category),
                    memory.In(category).Select(f => new MemoryPageFact(f, MemoryRules.LengthOf(f))).ToList()))
                .ToList(),
            memory.SetAside.Count);

    /// The count under an edit field, for text that is not saved yet. Keys count too: they are a
    /// part of the same limit, and the person cannot see them shrink the room.
    public static int LengthOf(string? text, string? meaning, MemoryFact? editing = null) =>
        MemoryRules.LengthOf(MemoryEdits.OneLine(text), MemoryEdits.OneLine(meaning), editing?.Keys ?? []);
}
