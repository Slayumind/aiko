namespace Aiko.Core;

/// The line shown when the mouse rests on the icon by the clock or in the menu bar.
///
/// It used to say "Aiko" and nothing else, which wasted the one surface that always works.
/// Windows 11 hides the icon of a new app behind the arrow, so the tooltip is the only thing some
/// people will ever read. A screen reader reads it too, and the ring and dot say nothing to one.
///
/// The twin of TrayText.swift, including the length: Windows keeps 128 characters with the closing
/// zero, macOS has no such limit, and one text for both is one text to read and translate.
public static class TrayText
{
    public const int Room = 127;

    public static string Tooltip(IReadOnlyList<CardState> cards, string? newerVersion = null, TimeZoneInfo? zone = null)
    {
        var parts = cards
            .Select(card => card.IconRow is { } row ? $"{card.Environment} {row.Percent}%" : $"{card.Environment} —")
            .ToList();

        if (parts.Count == 0)
        {
            parts.Add("Aiko");
        }

        if (cards.Count > 0 && cards.All(card => card.Freshness == DataFreshness.None))
        {
            parts.Add(Strings.TrayOpenClaudeCode);
        }
        else if (cards.Any(card => card.Freshness == DataFreshness.Stale))
        {
            var newest = cards.Where(card => card.UpdatedAt is not null).Max(card => card.UpdatedAt);
            parts.Add(CardText.Format(Strings.TrayLastSeen, CardText.Clock(newest, zone)));
        }

        if (newerVersion is not null)
        {
            parts.Add(CardText.Format(Strings.TrayUpdateOut, newerVersion));
        }

        var text = string.Join(" · ", parts);
        return text.Length <= Room ? text : text[..Room];
    }
}
