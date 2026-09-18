import Foundation

/// What the checklist page knows about itself while it is being drawn: the answers the person has
/// typed, which never reach WizardFacts because they are words and not states.
public struct ChecklistWords: Sendable, Equatable {
    /// The name of environment 2, as typed. Empty while there is no second environment.
    public var secondName = ""

    /// The launch commands in the fields, in order.
    public var commands: [String] = []

    public var boundFolders = 0

    /// Where Aiko will show: the island, or the menu bar item that is the default here (D-250).
    public var island = false

    public init() {}
}

/// The order of the items on the page and the words beside each one.
///
/// The order here is the reading order of ChecklistPage.xaml, which is not the order
/// WizardChecklist walks: bindings are shown under the second environment they choose between,
/// while the wizard asks for access and the commands first.
public enum ChecklistLayout {
    public static let order: [ChecklistItem] = [
        .install,
        .firstAccount,
        .secondEnvironment,
        .projectFolders,
        .access,
        .commands,
        .meetAiko,
        .privacy,
        .place,
    ]

    /// The group label above an item, when it starts a group.
    public static func labelAbove(_ item: ChecklistItem) -> String? {
        switch item {
        case .firstAccount: return Strings.groupFirst
        case .secondEnvironment: return Strings.groupSecond
        case .access: return Strings.groupShared
        default: return nil
        }
    }

    public static func title(_ item: ChecklistItem) -> String {
        switch item {
        case .install: return Strings.itemInstall
        case .firstAccount: return Strings.itemAccount
        case .secondEnvironment: return Strings.itemSecond
        case .projectFolders: return Strings.itemFolders
        case .access: return Strings.wizardStepAccess
        case .commands: return Strings.itemCommands
        case .meetAiko: return Strings.itemMeetAiko
        case .privacy: return Strings.wizardStats
        case .place: return Strings.wizardStepWhere
        }
    }

    /// The short word on the right of a header. The twin of ChecklistPage.StatusOf.
    public static func status(_ item: ChecklistItem, _ state: ItemState, _ words: ChecklistWords) -> String {
        if state == .locked {
            switch item {
            case .firstAccount: return Strings.stateAfterInstall
            case .projectFolders: return Strings.stateAfterSecond
            default: return Strings.stateAfterSignIn
            }
        }

        if state == .skipped {
            // "Don't send" is an answer, not a postponement, so this item says so plainly.
            return item == .privacy ? Strings.statsStateOff : Strings.stateLater
        }

        let done = state == .done
        switch item {
        case .install:
            return done ? Strings.stateInstalled : Strings.stateWaiting
        case .firstAccount:
            return done ? Strings.stateConnected : Strings.stateSignInNeeded
        case .secondEnvironment:
            return done ? words.secondName : ""
        case .access:
            return done ? Strings.stateAdded : ""
        case .commands:
            return done ? words.commands.filter { !$0.isEmpty }.joined(separator: " · ") : ""
        case .projectFolders:
            return words.boundFolders > 0
                ? Strings.format(Strings.stateBound, words.boundFolders)
                : Strings.stateOptional
        case .meetAiko:
            return done ? Strings.stateOn : Strings.stateOptional
        case .privacy:
            return done ? Strings.statsStateOn : Strings.statsStateUnset
        case .place:
            return words.island ? Strings.stateIsland : Strings.stateMenuBar
        }
    }
}
