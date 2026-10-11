namespace Aiko.Core;

/// What the daily count sends, as a numbered list a person agreed to.
///
/// The consent is to a list that the checklist and the Privacy page show line by line. When the
/// list grows, an old yes does not cover the new lines. So the app remembers which list the person
/// has been shown, sends a new value only after they saw the list that names it, and opens the
/// Privacy page once after an update that made the list longer. There they can leave the count
/// on or turn it off.
public static class StatsList
{
    /// 1: version, system, daily id, week, month, personality. 2 added the skills and the memory.
    public const int Current = 2;

    /// The list that first named the skills and the memory flags.
    public const int WithSkillsAndMemory = 2;

    public static bool SendsSkillsAndMemory(AppSettings app) =>
        app.SendStats && app.StatsListSeen >= WithSkillsAndMemory;

    /// Only somebody who is counted has anything to look at again, and only once Aiko is set up.
    public static bool AsksToLookAgain(AppSettings app, EnvironmentSettings environments) =>
        environments.HasEnvironments && app.SendStats && app.StatsListSeen < Current;

    /// After the list was put in front of the person: on the page opened for it, or with the
    /// question answered in the checklist or on the Privacy page.
    public static AppSettings Seen(AppSettings app) =>
        app.StatsListSeen >= Current ? app : app with { StatsListSeen = Current };
}
