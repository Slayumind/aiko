import Testing

@testable import AikoKit

struct WizardChecklistTests {
    static let freshMachine = WizardFacts()

    static var twoAccountsFound: WizardFacts {
        var facts = WizardFacts()
        facts.claudeInstalled = true
        facts.firstSignedIn = true
        facts.secondSignedIn = true
        return facts
    }

    @Test
    func aFreshMachineStartsWithTheInstallAndEverythingElseWaits() {
        #expect(WizardChecklist.nextToOpen(Self.freshMachine) == .install)
        #expect(WizardChecklist.stateOf(.firstAccount, Self.freshMachine) == .locked)
        #expect(WizardChecklist.stateOf(.access, Self.freshMachine) == .locked)
        #expect(WizardChecklist.stateOf(.place, Self.freshMachine) == .done)
        #expect(!WizardChecklist.canFinish(Self.freshMachine))
    }

    @Test
    func installingOpensTheFirstAccount() {
        var facts = Self.freshMachine
        facts.claudeInstalled = true

        #expect(WizardChecklist.nextToOpen(facts) == .firstAccount)
        #expect(WizardChecklist.stateOf(.secondEnvironment, facts) == .locked)
    }

    @Test
    func withTwoAccountsAlreadyThereTheWizardGoesStraightToAccess() {
        #expect(WizardChecklist.nextToOpen(Self.twoAccountsFound) == .access)
        #expect(WizardChecklist.progress(Self.twoAccountsFound) == (4, 9))
    }

    @Test
    func notNowIsAnAnswerAndFinishingNeedsOnlyAnswers() {
        var facts = Self.twoAccountsFound
        facts.accessGranted = false
        facts.commandsWanted = false
        facts.statsWanted = false

        #expect(WizardChecklist.stateOf(.access, facts) == .skipped)
        #expect(WizardChecklist.canFinish(facts))
        #expect(WizardChecklist.nextToOpen(facts) == .projectFolders)
    }

    @Test
    func oneAccountIsEnoughWhenTheSecondIsPutOff() {
        var facts = WizardFacts()
        facts.claudeInstalled = true
        facts.firstSignedIn = true
        facts.secondSkipped = true
        facts.accessGranted = true
        facts.commandsWanted = true
        facts.statsWanted = false

        #expect(WizardChecklist.canFinish(facts))
        #expect(WizardChecklist.stateOf(.projectFolders, facts) == .locked)
        #expect(WizardChecklist.nextToOpen(facts) == .meetAiko)
    }

    @Test
    func projectFoldersAreOptionalAndDoneOnceLookedAt() {
        var visited = Self.twoAccountsFound
        visited.foldersVisited = true
        var bound = Self.twoAccountsFound
        bound.boundFolders = 2

        #expect(WizardChecklist.isOptional(.projectFolders))
        #expect(WizardChecklist.stateOf(.projectFolders, visited) == .done)
        #expect(WizardChecklist.stateOf(.projectFolders, bound) == .done)
    }

    @Test
    func everyItemIsAnsweredAtTheEnd() {
        var facts = Self.twoAccountsFound
        facts.accessGranted = true
        facts.commandsWanted = true
        facts.foldersVisited = true
        facts.personaWanted = false
        facts.statsWanted = false

        #expect(WizardChecklist.progress(facts) == (9, 9))
        #expect(WizardChecklist.nextToOpen(facts) == nil)
    }

    @Test
    func meetAikoWaitsForTheFirstAccountAndDoesNotBlockFinish() {
        var ready = Self.freshMachine
        ready.claudeInstalled = true
        ready.firstSignedIn = true
        ready.secondSkipped = true
        ready.accessGranted = true
        ready.commandsWanted = true
        ready.statsWanted = false

        var wanted = ready
        wanted.personaWanted = true
        var later = ready
        later.personaWanted = false

        #expect(WizardChecklist.stateOf(.meetAiko, Self.freshMachine) == .locked)
        #expect(WizardChecklist.stateOf(.meetAiko, ready) == .open)
        #expect(WizardChecklist.isOptional(.meetAiko))
        #expect(WizardChecklist.canFinish(ready))
        #expect(WizardChecklist.stateOf(.meetAiko, wanted) == .done)
        #expect(WizardChecklist.stateOf(.meetAiko, later) == .skipped)
    }

    /// Consent is the one answer the wizard will not assume. Skipping the item is allowed, but
    /// leaving it unanswered is not: a wizard that finished without asking would have collected
    /// nothing, and the person would never learn the question existed.
    @Test
    func privacyWaitsForTheFirstAccountAndHasToBeAnswered() {
        var ready = Self.freshMachine
        ready.claudeInstalled = true
        ready.firstSignedIn = true
        ready.secondSkipped = true
        ready.accessGranted = true
        ready.commandsWanted = true

        var sending = ready
        sending.statsWanted = true
        var refused = ready
        refused.statsWanted = false

        #expect(WizardChecklist.stateOf(.privacy, Self.freshMachine) == .locked)
        #expect(WizardChecklist.stateOf(.privacy, ready) == .open)
        #expect(!WizardChecklist.isOptional(.privacy))
        #expect(!WizardChecklist.canFinish(ready))

        #expect(WizardChecklist.stateOf(.privacy, sending) == .done)
        #expect(WizardChecklist.stateOf(.privacy, refused) == .skipped)
        #expect(WizardChecklist.canFinish(refused))
    }

    @Test
    func privacyComesAfterMeetingAikoAndBeforeThePlace() {
        let order = WizardChecklist.order

        #expect(order.firstIndex(of: .meetAiko)! + 1 == order.firstIndex(of: .privacy)!)
        #expect(order.firstIndex(of: .privacy)! + 1 == order.firstIndex(of: .place)!)
    }

    @Test
    func thePrivacyQuestionOpensOnceForSomebodyWhoUpdates() {
        let setUp = Self.oneEnvironment(persona: true)
        var asked = AppSettings.default
        asked.privacyAsked = true
        var askedAndRefused = asked
        askedAndRefused.sendStats = false

        #expect(WizardChecklist.opensPrivacyOnStart(.default, setUp))
        #expect(!WizardChecklist.opensPrivacyOnStart(asked, setUp))

        // Answering "don't send" is still an answer: it does not come back tomorrow.
        #expect(!WizardChecklist.opensPrivacyOnStart(askedAndRefused, setUp))

        // A fresh machine goes through the whole checklist instead.
        #expect(!WizardChecklist.opensPrivacyOnStart(.default, .empty))
    }

    private static func oneEnvironment(persona: Bool) -> EnvironmentSettings {
        EnvironmentSettings([
            AikoEnvironment("Aiko", ["C:/Users/someone/.claude"], persona: persona)
        ])
    }

    @Test
    func meetAikoOpensOnceForSomeoneWhoSetAikoUpBefore() {
        var shown = AppSettings.default
        shown.meetAikoShown = true

        #expect(WizardChecklist.opensMeetAikoOnStart(.default, Self.oneEnvironment(persona: false)))
        #expect(!WizardChecklist.opensMeetAikoOnStart(shown, Self.oneEnvironment(persona: false)))
    }

    @Test
    func meetAikoDoesNotOpenOnAFirstRunOrWhenThePersonaIsAlreadyOn() {
        #expect(!WizardChecklist.opensMeetAikoOnStart(.default, .empty))
        #expect(!WizardChecklist.opensMeetAikoOnStart(.default, Self.oneEnvironment(persona: true)))
    }
}
