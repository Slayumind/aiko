using System.Text.Json;

namespace Aiko.Core.Terminal;

public enum DiffSide
{
    Removed,
    Added,
}

public sealed record DiffLine(DiffSide Side, string Text);

/// The lines an edit takes out and puts in, for the card that asks to allow it (D-303).
///
/// No line numbers: Claude Code does not send them, and the card is read, not navigated. The
/// equal lines at both ends of an edit are set aside, as in StepLine.Changed. A long edit shows
/// its first lines and says how many more there are.
public sealed record EditDiff(IReadOnlyList<DiffLine> Lines, int More)
{
    public const int MostLines = 40;

    /// The diff of Edit, MultiEdit and Write. Null for any other tool, and for an edit that
    /// changes no line.
    public static EditDiff? For(string toolName, JsonElement input)
    {
        var lines = new List<DiffLine>();
        switch (toolName)
        {
            case "Edit":
                AddChange(lines, Text(input, "old_string"), Text(input, "new_string"));
                break;
            case "MultiEdit":
                if (input.ValueKind == JsonValueKind.Object
                    && input.TryGetProperty("edits", out var edits)
                    && edits.ValueKind == JsonValueKind.Array)
                {
                    foreach (var edit in edits.EnumerateArray())
                    {
                        AddChange(lines, Text(edit, "old_string"), Text(edit, "new_string"));
                    }
                }

                break;
            case "Write":
                lines.AddRange(StepLine.Lines(Text(input, "content")).Select(line => new DiffLine(DiffSide.Added, line)));
                break;
            default:
                return null;
        }

        if (lines.Count == 0)
        {
            return null;
        }

        return lines.Count <= MostLines
            ? new EditDiff(lines, 0)
            : new EditDiff([.. lines.Take(MostLines)], lines.Count - MostLines);
    }

    private static void AddChange(List<DiffLine> lines, string before, string after)
    {
        var old = StepLine.Lines(before);
        var now = StepLine.Lines(after);
        var (start, end) = StepLine.EqualEnds(old, now);

        for (var at = start; at < old.Count - end; at++)
        {
            lines.Add(new DiffLine(DiffSide.Removed, old[at]));
        }

        for (var at = start; at < now.Count - end; at++)
        {
            lines.Add(new DiffLine(DiffSide.Added, now[at]));
        }
    }

    private static string Text(JsonElement input, string key) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
