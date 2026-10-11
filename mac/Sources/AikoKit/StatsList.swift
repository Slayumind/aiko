import Foundation

/// What the daily count sends, as a numbered list a person agreed to.
///
/// The consent is to a list that the checklist and the Privacy page show line by line. When the
/// list grows, an old yes does not cover the new lines. So the app remembers which list the person
/// has been shown, sends a new value only after they saw the list that names it, and opens the
/// Privacy page once after an update that made the list longer. There they can leave the count
/// on or turn it off.
public enum StatsList {
    /// 1: version, system, daily id, week, month, personality. 2 added the skills and the memory.
    public static let current = 2

    /// The list that first named the skills and the memory flags.
    public static let withSkillsAndMemory = 2

    public static func sendsSkillsAndMemory(_ app: AppSettings) -> Bool {
        app.sendStats && app.statsListSeen >= withSkillsAndMemory
    }

    /// Only somebody who is counted has anything to look at again, and only once Aiko is set up.
    public static func asksToLookAgain(_ app: AppSettings, _ environments: EnvironmentSettings) -> Bool {
        environments.hasEnvironments && app.sendStats && app.statsListSeen < current
    }

    /// After the list was put in front of the person: on the page opened for it, or with the
    /// question answered in the checklist or on the Privacy page.
    public static func seen(_ app: AppSettings) -> AppSettings {
        guard app.statsListSeen < current else { return app }
        var copy = app
        copy.statsListSeen = current
        return copy
    }
}
