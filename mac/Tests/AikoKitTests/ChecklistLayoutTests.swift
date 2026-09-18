import Testing

@testable import AikoKit

struct ChecklistLayoutTests {
    @Test
    func theNineItemsAreReadInTheOrderThePageDrawsThem() {
        #expect(ChecklistLayout.order == [
            .install, .firstAccount, .secondEnvironment, .projectFolders,
            .access, .commands, .meetAiko, .privacy, .place,
        ])
        #expect(Set(ChecklistLayout.order) == Set(ChecklistItem.allCases))
    }

    @Test
    func threeGroupLabelsSplitTheListAndNothingElseCarriesOne() {
        let labelled = ChecklistLayout.order.filter { ChecklistLayout.labelAbove($0) != nil }

        #expect(labelled == [.firstAccount, .secondEnvironment, .access])
        #expect(ChecklistLayout.labelAbove(.firstAccount) == Strings.groupFirst)
        #expect(ChecklistLayout.labelAbove(.secondEnvironment) == Strings.groupSecond)
        #expect(ChecklistLayout.labelAbove(.access) == Strings.groupShared)
    }

    @Test
    func everyItemHasATitle() {
        for item in ChecklistLayout.order {
            #expect(!ChecklistLayout.title(item).isEmpty)
        }
    }

    @Test
    func aLockedItemSaysWhatItIsWaitingFor() {
        let words = ChecklistWords()

        #expect(ChecklistLayout.status(.firstAccount, .locked, words) == Strings.stateAfterInstall)
        #expect(ChecklistLayout.status(.projectFolders, .locked, words) == Strings.stateAfterSecond)
        #expect(ChecklistLayout.status(.access, .locked, words) == Strings.stateAfterSignIn)
    }

    @Test
    func notNowReadsAsLaterEverywhereButTheConsent() {
        let words = ChecklistWords()

        #expect(ChecklistLayout.status(.commands, .skipped, words) == Strings.stateLater)
        #expect(ChecklistLayout.status(.privacy, .skipped, words) == Strings.statsStateOff)
    }

    @Test
    func aDoneItemSaysWhatWasAnswered() {
        var words = ChecklistWords()
        words.secondName = "Work"
        words.commands = ["aiko-main", "aiko-work", ""]
        words.boundFolders = 2

        #expect(ChecklistLayout.status(.install, .done, words) == Strings.stateInstalled)
        #expect(ChecklistLayout.status(.install, .open, words) == Strings.stateWaiting)
        #expect(ChecklistLayout.status(.firstAccount, .done, words) == Strings.stateConnected)
        #expect(ChecklistLayout.status(.secondEnvironment, .done, words) == "Work")
        #expect(ChecklistLayout.status(.access, .done, words) == Strings.stateAdded)
        #expect(ChecklistLayout.status(.commands, .done, words) == "aiko-main · aiko-work")
        #expect(ChecklistLayout.status(.projectFolders, .done, words) == Strings.format(Strings.stateBound, 2))
        #expect(ChecklistLayout.status(.meetAiko, .done, words) == Strings.stateOn)
        #expect(ChecklistLayout.status(.privacy, .done, words) == Strings.statsStateOn)
    }

    @Test
    func anEmptyOptionalItemSaysItIsOptional() {
        let words = ChecklistWords()

        #expect(ChecklistLayout.status(.projectFolders, .open, words) == Strings.stateOptional)
        #expect(ChecklistLayout.status(.meetAiko, .open, words) == Strings.stateOptional)
        #expect(ChecklistLayout.status(.privacy, .open, words) == Strings.statsStateUnset)
    }

    /// The menu bar item is the default on macOS (D-250), and the island is the equal second
    /// choice: the same two answers as the tray and the island on Windows.
    @Test
    func wherePartSaysTheMenuBarUntilTheIslandIsPicked() {
        var words = ChecklistWords()
        #expect(ChecklistLayout.status(.place, .done, words) == Strings.stateMenuBar)

        words.island = true
        #expect(ChecklistLayout.status(.place, .done, words) == Strings.stateIsland)
    }
}
