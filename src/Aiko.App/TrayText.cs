using Aiko.Core;

namespace Aiko.App;

/// The line Windows shows when the mouse rests on the tray icon.
///
/// It used to say "Aiko" and nothing else, which wasted the one surface that always works.
/// Windows 11 hides the icon of a new app behind the arrow, and under that arrow the hover card
/// never opens at all, so the tooltip is the only thing some people will ever read. A screen
/// reader reads it too, and the ring and dot say nothing to one.
static class TrayText
{
    /// Windows keeps 128 characters including the closing zero.
    private const int Room = 127;

    public static string Tooltip(IReadOnlyList<CardState> cards, string? newerVersion)
    {
        var parts = new List<string>();

        foreach (var card in cards)
        {
            parts.Add(card.IconRow is { } row
                ? $"{card.Environment} {row.Percent}%"
                : $"{card.Environment} —");
        }

        if (parts.Count == 0)
        {
            parts.Add("Aiko");
        }

        if (cards.Count > 0 && cards.All(c => c.Freshness == DataFreshness.None))
        {
            parts.Add(Strings.TrayOpenClaudeCode);
        }
        else if (cards.Any(c => c.Freshness == DataFreshness.Stale))
        {
            var newest = cards.Where(c => c.UpdatedAt is not null).Max(c => c.UpdatedAt);
            parts.Add(string.Format(Strings.TrayLastSeen, $"{newest:HH:mm}"));
        }

        if (newerVersion is not null)
        {
            parts.Add(string.Format(Strings.TrayUpdateOut, newerVersion));
        }

        var text = string.Join(" · ", parts);
        return text.Length <= Room ? text : text[..Room];
    }
}
