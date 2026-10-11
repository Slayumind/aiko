namespace Aiko.Core;

/// How a change the person made in the settings window ended.
public enum MemoryHandOutcome
{
    Done,

    /// The fact already says this; the file is not written.
    NothingToDo,

    /// A fact needs text.
    Empty,

    /// The fact with its meaning and keys is over the limit.
    TooLong,

    /// The category has no free place, and nothing is dropped to make one.
    Full,

    /// The fact is gone, for example the model forgot it while the window was open.
    NoFact,
}

/// The memory after a change by hand. Changed says whether to write the file.
public sealed record MemoryHandEdit(MemoryFile Memory, MemoryHandOutcome Outcome)
{
    public bool Changed => Outcome == MemoryHandOutcome.Done;
}

/// What the person does to the memory in the settings window: add, edit, pin, remove, erase.
///
/// These are not the tools of the model. A tool call answers the model in words and may drop the
/// oldest fact to make room. A person sees the list, so nothing is dropped behind their back: a
/// full category refuses, and the window says so. The outcome is a value, and the words for it
/// are the window's, in the person's language.
public static class MemoryHand
{
    /// A fact typed by hand has no keys: the person did not name any, and guessing them here
    /// would refresh the fact on words they never chose. It may be in any language.
    public static MemoryHandEdit Add(MemoryFile memory, MemoryCategory category, string? text, string? meaning, DateOnly today)
    {
        var fact = new MemoryFact(MemoryFact.IdFor(memory.FirstFreeNumber), category, MemoryEdits.OneLine(text))
        {
            Meaning = MemoryEdits.OneLine(meaning),
            Seen = today,
            Created = today,
        };

        if (Problem(fact) is { } problem)
        {
            return new MemoryHandEdit(memory, problem);
        }

        return memory.In(category).Count() >= MemoryRules.Quota(category)
            ? new MemoryHandEdit(memory, MemoryHandOutcome.Full)
            : Done(memory with { NextId = fact.Number + 1, Facts = [.. memory.Facts, fact] });
    }

    /// Unlike the tool, an empty meaning here means "no meaning": the person cleared the field.
    /// The day the fact was last seen does not move: editing a fact is not living by it.
    public static MemoryHandEdit Edit(MemoryFile memory, string? id, string? text, string? meaning)
    {
        if (memory.Find(id) is not { } existing)
        {
            return new MemoryHandEdit(memory, MemoryHandOutcome.NoFact);
        }

        var edited = existing with { Text = MemoryEdits.OneLine(text), Meaning = MemoryEdits.OneLine(meaning) };
        if (Problem(edited) is { } problem)
        {
            return new MemoryHandEdit(memory, problem);
        }

        return edited == existing ? new MemoryHandEdit(memory, MemoryHandOutcome.NothingToDo) : Done(Replace(memory, edited));
    }

    /// A pinned fact never ages, so it needs no keys and loses the ones it had, as with the tool.
    /// Taking the pin off does not bring them back.
    public static MemoryHandEdit Pin(MemoryFile memory, string? id, bool pinned)
    {
        if (memory.Find(id) is not { } existing)
        {
            return new MemoryHandEdit(memory, MemoryHandOutcome.NoFact);
        }

        if (existing.Pinned == pinned)
        {
            return new MemoryHandEdit(memory, MemoryHandOutcome.NothingToDo);
        }

        var keys = MemoryRules.TakesKeys(existing.Category, pinned) ? existing.Keys : [];
        return Done(Replace(memory, existing with { Pinned = pinned, Keys = keys }));
    }

    public static MemoryHandEdit Remove(MemoryFile memory, string? id) =>
        memory.Find(id) is { } existing
            ? Done(memory with { Facts = memory.Facts.Where(f => f.Id != existing.Id).ToList() })
            : new MemoryHandEdit(memory, MemoryHandOutcome.NoFact);

    /// Every fact and every record kept aside. The numbers go on from where they were: a session
    /// that is open may still hold the old ids, and a new fact must not answer to one of them.
    public static MemoryHandEdit EraseAll(MemoryFile memory) =>
        memory.Facts.Count == 0 && memory.SetAside.Count == 0
            ? new MemoryHandEdit(memory, MemoryHandOutcome.NothingToDo)
            : Done(new MemoryFile(memory.FirstFreeNumber, [], []));

    private static MemoryHandOutcome? Problem(MemoryFact fact) =>
        fact.Text.Length == 0 ? MemoryHandOutcome.Empty
        : !MemoryRules.Fits(MemoryRules.LengthOf(fact)) ? MemoryHandOutcome.TooLong
        : null;

    private static MemoryFile Replace(MemoryFile memory, MemoryFact fact) =>
        memory with { Facts = memory.Facts.Select(f => f.Id == fact.Id ? fact : f).ToList() };

    private static MemoryHandEdit Done(MemoryFile memory) => new(memory, MemoryHandOutcome.Done);
}
