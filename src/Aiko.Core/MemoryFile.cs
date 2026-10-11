using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aiko.Core;

/// The memory as it lies in memory.json: the facts, the number of the next one, and the entries
/// Aiko could not use.
///
/// The file is the person's own data and they may edit it by hand. So an entry that cannot be
/// used is not dropped: its JSON is kept in SetAside as it was written and goes back into the
/// file on the next save, where the person can still find and repair it.
public sealed record MemoryFile(int NextId, IReadOnlyList<MemoryFact> Facts, IReadOnlyList<string> SetAside)
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public const int CurrentSchema = 1;

    public const string DateFormat = "yyyy-MM-dd";

    public static readonly MemoryFile Empty = new(1, [], []);

    /// The number the next new fact gets. An id is never used twice, even when the file says a
    /// smaller number than the facts in it: the model holds ids from earlier in the session.
    public int FirstFreeNumber =>
        Math.Max(Math.Max(NextId, 1), Facts.Select(f => f.Number).DefaultIfEmpty(0).Max() + 1);

    /// The model types ids by hand, so f12 finds F12.
    public MemoryFact? Find(string? id) =>
        Facts.FirstOrDefault(f => f.Id.Equals(id?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<MemoryFact> In(MemoryCategory category) => Facts.Where(f => f.Category == category);

    /// No file yet is an empty memory. A file that is not the memory at all is an empty memory
    /// too, but it is marked: the next save would put this empty memory over text somebody may
    /// want back, so the caller keeps that file aside first (the same idea as JsonText).
    /// A file from a newer Aiko counts as unreadable for the same reason.
    public static MemoryRead FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MemoryRead(Empty, Unreadable: false);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement) is { } memory
                ? new MemoryRead(memory, Unreadable: false)
                : new MemoryRead(Empty, Unreadable: true);
        }
        catch (JsonException)
        {
            return new MemoryRead(Empty, Unreadable: true);
        }
    }

    /// Written by hand with a writer, not through a serializer: the bridge is published trimmed
    /// and cannot use reflection, and the entries set aside have to go back as they were.
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", CurrentSchema);
            writer.WriteNumber("nextId", FirstFreeNumber);
            writer.WriteStartArray("facts");
            foreach (var fact in Facts)
            {
                Write(writer, fact);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("setAside");
            foreach (string raw in SetAside)
            {
                writer.WriteRawValue(raw);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    private static MemoryFile? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || IsFromNewerAiko(root))
        {
            return null;
        }

        var facts = new List<MemoryFact>();
        var setAside = new List<string>();
        if (!Absent(root, "facts"))
        {
            var entries = root.GetProperty("facts");
            if (entries.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (FactIn(entry) is { } fact && facts.All(f => f.Id != fact.Id))
                {
                    facts.Add(fact);
                }
                else
                {
                    setAside.Add(entry.GetRawText());
                }
            }
        }

        if (root.TryGetProperty("setAside", out var kept) && kept.ValueKind == JsonValueKind.Array)
        {
            setAside.AddRange(kept.EnumerateArray().Select(entry => entry.GetRawText()));
        }

        int nextId = root.TryGetProperty("nextId", out var next)
            && next.ValueKind == JsonValueKind.Number
            && next.TryGetInt32(out int number)
                ? number
                : 1;
        var memory = new MemoryFile(nextId, facts, setAside);
        return memory with { NextId = memory.FirstFreeNumber };
    }

    private static bool IsFromNewerAiko(JsonElement root) =>
        root.TryGetProperty("schema", out var schema)
        && schema.ValueKind == JsonValueKind.Number
        && (!schema.TryGetInt32(out int version) || version > CurrentSchema);

    /// Null when the entry cannot be used. A field that is missing has a default where one makes
    /// sense; a field of the wrong type never does, because guessing would change what was meant.
    private static MemoryFact? FactIn(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || Text(entry, "id") is not { } id
            || MemoryFact.NumberOf(id) is null
            || MemoryRules.CategoryOf(Text(entry, "category")) is not { } category
            || Text(entry, "text") is not { } text
            || string.IsNullOrWhiteSpace(text)
            || Date(entry, "seen") is not { } seen)
        {
            return null;
        }

        string? meaning = Absent(entry, "meaning") ? "" : Text(entry, "meaning");
        bool? pinned = Absent(entry, "pinned") ? false : Flag(entry, "pinned");
        IReadOnlyList<string>? keys = Absent(entry, "keys") ? [] : Texts(entry, "keys");
        DateOnly? created = Absent(entry, "created") ? seen : Date(entry, "created");
        if (meaning is null || pinned is null || keys is null || created is null)
        {
            return null;
        }

        return new MemoryFact(id, category, text)
        {
            Meaning = meaning,
            Pinned = pinned.Value,
            Keys = [.. keys.Take(MemoryRules.MaxKeys)],
            Seen = seen,
            Created = created.Value,
        };
    }

    private static bool Absent(JsonElement entry, string name) =>
        !entry.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? Flag(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateOnly? Date(JsonElement entry, string name) =>
        DateOnly.TryParseExact(
            Text(entry, name), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static IReadOnlyList<string>? Texts(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Array
        && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            ? [.. value.EnumerateArray().Select(item => item.GetString()!)]
            : null;

    private static void Write(Utf8JsonWriter writer, MemoryFact fact)
    {
        writer.WriteStartObject();
        writer.WriteString("id", fact.Id);
        writer.WriteString("category", MemoryRules.NameOf(fact.Category));
        writer.WriteString("text", fact.Text);
        writer.WriteString("meaning", fact.Meaning);
        writer.WriteBoolean("pinned", fact.Pinned);
        writer.WriteStartArray("keys");
        foreach (string key in fact.Keys)
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
        writer.WriteString("seen", fact.Seen.ToString(DateFormat, CultureInfo.InvariantCulture));
        writer.WriteString("created", fact.Created.ToString(DateFormat, CultureInfo.InvariantCulture));
        writer.WriteEndObject();
    }

    /// The relaxed encoder keeps Cyrillic, quotes and + as they are. The file is read by a person
    /// in a text editor and never put into a web page, which is all the strict one guards against.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// What reading memory.json gave. Unreadable means the text was there and was not a memory file.
public sealed record MemoryRead(MemoryFile Memory, bool Unreadable);
