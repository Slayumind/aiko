import Foundation

/// The card, ready to be drawn: words already chosen, tones already picked. Built fresh every time
/// the numbers change, because the card is created on demand and closed, never kept around.
///
/// The twin of CardModel.cs on Windows. Where the WPF version holds brushes and visibilities, this
/// one holds the tone and a plain "shown or not": AppKit and SwiftUI spell those differently, and
/// a rule that names a brush cannot be tested without a window.
public struct CardModel: Sendable, Equatable {
    public let updated: String
    public let blocks: [EnvironmentBlock]

    public init(updated: String, blocks: [EnvironmentBlock]) {
        self.updated = updated
        self.blocks = blocks
    }

    public static func from(
        _ cards: [CardState],
        _ now: Date,
        noAccess: Set<String> = [],
        accounts: [String: CardAccount] = [:],
        timeZone: TimeZone = .current
    ) -> CardModel {
        let newest = cards
            .filter { $0.updatedAt != nil }
            .max { ($0.updatedAt ?? .distantPast) < ($1.updatedAt ?? .distantPast) }

        let blocks = cards.enumerated().map { index, card in
            EnvironmentBlock.from(
                card,
                first: index == 0,
                noAccess: noAccess.contains(card.environment),
                account: accounts[card.environment],
                working: card.isWorkingAt(now),
                timeZone: timeZone)
        }

        return CardModel(
            updated: CardText.updated(newest?.freshness ?? .none, newest?.updatedAt, timeZone: timeZone),
            blocks: blocks)
    }
}

/// The dot beside the state word: filled when the account is connected, an empty ring when not.
public enum StateDot: Sendable, Equatable {
    case filled
    case hollow
}

public struct EnvironmentBlock: Sendable, Equatable {
    public let name: String
    public let rows: [LimitRow]
    public let note: String
    public let showsNote: Bool

    /// A hairline between environments, but not above the first one.
    public let showsSeparator: Bool

    /// The plan chip beside the name; hidden when the plan is not known.
    public let plan: String
    public let showsPlan: Bool

    /// "connected" or "sign in needed", with the dot.
    public let state: String
    public let stateDot: StateDot
    public let showsState: Bool

    /// "working now" is the one state worth the eye: the words go bright and the dot gets a halo.
    public let stateIsBright: Bool
    public let showsHalo: Bool

    /// The account is known and not signed in: the open header offers "Sign in".
    public let needsSignIn: Bool

    /// "updated 12:04", or "as of 9:30": how fresh the numbers of this environment are, for the
    /// line under the open header.
    public let updated: String

    public static func from(
        _ card: CardState,
        first: Bool,
        noAccess: Bool,
        account: CardAccount? = nil,
        working: Bool = false,
        timeZone: TimeZone = .current
    ) -> EnvironmentBlock {
        let rows = card.rows.map(LimitRow.from)
        let signedIn = account?.signedIn == true
        let busy = working && signedIn

        // Somebody who said "not now" in the wizard is told that, not told to open a terminal they
        // have already opened. The advice has to match their situation.
        let note: String
        if account != nil && !signedIn {
            note = Strings.cardSignInNote
        } else {
            note = noAccess ? CardText.noAccessNote : CardText.noDataNote
        }

        return EnvironmentBlock(
            name: card.environment,
            rows: rows,
            note: note,
            showsNote: rows.isEmpty,
            showsSeparator: !first,
            plan: account?.plan ?? "",
            showsPlan: !(account?.plan ?? "").isEmpty,
            state: busy ? Strings.stateWorkingNow
                : signedIn ? Strings.stateConnected
                : Strings.stateSignInNeeded,
            stateDot: signedIn ? .filled : .hollow,
            showsState: account != nil,
            stateIsBright: busy,
            showsHalo: busy,
            needsSignIn: account.map { !$0.signedIn } ?? false,
            updated: CardText.updated(card.freshness, card.updatedAt, timeZone: timeZone))
    }
}

/// What the card says about the account of one environment. Read from .claude.json and from
/// whether the credentials file is there; the credentials file itself is never opened.
public struct CardAccount: Sendable, Equatable {
    public let plan: String
    public let signedIn: Bool

    public init(plan: String, signedIn: Bool) {
        self.plan = plan
        self.signedIn = signedIn
    }
}

public struct LimitRow: Sendable, Equatable {
    public let name: String

    /// The name for a narrow place: "5 h", "wk", or the model name alone.
    public let shortName: String

    public let percent: String
    public let resets: String

    /// The same time without the words around it: "1h 6m" for "resets in 1h 6m".
    public let countdown: String

    public let pace: String
    public let tone: LimitTone

    /// The word beside the bar takes the colour of the bar when it is the tone, and stays quiet
    /// when it is only a guess about the pace.
    public let paceTakesTone: Bool

    /// How much of the bar is filled, from 0 to 1.
    public let fill: Double

    public static func from(_ row: CardRow) -> LimitRow {
        // One slot, two things that could go in it. The tone is a fact and the pace is a guess,
        // so when a limit is running low the fact wins. The guess still shows in the ordinary
        // case, which is where it is worth acting on.
        let toneWord = CardText.tone(row.tone)

        return LimitRow(
            name: row.label ?? CardText.windowName(row.kind, row.modelName),
            shortName: row.label ?? CardText.shortWindowName(row.kind, row.modelName),
            percent: CardText.percent(row.percent),
            resets: CardText.resets(row.countdown),
            countdown: CardText.countdown(row.countdown),
            pace: toneWord.isEmpty ? CardText.pace(row.pace) : toneWord,
            tone: row.tone,
            paceTakesTone: !toneWord.isEmpty,
            fill: Double(row.percent) / 100)
    }
}
