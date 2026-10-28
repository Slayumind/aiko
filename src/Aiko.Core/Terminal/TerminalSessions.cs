using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aiko.Core.Terminal;

/// One session of the terminal as Aiko remembers it between runs (D-293): which conversation of
/// Claude Code it is and where it runs. Not a word of the conversation is here.
public sealed record TerminalSession(string Id, string Environment, string Folder, string Name)
{
    /// Null leaves the choice of the model to Claude Code.
    public string? Model { get; init; }

    public PermissionMode Mode { get; init; } = PermissionMode.Manual;

    /// Claude Code has the conversation on disk, so the next start resumes it.
    public bool Started { get; init; }

    /// A closed session is out of the list. Its id is kept, so the conversation can be found again.
    public bool Closed { get; init; }

    /// When something last happened in it, in seconds since 1970. Zero when nothing did.
    public long LastUsed { get; init; }
}

/// The sessions of one folder, as one group of the list.
public sealed record SessionGroup(string Folder, string Name, IReadOnlyList<TerminalSession> Sessions);

/// The sessions of the terminal, in the order the person put them, and the one that was open last.
///
/// This is the file terminal.json. The list changes only through the methods here; each of them
/// gives a new list.
public sealed record TerminalSessions(IReadOnlyList<TerminalSession> Sessions, string? LastId)
{
    public static readonly TerminalSessions Empty = new([], null);

    /// Where the window stood when it was closed. Null until it was closed once.
    public WindowPlace? Window { get; init; }

    // ---- reading the list ----

    public TerminalSession? Find(string id) => Sessions.FirstOrDefault(session => session.Id == id);

    /// The sessions of an environment that are in the list, in its order.
    public IReadOnlyList<TerminalSession> Open(string environment) =>
        [.. Sessions.Where(session => !session.Closed && session.Environment == environment)];

    /// The list of an environment: a group for each folder that has a session in the list. The
    /// groups stand in the order their folders first appear in the file, closed sessions counted,
    /// so a group does not jump when its first session is closed.
    public IReadOnlyList<SessionGroup> Groups(string environment)
    {
        var folders = new List<string>();
        foreach (var session in Sessions.Where(session => session.Environment == environment))
        {
            if (!folders.Contains(session.Folder))
            {
                folders.Add(session.Folder);
            }
        }

        var open = Open(environment);
        return [.. folders
            .Select(folder => new SessionGroup(
                folder,
                SessionName.FolderName(folder),
                [.. open.Where(session => session.Folder == folder)]))
            .Where(group => group.Sessions.Count > 0)];
    }

    /// The session the window opens on: the last one, or the first of the environment.
    public TerminalSession? ToOpen(string environment) =>
        Open(environment).FirstOrDefault(session => session.Id == LastId) ?? Open(environment).FirstOrDefault();

    // ---- changing the list ----

    /// A new session at the end of the list. An empty name becomes the folder with a number. An
    /// id that is already taken, or one without an environment or a folder, changes nothing.
    public TerminalSessions Add(string id, string environment, string folder, string name, string? model, PermissionMode mode, long now)
    {
        var place = SessionName.FolderPath(folder);
        if (id.Length == 0 || environment.Length == 0 || place.Length == 0 || Find(id) is not null)
        {
            return this;
        }

        var given = SessionName.Clean(name);
        var session = new TerminalSession(
            id,
            environment,
            place,
            given.Length > 0 ? given : SessionName.Default(place, NamesIn(environment, place)))
        {
            Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            Mode = mode,
            LastUsed = now,
        };

        return this with { Sessions = [.. Sessions, session], LastId = id };
    }

    /// The names the open sessions of a folder already have.
    public IReadOnlyList<string> NamesIn(string environment, string folder) =>
        [.. Open(environment).Where(session => session.Folder == SessionName.FolderPath(folder)).Select(session => session.Name)];

    /// An empty name changes nothing: a session is never left without one.
    public TerminalSessions Rename(string id, string name) =>
        SessionName.Clean(name) is { Length: > 0 } clean ? With(id, session => session with { Name = clean }) : this;

    public TerminalSessions SetMode(string id, PermissionMode mode) => With(id, session => session with { Mode = mode });

    public TerminalSessions SetModel(string id, string? model) =>
        With(id, session => session with { Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim() });

    /// Claude Code has the conversation now.
    public TerminalSessions MarkStarted(string id) => With(id, session => session with { Started = true });

    public TerminalSessions Used(string id, long now) => With(id, session => session with { LastUsed = now });

    /// A renamed environment takes its sessions with it: they are known by its name.
    public TerminalSessions RenameEnvironment(string from, string to) =>
        from == to ? this : this with
        {
            Sessions = [.. Sessions.Select(session => session.Environment == from ? session with { Environment = to } : session)],
        };

    /// The sessions after the settings changed under an open window. An environment keeps its
    /// folder of Claude Code when it is renamed, so the folder tells which old name is which new one.
    public TerminalSessions Follow(EnvironmentSettings before, EnvironmentSettings after)
    {
        var next = this;
        foreach (var old in before.Environments)
        {
            var renamed = after.Environments.FirstOrDefault(environment =>
                environment.Name != old.Name
                && environment.ConfigDirectories.SequenceEqual(old.ConfigDirectories, StringComparer.OrdinalIgnoreCase));
            if (renamed is not null && after.Environments.All(environment => environment.Name != old.Name))
            {
                next = next.RenameEnvironment(old.Name, renamed.Name);
            }
        }

        return next;
    }

