using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// The card, ready to be drawn: words already chosen, colours already picked. Built fresh every
/// time the numbers change, because the card is created on demand and closed, never kept around.
public sealed class CardModel
{
    public required string Updated { get; init; }
    public required IReadOnlyList<EnvironmentBlock> Blocks { get; init; }

    public static CardModel From(
        IReadOnlyList<CardState> cards,
        DateTimeOffset now,
        IReadOnlySet<string>? noAccess = null,
        IReadOnlyDictionary<string, CardAccount>? accounts = null)
    {
        var newest = cards
            .Where(card => card.UpdatedAt is not null)
            .OrderByDescending(card => card.UpdatedAt)
            .FirstOrDefault();

        var blocks = new List<EnvironmentBlock>(cards.Count);
        for (var i = 0; i < cards.Count; i++)
        {
            blocks.Add(EnvironmentBlock.From(
                cards[i],
                first: i == 0,
                noAccess: noAccess?.Contains(cards[i].Environment) == true,
                account: accounts?.GetValueOrDefault(cards[i].Environment),
                working: cards[i].IsWorkingAt(now)));
        }

        return new CardModel
        {
            Updated = CardText.Updated(newest?.Freshness ?? DataFreshness.None, newest?.UpdatedAt),
            Blocks = blocks,
        };
    }
}

public sealed class EnvironmentBlock
{
    public required string Name { get; init; }
    public required IReadOnlyList<LimitRow> Rows { get; init; }
    public required string Note { get; init; }
    public required Visibility NoteVisibility { get; init; }

    /// A hairline between environments, but not above the first one.
    public required Visibility SeparatorVisibility { get; init; }

    /// The plan chip beside the name; hidden when the plan is not known.
    public required string Plan { get; init; }
    public required Visibility PlanVisibility { get; init; }

    /// "connected" or "sign in needed", with a dot: filled when connected, an empty ring when not.
    public required string State { get; init; }
    public required Brush StateFill { get; init; }
    public required Brush StateRing { get; init; }
    public required Visibility StateVisibility { get; init; }

    /// "working now" is the one state worth the eye: the words go bright and the dot gets a halo.
    public required Brush StateText { get; init; }
    public required Visibility HaloVisibility { get; init; }

    /// "Open Claude Code", or "Sign in" when the account is not connected. Both open Claude Code
    /// for this environment; signing in happens there.
    public required string OpenLabel { get; init; }
    public required Visibility OpenVisibility { get; init; }

    public static EnvironmentBlock From(CardState card, bool first, bool noAccess, CardAccount? account = null, bool working = false)
    {
        var rows = card.Rows.Select(LimitRow.From).ToList();
        var signedIn = account?.SignedIn == true;
        working &= signedIn;
        return new EnvironmentBlock
        {
            Name = card.Environment,
            Plan = account?.Plan ?? string.Empty,
            PlanVisibility = string.IsNullOrEmpty(account?.Plan) ? Visibility.Collapsed : Visibility.Visible,
            State = working ? Strings.StateWorkingNow : signedIn ? Strings.StateConnected : Strings.StateSignInNeeded,
            StateText = Tokens.Brush(working ? "Ink" : "Muted"),
            HaloVisibility = working ? Visibility.Visible : Visibility.Collapsed,
            StateFill = signedIn ? Tokens.Brush("Positive") : Brushes.Transparent,
            StateRing = signedIn ? Brushes.Transparent : Tokens.Brush("Muted"),
            StateVisibility = account is null ? Visibility.Collapsed : Visibility.Visible,
            OpenLabel = signedIn ? Strings.OpenClaudeCode : Strings.SignIn,
            OpenVisibility = account is null ? Visibility.Collapsed : Visibility.Visible,
            Rows = rows,
            // Somebody who said "not now" in the wizard is told that, not told to open a
            // terminal they have already opened. The advice has to match their situation.
            Note = account is { SignedIn: false } ? Strings.CardSignInNote
                : noAccess ? CardText.NoAccessNote
                : CardText.NoDataNote,
            NoteVisibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
            SeparatorVisibility = first ? Visibility.Collapsed : Visibility.Visible,
        };
    }
}

/// What the card says about the account of one environment. Read from .claude.json and from
/// whether the credentials file is there; the credentials file itself is never opened.
public sealed record CardAccount(string Plan, bool SignedIn);

public sealed class LimitRow
{
    public required string Name { get; init; }
    public required string Percent { get; init; }
    public required string Resets { get; init; }
    public required string Pace { get; init; }
    public required Brush Tone { get; init; }

    /// The word beside the bar takes the colour of the bar when it is the tone, and stays quiet
    /// when it is only a guess about the pace.
    public required Brush PaceTone { get; init; }

    /// The bar is two columns sharing the width, so the fill follows the percentage without any
    /// measuring in code.
    public required GridLength Fill { get; init; }
    public required GridLength Rest { get; init; }

    public static LimitRow From(CardRow row) => new()
    {
        Name = CardText.WindowName(row.Kind, row.ModelName),
        Percent = CardText.Percent(row.Percent),
        Resets = CardText.Resets(row.Countdown),

        // One slot, two things that could go in it. The tone is a fact and the pace is a guess,
        // so when a limit is running low the fact wins. The guess still shows in the ordinary
        // case, which is where it is worth acting on.
        Pace = CardText.Tone(row.Tone) is { Length: > 0 } word ? word : CardText.Pace(row.Pace),
        PaceTone = CardText.Tone(row.Tone).Length > 0 ? Tokens.ToneBrush(row.Tone) : Tokens.Brush("Muted"),
        Tone = Tokens.ToneBrush(row.Tone),
        Fill = new GridLength(row.Percent, GridUnitType.Star),
        Rest = new GridLength(100 - row.Percent, GridUnitType.Star),
    };
}

/// The one place that reads the palette. Loaded straight from the dictionary, so a colour is
/// never written down twice.
public static class Tokens
{
    private static readonly ResourceDictionary Dictionary = new()
    {
        Source = new Uri("pack://application:,,,/Theme/Tokens.xaml", UriKind.Absolute),
    };

    public static Brush Brush(string key) => (Brush)Dictionary[key];

    /// Any token, for controls built in code: they are drawn before they sit in a window, and
    /// FindResource finds nothing there.
    public static T Get<T>(string key) => (T)Dictionary[key];

    public static Brush ToneBrush(LimitTone tone) => tone switch
    {
        LimitTone.Normal => Brush("Positive"),
        LimitTone.Caution => Brush("Caution"),
        LimitTone.Critical => Brush("Destructive"),
        _ => Brush("Muted"),
    };
}
