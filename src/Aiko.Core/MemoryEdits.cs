namespace Aiko.Core;

/// What the model asks for in one call of the remember tool. With an id it changes that fact,
/// without one it adds a new fact.
public sealed record MemoryRequest
{
    public string? Id { get; init; }

    public string? Category { get; init; }

    public string? Text { get; init; }

    public string? Meaning { get; init; }

    public bool? Pin { get; init; }

    public IReadOnlyList<string>? Keys { get; init; }
}

/// The memory after a change and the answer the model reads. Changed is false when the memory is
/// the one that came in, so the caller does not write the file for nothing.
public sealed record MemoryEdit(MemoryFile Memory, string Reply, bool Changed);

/// The memory after a message of the person, and the ids of the facts it refreshed.
public sealed record MemoryTouch(MemoryFile Memory, IReadOnlyList<string> Touched);

/// What the two memory tools and a message of the person do to the memory.
///
/// Nothing here reads a file or a clock: the memory and the day come in, the new memory and the
/// reply go out. The reply is written for the model, in English, and always says what happened
/// to the memory, because the model cannot see the file.
public static class MemoryEdits
{
    public static MemoryEdit Remember(MemoryFile memory, MemoryRequest request, DateOnly today) =>
        string.IsNullOrWhiteSpace(request.Id) ? Add(memory, request, today) : Update(memory, request, today);

    public static MemoryEdit Forget(MemoryFile memory, string? id) =>
        memory.Find(id) is { } fact
            ? new MemoryEdit(Without(memory, fact), $"Forgot {fact.Id}.", Changed: true)
            : Refused(memory, NoFact(id));

    /// A key in the message means the fact came up again, so it is seen today. Facts already seen
    /// today are left alone and not named: the caller saves only when something moved.
    public static MemoryTouch TouchByPrompt(MemoryFile memory, string prompt, DateOnly today)
    {
        var touched = memory.Facts
            .Where(f => f.Seen != today && f.Keys.Any(key => MemoryRules.KeyMatches(key, prompt)))
            .Select(f => f.Id)
            .ToList();
        if (touched.Count == 0)
        {
            return new MemoryTouch(memory, []);
        }

        var facts = memory.Facts.Select(f => touched.Contains(f.Id) ? f with { Seen = today } : f).ToList();
        return new MemoryTouch(memory with { Facts = facts }, touched);
    }

    private static MemoryEdit Add(MemoryFile memory, MemoryRequest request, DateOnly today)
    {
        if (MemoryRules.CategoryOf(request.Category) is not { } category)
        {
            string names = string.Join(", ", MemoryRules.Categories.Select(MemoryRules.NameOf));
            return Refused(memory, $"Unknown category {request.Category}. Use one of: {names}. Nothing was saved.");
        }

        string text = OneLine(request.Text);
        if (text.Length == 0)
        {
            return Refused(memory, "A new fact needs text. Nothing was saved.");
        }

        bool pinned = request.Pin ?? false;
        var keys = MemoryRules.TakesKeys(category, pinned) ? MemoryRules.CleanKeys(request.Keys) : CleanedKeys.None;
        var fact = new MemoryFact(MemoryFact.IdFor(memory.FirstFreeNumber), category, text)
        {
            Meaning = OneLine(request.Meaning),
            Pinned = pinned,
            Keys = keys.Kept,
            Seen = today,
            Created = today,
        };

        if (!MemoryRules.Fits(MemoryRules.LengthOf(fact)))
        {
            return Refused(memory, TooLong(fact, "Nothing was saved."));
        }

        return MakeRoom(memory, category) is { } room
            ? Saved(room.Memory, fact, KeyNote(keys) + room.Note)
            : Refused(memory, category == MemoryCategory.Work ? WorkIsFull(memory) : AllPinned(category));
    }

