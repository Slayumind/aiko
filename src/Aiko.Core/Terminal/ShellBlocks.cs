namespace Aiko.Core.Terminal;

/// One command of a command line and what it printed (D-363, D-412). FirstLine and EndLine are rows
/// of the screen, EndLine not included; ExitCode is null while it runs. Times are in milliseconds.
public sealed record ShellBlock(string Command, int FirstLine, int EndLine, int? ExitCode, long StartedAt, long? EndedAt)
{
    public bool Running => EndedAt is null;

    public bool Failed => ExitCode is { } code && code != 0;

    public int LineCount => Math.Max(0, EndLine - FirstLine);
}

/// Cuts the screen of a command line into commands by the marks of shell integration (OSC 133):
/// B starts what the person typed, C starts the output, D ends it with the exit code. A command
/// that is only Enter is no block. A shell that sends no C (PowerShell without its hook) gets the
/// output from the line after the command. Rows: spec/cases/shell-screen.
public static class ShellBlocks
{
    public static IReadOnlyList<ShellBlock> Of(ShellScreen screen)
    {
        var blocks = new List<ShellBlock>();
        ShellMark? typed = null;
        ShellMark? output = null;
        var cursor = screen.Cursor;

        foreach (var mark in screen.Marks)
        {
            switch (mark.Kind)
            {
                case 'A':
                    typed = null;
                    output = null;
                    break;
                case 'B':
                    typed = mark;
                    output = null;
                    break;
                case 'C':
                    output = mark;
                    break;
                case 'D' when typed is not null:
                    Add(blocks, screen, typed, output, mark);
                    typed = null;
                    output = null;
                    break;
            }
        }

        // A command that still runs: from C to the cursor.
        if (typed is not null && output is not null)
        {
            var end = new ShellMark('D', cursor.Row + screen.Dropped, cursor.Column, null, output.At);
            Add(blocks, screen, typed, output, end, running: true);
        }

        return blocks;
    }

    private static void Add(List<ShellBlock> blocks, ShellScreen screen, ShellMark typed, ShellMark? output, ShellMark end, bool running = false)
    {
        var dropped = screen.Dropped;
        var typedRow = typed.Row - dropped;

        // Without C the command is the rest of its line and the lines the terminal broke from it.
        var commandEnd = typedRow;
        while (output is null && screen.Wrapped(commandEnd))
        {
            commandEnd++;
        }

        var outputRow = output is not null ? output.Row - dropped : commandEnd + 1;
        var outputColumn = output?.Column ?? 0;
        var command = output is not null
            ? screen.Text(typedRow, typed.Column, outputRow, outputColumn)
            : screen.Text(typedRow, typed.Column, commandEnd, int.MaxValue);
        command = command.Trim();
        if (command.Length == 0)
        {
            return;
        }

        // The output starts on the line after the command when C came at the end of it.
        var first = outputColumn > 0 && output is not null && outputRow == typedRow ? outputRow + 1 : outputRow;
        var endRow = end.Row - dropped;
        var last = end.Column > 0 ? endRow + 1 : endRow;
        if (first < 0)
        {
            return;
        }

        // Blank lines at either end belong to no one: a spinner leaves one, the next prompt starts with one.
        last = Math.Max(first, last);
        while (first < last && screen.LineText(first).Length == 0)
        {
            first++;
        }

        while (last > first && screen.LineText(last - 1).Length == 0)
        {
            last--;
        }

        var startedAt = output?.At ?? typed.At;
        blocks.Add(new ShellBlock(
            command,
            first,
            last,
            running ? null : end.Code ?? 0,
            startedAt,
            running ? null : end.At));
    }
}
