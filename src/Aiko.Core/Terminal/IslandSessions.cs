namespace Aiko.Core.Terminal;

/// What the island shows about the sessions after the rings of the limits (D-364): how many work,
/// how many wait, and the waiting session a click on the mark opens. Nothing working or waiting
/// leaves the island as it was.
public sealed record IslandSessions(int Working, int Waiting, string? FirstWaiting)
{
    public static readonly IslandSessions None = new(0, 0, null);

    public bool IsEmpty => Working == 0 && Waiting == 0;

    /// The running terminals in the order of the list, with what each one does.
    public static IslandSessions Of(IEnumerable<(string Id, TerminalActivity Activity)> terminals)
    {
        var working = 0;
        var waiting = 0;
        string? first = null;
        foreach (var (id, activity) in terminals)
        {
            if (activity == TerminalActivity.Working)
            {
                working++;
            }
            else if (activity == TerminalActivity.Waiting)
            {
                waiting++;
                first ??= id;
            }
        }

        return new IslandSessions(working, waiting, first);
    }
}
