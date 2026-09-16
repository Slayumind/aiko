namespace Aiko.Core;

/// The items of the environment wizard, in the order a fresh machine needs them (D-165).
public enum ChecklistItem
{
    Install,
    FirstAccount,
    SecondEnvironment,
    Access,
    Commands,
    ProjectFolders,
    MeetAiko,
    Privacy,
    Place,
}

public enum ItemState
{
    /// Waits for an item before it: signing in needs Claude Code, and so on.
    Locked,

    /// Can be done now.
    Open,

    Done,

    /// Answered with "not now". Counts as answered, and says so.
    Skipped,
}

/// What the wizard knows, gathered by the app from the disk and from the person's answers.
/// Null for a question means it was not answered yet.
public sealed record WizardFacts
{
    public bool ClaudeInstalled { get; init; }
    public bool FirstSignedIn { get; init; }
    public bool SecondSignedIn { get; init; }
    public bool SecondSkipped { get; init; }
    public bool? AccessGranted { get; init; }
    public bool? CommandsWanted { get; init; }
    public bool FoldersVisited { get; init; }
    public int BoundFolders { get; init; }

    /// True: turn the persona on in environment 1 at Finish. False: "Later".
    public bool? PersonaWanted { get; init; }

    /// True: send the daily count. False: don't. Null: the question has not been answered, and
    /// "don't" is what happens until it is.
    public bool? StatsWanted { get; init; }
}

/// Which checklist item is ready, blocked or done, and which one opens next.
///
/// The checklist is one screen that people come back to (D-165), so the rules have to hold in any
/// order of clicks. They live here, where every order can be tested without a window.
public static class WizardChecklist
{
    public static readonly IReadOnlyList<ChecklistItem> Order = Enum.GetValues<ChecklistItem>();

    /// Project folders can be left empty, and where Aiko shows already has an answer: the tray.
    public static bool IsOptional(ChecklistItem item) => item is ChecklistItem.ProjectFolders or ChecklistItem.MeetAiko;

    public static ItemState StateOf(ChecklistItem item, WizardFacts facts) => item switch
    {
        ChecklistItem.Install => facts.ClaudeInstalled ? ItemState.Done : ItemState.Open,

        ChecklistItem.FirstAccount => !facts.ClaudeInstalled ? ItemState.Locked
            : facts.FirstSignedIn ? ItemState.Done
            : ItemState.Open,

        ChecklistItem.SecondEnvironment => !facts.FirstSignedIn ? ItemState.Locked
            : facts.SecondSignedIn ? ItemState.Done
            : facts.SecondSkipped ? ItemState.Skipped
            : ItemState.Open,

        ChecklistItem.Access => Answered(facts.FirstSignedIn, facts.AccessGranted),

        ChecklistItem.Commands => Answered(facts.FirstSignedIn, facts.CommandsWanted),

        // Bindings choose between two environments, so they wait for the second one.
        ChecklistItem.ProjectFolders => !facts.SecondSignedIn ? ItemState.Locked
            : facts.FoldersVisited || facts.BoundFolders > 0 ? ItemState.Done
            : ItemState.Open,

        // The persona goes into an environment, so it waits for the first one.
        ChecklistItem.MeetAiko => Answered(facts.FirstSignedIn, facts.PersonaWanted),

        // Asking before there is an account would be asking a stranger, and the answer mentions
        // the personality, which the item above has just offered.
        ChecklistItem.Privacy => Answered(facts.FirstSignedIn, facts.StatsWanted),

        ChecklistItem.Place => ItemState.Done,

        _ => ItemState.Locked,
    };

    /// The first item that can be done now. Null when nothing is left to do.
    public static ChecklistItem? NextToOpen(WizardFacts facts) =>
        Order.Where(item => StateOf(item, facts) == ItemState.Open).Cast<ChecklistItem?>().FirstOrDefault();

    /// "3 of 7 done". A skipped item counts as done: it has an answer.
    public static (int Done, int Total) Progress(WizardFacts facts) =>
        (Order.Count(item => StateOf(item, facts) is ItemState.Done or ItemState.Skipped), Order.Count);

    /// Every item that is not optional has an answer.
    public static bool CanFinish(WizardFacts facts) =>
        Order.Where(item => !IsOptional(item))
            .All(item => StateOf(item, facts) is ItemState.Done or ItemState.Skipped);

    /// Someone who set Aiko up before 0.2 never saw "Meet Aiko", so the checklist opens on it once
    /// at startup (D-200). Not for a person who already turned the persona on.
    public static bool OpensMeetAikoOnStart(AppSettings app, EnvironmentSettings environments) =>
        environments.HasEnvironments && !app.MeetAikoShown && !environments.Environments.Any(e => e.Persona);

    /// Somebody updating from 0.2.0 never saw the privacy question: their old yes covered a
    /// smaller thing, so it was not carried over. The checklist opens on it once, the same way it
    /// did for "Meet Aiko" (D-200).
    public static bool OpensPrivacyOnStart(AppSettings app, EnvironmentSettings environments) =>
        environments.HasEnvironments && !app.PrivacyAsked;

    private static ItemState Answered(bool unlocked, bool? answer) =>
        !unlocked ? ItemState.Locked
        : answer switch
        {
            true => ItemState.Done,
            false => ItemState.Skipped,
            null => ItemState.Open,
        };
}
