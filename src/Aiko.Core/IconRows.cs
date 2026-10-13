namespace Aiko.Core;

/// What the small picture shows: the tray icon on Windows and the menu bar icon on macOS. The
/// ring is one environment and the dot is another. The island needs no such rule: it draws a ring
/// for every environment.
public static class IconRows
{
    /// The ring goes to the environment the person chose, when it has numbers, and to the first
    /// one that has them otherwise. The dot is the first other environment with numbers. With
    /// nothing reported yet both are empty, and the icon draws the dashed ring.
    public static (CardRow? Ring, CardRow? Dot) Choose(IReadOnlyList<CardState> cards, string? ringEnvironment)
    {
        var withData = cards.Where(card => card.HasData).ToList();
        if (withData.Count == 0)
        {
            return (null, null);
        }

        var ring = withData.FirstOrDefault(card => card.Environment == ringEnvironment) ?? withData[0];
        var dot = withData.FirstOrDefault(card => card.Environment != ring.Environment);
        return (ring.IconRow, dot?.IconRow);
    }
}
