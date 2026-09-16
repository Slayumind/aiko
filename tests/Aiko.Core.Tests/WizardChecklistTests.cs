using Aiko.Core;

namespace Aiko.Core.Tests;

public class WizardChecklistTests
{
    private static readonly WizardFacts FreshMachine = new();

    private static readonly WizardFacts TwoAccountsFound = new()
    {
        ClaudeInstalled = true,
        FirstSignedIn = true,
        SecondSignedIn = true,
    };

    [Fact]
    public void A_fresh_machine_starts_with_the_install_and_everything_else_waits()
    {
        Assert.Equal(ChecklistItem.Install, WizardChecklist.NextToOpen(FreshMachine));
        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.FirstAccount, FreshMachine));
        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.Access, FreshMachine));
        Assert.Equal(ItemState.Done, WizardChecklist.StateOf(ChecklistItem.Place, FreshMachine));
        Assert.False(WizardChecklist.CanFinish(FreshMachine));
    }

    [Fact]
    public void Installing_opens_the_first_account()
    {
        var facts = FreshMachine with { ClaudeInstalled = true };

        Assert.Equal(ChecklistItem.FirstAccount, WizardChecklist.NextToOpen(facts));
        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.SecondEnvironment, facts));
    }

    [Fact]
    public void With_two_accounts_already_there_the_wizard_goes_straight_to_access()
    {
        Assert.Equal(ChecklistItem.Access, WizardChecklist.NextToOpen(TwoAccountsFound));
        Assert.Equal((4, 9), WizardChecklist.Progress(TwoAccountsFound));
    }

    [Fact]
    public void Not_now_is_an_answer_and_finishing_needs_only_answers()
    {
        var facts = TwoAccountsFound with { AccessGranted = false, CommandsWanted = false, StatsWanted = false };

        Assert.Equal(ItemState.Skipped, WizardChecklist.StateOf(ChecklistItem.Access, facts));
        Assert.True(WizardChecklist.CanFinish(facts));
        Assert.Equal(ChecklistItem.ProjectFolders, WizardChecklist.NextToOpen(facts));
    }

    [Fact]
    public void One_account_is_enough_when_the_second_is_put_off()
    {
        var facts = new WizardFacts
        {
            ClaudeInstalled = true,
            FirstSignedIn = true,
            SecondSkipped = true,
            AccessGranted = true,
            CommandsWanted = true,
            StatsWanted = false,
        };

        Assert.True(WizardChecklist.CanFinish(facts));
        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.ProjectFolders, facts));
        Assert.Equal(ChecklistItem.MeetAiko, WizardChecklist.NextToOpen(facts));
    }

    [Fact]
    public void Project_folders_are_optional_and_done_once_looked_at()
    {
        Assert.True(WizardChecklist.IsOptional(ChecklistItem.ProjectFolders));
        Assert.Equal(ItemState.Done, WizardChecklist.StateOf(ChecklistItem.ProjectFolders, TwoAccountsFound with { FoldersVisited = true }));
        Assert.Equal(ItemState.Done, WizardChecklist.StateOf(ChecklistItem.ProjectFolders, TwoAccountsFound with { BoundFolders = 2 }));
    }

    [Fact]
    public void Every_item_is_answered_at_the_end()
    {
        var facts = TwoAccountsFound with { AccessGranted = true, CommandsWanted = true, FoldersVisited = true, PersonaWanted = false, StatsWanted = false };

        Assert.Equal((9, 9), WizardChecklist.Progress(facts));
        Assert.Null(WizardChecklist.NextToOpen(facts));
    }

    [Fact]
    public void Meet_Aiko_waits_for_the_first_account_and_does_not_block_Finish()
    {
        var ready = FreshMachine with { ClaudeInstalled = true, FirstSignedIn = true, SecondSkipped = true, AccessGranted = true, CommandsWanted = true, StatsWanted = false };

        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.MeetAiko, FreshMachine));
        Assert.Equal(ItemState.Open, WizardChecklist.StateOf(ChecklistItem.MeetAiko, ready));
        Assert.True(WizardChecklist.IsOptional(ChecklistItem.MeetAiko));
        Assert.True(WizardChecklist.CanFinish(ready));
        Assert.Equal(ItemState.Done, WizardChecklist.StateOf(ChecklistItem.MeetAiko, ready with { PersonaWanted = true }));
        Assert.Equal(ItemState.Skipped, WizardChecklist.StateOf(ChecklistItem.MeetAiko, ready with { PersonaWanted = false }));
    }

    /// Consent is the one answer the wizard will not assume. Skipping the item is allowed, but
    /// leaving it unanswered is not: a wizard that finished without asking would have collected
    /// nothing, and the person would never learn the question existed.
    [Fact]
    public void Privacy_waits_for_the_first_account_and_has_to_be_answered()
    {
        var ready = FreshMachine with { ClaudeInstalled = true, FirstSignedIn = true, SecondSkipped = true, AccessGranted = true, CommandsWanted = true };

        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.Privacy, FreshMachine));
        Assert.Equal(ItemState.Open, WizardChecklist.StateOf(ChecklistItem.Privacy, ready));
        Assert.False(WizardChecklist.IsOptional(ChecklistItem.Privacy));
        Assert.False(WizardChecklist.CanFinish(ready));

        Assert.Equal(ItemState.Done, WizardChecklist.StateOf(ChecklistItem.Privacy, ready with { StatsWanted = true }));
        Assert.Equal(ItemState.Skipped, WizardChecklist.StateOf(ChecklistItem.Privacy, ready with { StatsWanted = false }));
        Assert.True(WizardChecklist.CanFinish(ready with { StatsWanted = false }));
    }

    [Fact]
    public void Privacy_comes_after_meeting_Aiko_and_before_the_place()
    {
        var order = WizardChecklist.Order.ToList();

        Assert.Equal(order.IndexOf(ChecklistItem.MeetAiko) + 1, order.IndexOf(ChecklistItem.Privacy));
        Assert.Equal(order.IndexOf(ChecklistItem.Privacy) + 1, order.IndexOf(ChecklistItem.Place));
    }

    [Fact]
    public void The_privacy_question_opens_once_for_somebody_who_updates()
    {
        var set_up = OneEnvironment(persona: true);

        Assert.True(WizardChecklist.OpensPrivacyOnStart(AppSettings.Default, set_up));
        Assert.False(WizardChecklist.OpensPrivacyOnStart(AppSettings.Default with { PrivacyAsked = true }, set_up));

        // Answering "don't send" is still an answer: it does not come back tomorrow.
        Assert.False(WizardChecklist.OpensPrivacyOnStart(
            AppSettings.Default with { PrivacyAsked = true, SendStats = false },
            set_up));

        // A fresh machine goes through the whole checklist instead.
        Assert.False(WizardChecklist.OpensPrivacyOnStart(AppSettings.Default, EnvironmentSettings.Empty));
    }

    private static EnvironmentSettings OneEnvironment(bool persona) =>
        new([new AikoEnvironment("Aiko", ["C:/Users/someone/.claude"]) { Persona = persona }]);

    [Fact]
    public void Meet_Aiko_opens_once_for_someone_who_set_Aiko_up_before()
    {
        Assert.True(WizardChecklist.OpensMeetAikoOnStart(AppSettings.Default, OneEnvironment(persona: false)));
        Assert.False(WizardChecklist.OpensMeetAikoOnStart(AppSettings.Default with { MeetAikoShown = true }, OneEnvironment(persona: false)));
    }

    [Fact]
    public void Meet_Aiko_does_not_open_on_a_first_run_or_when_the_persona_is_already_on()
    {
        Assert.False(WizardChecklist.OpensMeetAikoOnStart(AppSettings.Default, EnvironmentSettings.Empty));
        Assert.False(WizardChecklist.OpensMeetAikoOnStart(AppSettings.Default, OneEnvironment(persona: true)));
    }
}