    private static MemoryEdit Update(MemoryFile memory, MemoryRequest request, DateOnly today)
    {
        if (memory.Find(request.Id) is not { } existing)
        {
            return Refused(memory, NoFact(request.Id));
        }

        bool pinned = request.Pin ?? existing.Pinned;
        bool takesKeys = MemoryRules.TakesKeys(existing.Category, pinned);
        var given = takesKeys && request.Keys is { Count: > 0 } ? MemoryRules.CleanKeys(request.Keys) : null;
        var updated = existing with
        {
            Text = OneLine(request.Text) is { Length: > 0 } text ? text : existing.Text,
            Meaning = OneLine(request.Meaning) is { Length: > 0 } meaning ? meaning : existing.Meaning,
            Pinned = pinned,
            Keys = !takesKeys ? [] : given?.Kept ?? existing.Keys,
            Seen = today,
        };

        if (!MemoryRules.Fits(MemoryRules.LengthOf(updated)))
        {
            return Refused(memory, TooLong(updated, "Nothing changed."));
        }

        var facts = memory.Facts.Select(f => f.Id == existing.Id ? updated : f).ToList();
        string note = given is null ? "" : KeyNote(given);
        return new MemoryEdit(memory with { Facts = facts }, $"Refreshed {existing.Id}.{note}", Changed: true);
    }

    /// The memory with a free place in the category, or null when there is none to make. Work
    /// habits are never dropped by age: an old habit is as true as a new one, so the model picks.
    private static (MemoryFile Memory, string Note)? MakeRoom(MemoryFile memory, MemoryCategory category)
    {
        var inCategory = memory.In(category).ToList();
        if (inCategory.Count < MemoryRules.Quota(category))
        {
            return (memory, "");
        }

        if (category == MemoryCategory.Work || MemoryRules.OldestUnpinned(inCategory) is not { } oldest)
        {
            return null;
        }

        return (Without(memory, oldest), $" Memory was full, forgot {oldest.Id}: {oldest.Text}");
    }

    private static MemoryEdit Saved(MemoryFile memory, MemoryFact fact, string notes) =>
        new(
            memory with { NextId = fact.Number + 1, Facts = [.. memory.Facts, fact] },
            $"Saved {fact.Id}.{notes}",
            Changed: true);

    private static MemoryEdit Refused(MemoryFile memory, string reply) => new(memory, reply, Changed: false);

    private static MemoryFile Without(MemoryFile memory, MemoryFact fact) =>
        memory with { Facts = memory.Facts.Where(f => f.Id != fact.Id).ToList() };

    private static string NoFact(string? id) => $"No fact {id?.Trim()}. Nothing changed.";

    private static string TooLong(MemoryFact fact, string outcome) =>
        $"Too long: the fact, its meaning and keys are {MemoryRules.LengthOf(fact)} characters together, "
        + $"and the limit is {MemoryRules.MaxLength}. {outcome} Shorten it to one thought and call again.";

    private static string KeyNote(CleanedKeys keys) =>
        keys.Rejected.Count == 0
            ? ""
            : $" Keys not kept, they are common words: {string.Join(", ", keys.Rejected)}."
              + " Use names or terms, or no keys.";

    private static string WorkIsFull(MemoryFile memory)
    {
        string title = MemoryRules.Title(MemoryCategory.Work);
        var lines = memory.In(MemoryCategory.Work).Select(f => $"[{f.Id}] {f.Text}");

        return $"\"{title}\" is full ({MemoryRules.Quota(MemoryCategory.Work)} facts). Nothing was saved.\n"
            + string.Join("\n", lines)
            + "\nMerge two close facts into one (remember with an id, then forget the other),"
            + " or forget the one that matters least. Then call again.";
    }

    private static string AllPinned(MemoryCategory category) =>
        $"\"{MemoryRules.Title(category)}\" is full and every fact in it is pinned, so none can be forgotten"
        + " to make room. Nothing was saved. Merge two close facts into one (remember with an id, then"
        + " forget the other), then call again.";

    /// A fact is one line of the memory block, so a line break inside it would break the list.
    internal static string OneLine(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
