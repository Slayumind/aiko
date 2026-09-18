import Foundation

/// The items of the environment wizard, in the order a fresh machine needs them (D-165).
public enum ChecklistItem: Sendable, Equatable, CaseIterable {
    case install
    case firstAccount
    case secondEnvironment
    case access
    case commands
    case projectFolders
    case meetAiko
    case privacy
    case place
}

public enum ItemState: Sendable, Equatable {
    /// Waits for an item before it: signing in needs Claude Code, and so on.
    case locked

    /// Can be done now.
    case open

    case done

    /// Answered with "not now". Counts as answered, and says so.
    case skipped
}

/// What the wizard knows, gathered by the app from the disk and from the person's answers.
/// Nil for a question means it was not answered yet.
public struct WizardFacts: Sendable, Equatable {
    public var claudeInstalled = false
    public var firstSignedIn = false
    public var secondSignedIn = false
    public var secondSkipped = false
    public var accessGranted: Bool?
    public var commandsWanted: Bool?
    public var foldersVisited = false
    public var boundFolders = 0

    /// True: turn the persona on in environment 1 at Finish. False: "Later".
    public var personaWanted: Bool?

    /// True: send the daily count. False: don't. Nil: the question has not been answered, and
    /// "don't" is what happens until it is.
    public var statsWanted: Bool?

    public init() {}
}

/// Which checklist item is ready, blocked or done, and which one opens next.
///
/// The checklist is one screen that people come back to (D-165), so the rules have to hold in any
/// order of clicks. They live here, where every order can be tested without a window.
public enum WizardChecklist {
    public static let order = ChecklistItem.allCases

    /// Project folders can be left empty, and where Aiko shows already has an answer: the tray.
    public static func isOptional(_ item: ChecklistItem) -> Bool {
        item == .projectFolders || item == .meetAiko
    }

    public static func stateOf(_ item: ChecklistItem, _ facts: WizardFacts) -> ItemState {
        switch item {
        case .install:
            return facts.claudeInstalled ? .done : .open

        case .firstAccount:
            if !facts.claudeInstalled { return .locked }
            return facts.firstSignedIn ? .done : .open

        case .secondEnvironment:
            if !facts.firstSignedIn { return .locked }
            if facts.secondSignedIn { return .done }
            return facts.secondSkipped ? .skipped : .open

        case .access:
            return answered(facts.firstSignedIn, facts.accessGranted)

        case .commands:
            return answered(facts.firstSignedIn, facts.commandsWanted)

        // Bindings choose between two environments, so they wait for the second one.
        case .projectFolders:
            if !facts.secondSignedIn { return .locked }
            return facts.foldersVisited || facts.boundFolders > 0 ? .done : .open

        // The persona goes into an environment, so it waits for the first one.
        case .meetAiko:
            return answered(facts.firstSignedIn, facts.personaWanted)

        // Asking before there is an account would be asking a stranger, and the answer mentions
        // the personality, which the item above has just offered.
        case .privacy:
            return answered(facts.firstSignedIn, facts.statsWanted)

        case .place:
            return .done
        }
    }

    /// The first item that can be done now. Nil when nothing is left to do.
    public static func nextToOpen(_ facts: WizardFacts) -> ChecklistItem? {
        order.first { stateOf($0, facts) == .open }
    }

    /// "3 of 7 done". A skipped item counts as done: it has an answer.
    public static func progress(_ facts: WizardFacts) -> (done: Int, total: Int) {
        (order.filter { stateOf($0, facts) == .done || stateOf($0, facts) == .skipped }.count, order.count)
    }

    /// Every item that is not optional has an answer.
    public static func canFinish(_ facts: WizardFacts) -> Bool {
        order.filter { !isOptional($0) }.allSatisfy {
            let state = stateOf($0, facts)
            return state == .done || state == .skipped
        }
    }

    /// Someone who set Aiko up before 0.2 never saw "Meet Aiko", so the checklist opens on it once
    /// at startup (D-200). Not for a person who already turned the persona on.
    public static func opensMeetAikoOnStart(_ app: AppSettings, _ environments: EnvironmentSettings) -> Bool {
        environments.hasEnvironments && !app.meetAikoShown && !environments.environments.contains(where: \.persona)
    }

    /// Somebody updating from 0.2.0 never saw the privacy question: their old yes covered a
    /// smaller thing, so it was not carried over. The checklist opens on it once, the same way it
    /// did for "Meet Aiko" (D-200).
    public static func opensPrivacyOnStart(_ app: AppSettings, _ environments: EnvironmentSettings) -> Bool {
        environments.hasEnvironments && !app.privacyAsked
    }

    private static func answered(_ unlocked: Bool, _ answer: Bool?) -> ItemState {
        guard unlocked else { return .locked }
        switch answer {
        case .some(true): return .done
        case .some(false): return .skipped
        case .none: return .open
        }
    }
}