    /// The person went into this session.
    public TerminalSessions Opened(string id) =>
        Find(id) is { Closed: false } ? this with { LastId = id } : this;

    /// Takes the session out of the list. If it was the one the window was on, the window goes to
    /// its neighbour in the same environment: the next one, or the one before.
    public TerminalSessions Close(string id)
    {
        if (Find(id) is not { Closed: false } closing)
        {
            return this;
        }

        var around = Open(closing.Environment);
        var at = around.ToList().FindIndex(session => session.Id == id);
        var neighbour = at + 1 < around.Count ? around[at + 1] : at > 0 ? around[at - 1] : null;

        var next = With(id, session => session with { Closed = true });
        return LastId == id ? next with { LastId = neighbour?.Id } : next;
    }

    /// The window was closed at this place. It is kept in whole units; a place that is not a
    /// place at all changes nothing.
    public TerminalSessions Placed(WindowPlace place) =>
        Whole(place) is { } whole ? this with { Window = whole } : this;

    private TerminalSessions With(string id, Func<TerminalSession, TerminalSession> change) =>
        this with { Sessions = [.. Sessions.Select(session => session.Id == id ? change(session) : session)] };

    // ---- the file ----

    /// Never throws. A broken file is an empty list; a session without an id, an environment or
    /// a folder is left out, and so is a second session with an id that was already read.
    public static TerminalSessions FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            var sessions = new List<TerminalSession>();
            if (root.TryGetProperty("sessions", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    if (SessionIn(item) is { } session && sessions.All(read => read.Id != session.Id))
                    {
                        sessions.Add(session);
                    }
                }
            }

            var last = Word(root, "last");
            return new TerminalSessions(sessions, sessions.Any(session => session.Id == last && !session.Closed) ? last : null)
            {
                Window = root.TryGetProperty("window", out var window) ? PlaceIn(window) : null,
            };
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("sessions");
            foreach (var session in Sessions)
            {
                writer.WriteStartObject();
                writer.WriteString("id", session.Id);
                writer.WriteString("environment", session.Environment);
                writer.WriteString("folder", session.Folder);
                writer.WriteString("name", session.Name);
                if (session.Model is { } model)
                {
                    writer.WriteString("model", model);
                }
                writer.WriteString("mode", PermissionModes.Name(session.Mode));
                writer.WriteBoolean("started", session.Started);
                writer.WriteBoolean("closed", session.Closed);
                writer.WriteNumber("lastUsed", session.LastUsed);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (LastId is { } last)
            {
                writer.WriteString("last", last);
            }
            if (Window is { } window)
            {
                writer.WriteStartObject("window");
                writer.WriteNumber("left", (long)window.Left);
                writer.WriteNumber("top", (long)window.Top);
                writer.WriteNumber("width", (long)window.Width);
                writer.WriteNumber("height", (long)window.Height);
                writer.WriteBoolean("maximized", window.Maximized);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length) + "\n";
    }

    private static TerminalSession? SessionIn(JsonElement item)
    {
        var id = Word(item, "id");
        var environment = Word(item, "environment");
        var folder = SessionName.FolderPath(Word(item, "folder"));
        if (id.Length == 0 || environment.Length == 0 || folder.Length == 0)
        {
            return null;
        }

        var name = SessionName.Clean(Word(item, "name"));
        return new TerminalSession(id, environment, folder, name.Length > 0 ? name : SessionName.FolderName(folder))
        {
            Model = Word(item, "model") is { Length: > 0 } model ? model : null,
            Mode = PermissionModes.FromStream(Word(item, "mode")) ?? PermissionMode.Manual,
            Started = Flag(item, "started"),
            Closed = Flag(item, "closed"),
            LastUsed = item.TryGetProperty("lastUsed", out var used) && used.ValueKind == JsonValueKind.Number && used.TryGetInt64(out var seconds)
                ? seconds
                : 0,
        };
    }

    /// A place in the file needs all four numbers; a width or a height of zero or less is no place.
    private static WindowPlace? PlaceIn(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? Number(string key) =>
            node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
                ? number
                : null;

        return Number("left") is { } left && Number("top") is { } top && Number("width") is { } width && Number("height") is { } height
            ? Whole(new WindowPlace(left, top, width, height, Flag(node, "maximized")))
            : null;
    }

    private static WindowPlace? Whole(WindowPlace place)
    {
        double[] numbers = [place.Left, place.Top, place.Width, place.Height];
        if (!numbers.All(double.IsFinite) || place.Width <= 0 || place.Height <= 0)
        {
            return null;
        }

        return new WindowPlace(Round(place.Left), Round(place.Top), Round(place.Width), Round(place.Height), place.Maximized);
    }

    /// Half goes away from zero, as in Swift.
    private static double Round(double number) => Math.Round(number, MidpointRounding.AwayFromZero);

    private static string Word(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool Flag(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
}
