/// What the small picture shows: the tray icon on Windows, the menu bar icon on macOS and the
/// island. The ring is one environment and the dot is another. The twin of IconRows.cs.
public enum IconRows {
    /// The ring goes to the environment the person chose, when it has numbers, and to the first
    /// one that has them otherwise. The dot is the first other environment with numbers. With
    /// nothing reported yet both are empty, and the icon draws the dashed ring.
    public static func choose(_ cards: [CardState], ringEnvironment: String?) -> (ring: CardRow?, dot: CardRow?) {
        let withData = cards.filter(\.hasData)
        guard !withData.isEmpty else { return (nil, nil) }

        let ring = withData.first { $0.environment == ringEnvironment } ?? withData[0]
        let dot = withData.first { $0.environment != ring.environment }
        return (ring.iconRow, dot?.iconRow)
    }
}
