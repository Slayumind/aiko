namespace Aiko.Core;

/// The limits of every environment, ready to be drawn: words already chosen, tones already
/// picked. Built fresh every time the numbers change.
///
/// The twin of CardModel.swift. A view turns a tone into its colour and a "shown" into its own
/// way of hiding a thing: WPF and SwiftUI spell those differently, and a rule that names a brush
/// cannot be tested without a window.
public sealed record CardModel(string Updated, IReadOnlyList<EnvironmentBlock> Blocks)
{
    public static CardModel From(
        IReadOnlyList<CardState> cards,
        DateTimeOffset now,
        IReadOnlySet<string>? noAccess = null,
        IReadOnlyDictionary<string, CardAccount>? accounts = null,
        TimeZoneInfo? zone = null)
    {
        var newest = cards
            .Where(card => card.UpdatedAt is not null)
            .OrderByDescending(card => card.UpdatedAt)
            .FirstOrDefault();

        var blocks = cards.Select((card, i) => EnvironmentBlock.From(
            card,
            first: i == 0,
            noAccess: noAccess?.Contains(card.Environment) == true,
            account: accounts?.GetValueOrDefault(card.Environment),
            working: card.IsWorkingAt(now),
            zone: zone)).ToList();

        return new CardModel(CardText.Updated(newest?.Freshness ?? DataFreshness.None, newest?.UpdatedAt, zone), blocks);
    }
}

/// The dot beside the state word: filled when the account is connected, an empty ring when not.
public enum StateDot
{
    Filled,
    Hollow,
}

public sealed record EnvironmentBlock
{
    public required string Name { get; init; }
    public required IReadOnlyList<LimitRow> Rows { get; init; }
    public required string Note { get; init; }
    public required bool ShowsNote { get; init; }

    /// A hairline between environments, but not above the first one.
    public required bool ShowsSeparator { get; init; }

    /// The plan chip beside the name; hidden when the plan is not known.
    public required string Plan { get; init; }
    public required bool ShowsPlan { get; init; }

    /// "connected" or "sign in needed", with the dot.
    public required string State { get; init; }
    public required StateDot StateDot { get; init; }
    public required bool ShowsState { get; init; }

    /// "working now" is the one state worth the eye: the words go bright and the dot gets a halo.
    public required bool StateIsBright { get; init; }
    public required bool ShowsHalo { get; init; }

    /// "Open Claude Code", or "Sign in" when the account is not connected. Both open Claude Code
    /// for this environment; signing in happens there.
    public required string OpenLabel { get; init; }
    public required bool ShowsOpen { get; init; }

    /// The account is known and not signed in: the open header offers "Sign in". Windows only.
    public required bool NeedsSignIn { get; init; }

    /// "Updated at 12:04", or "Last data at 9:30": how fresh the numbers of this environment are,
    /// for the open header. Windows only.
    public required string Updated { get; init; }

    public static EnvironmentBlock From(
        CardState card,
        bool first,
        bool noAccess,
        CardAccount? account = null,
        bool working = false,
        TimeZoneInfo? zone = null)
    {
        var rows = card.Rows.Select(LimitRow.From).ToList();
        var signedIn = account?.SignedIn == true;
        var busy = working && signedIn;
        return new EnvironmentBlock
        {
            Name = card.Environment,
            Rows = rows,

            // Somebody who said "not now" in the wizard is told that, not told to open a terminal
            // they have already opened. The advice has to match their situation.
            Note = account is { SignedIn: false } ? Strings.CardSignInNote
                : noAccess ? CardText.NoAccessNote
                : CardText.NoDataNote,
            ShowsNote = rows.Count == 0,
            ShowsSeparator = !first,
            Plan = account?.Plan ?? "",
            ShowsPlan = !string.IsNullOrEmpty(account?.Plan),
            State = busy ? Strings.StateWorkingNow : signedIn ? Strings.StateConnected : Strings.StateSignInNeeded,
            StateDot = signedIn ? StateDot.Filled : StateDot.Hollow,
            ShowsState = account is not null,
            StateIsBright = busy,
            ShowsHalo = busy,
            OpenLabel = signedIn ? Strings.OpenClaudeCode : Strings.SignIn,
            ShowsOpen = account is not null,
            NeedsSignIn = account is { SignedIn: false },
            Updated = CardText.Updated(card.Freshness, card.UpdatedAt, zone),
        };
    }
}

/// What the card says about the account of one environment. Read from .claude.json and from
/// whether the credentials file is there; the credentials file itself is never opened.
public sealed record CardAccount(string Plan, bool SignedIn);

public sealed record LimitRow
{
    public required string Name { get; init; }

    /// The name for a narrow place: "5 h", "wk", or the model name alone. Windows only.
    public required string ShortName { get; init; }

    public required string Percent { get; init; }
    public required string Resets { get; init; }

    /// The same time without the words around it: "1h 6m" for "resets in 1h 6m". Windows only.
    public required string Countdown { get; init; }

    public required string Pace { get; init; }
    public required LimitTone Tone { get; init; }

    /// The word beside the bar takes the colour of the bar when it is the tone, and stays quiet
    /// when it is only a guess about the pace.
    public required bool PaceTakesTone { get; init; }

    /// How much of the bar is filled, from 0 to 100.
    public required int Fill { get; init; }

    public static LimitRow From(CardRow row)
    {
        // One slot, two things that could go in it. The tone is a fact and the pace is a guess,
        // so when a limit is running low the fact wins. The guess still shows in the ordinary
        // case, which is where it is worth acting on.
        var toneWord = CardText.Tone(row.Tone);
        return new LimitRow
        {
            Name = CardText.WindowName(row.Kind, row.ModelName),
            ShortName = CardText.ShortWindowName(row.Kind, row.ModelName),
            Percent = CardText.Percent(row.Percent),
            Resets = CardText.Resets(row.Countdown),
            Countdown = CardText.Countdown(row.Countdown),
            Pace = toneWord.Length > 0 ? toneWord : CardText.Pace(row.Pace),
            Tone = row.Tone,
            PaceTakesTone = toneWord.Length > 0,
            Fill = Math.Clamp(row.Percent, 0, 100),
        };
    }
}
