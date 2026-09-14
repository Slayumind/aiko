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
        Assert.Equal((4, 7), WizardChecklist.Progress(TwoAccountsFound));
    }

    [Fact]
    public void Not_now_is_an_answer_and_finishing_needs_only_answers()
    {
        var facts = TwoAccountsFound with { AccessGranted = false, CommandsWanted = false };

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
        };

        Assert.True(WizardChecklist.CanFinish(facts));
        Assert.Equal(ItemState.Locked, WizardChecklist.StateOf(ChecklistItem.ProjectFolders, facts));
        Assert.Null(WizardChecklist.NextToOpen(facts));
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
        var facts = TwoAccountsFound with { AccessGranted = true, CommandsWanted = true, FoldersVisited = true };

        Assert.Equal((7, 7), WizardChecklist.Progress(facts));
        Assert.Null(WizardChecklist.NextToOpen(facts));
    }
}
