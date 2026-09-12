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
        IReadOnlySet<string>? noAccess = null)
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
                noAccess: noAccess?.Contains(cards[i].Environment) == true));
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
    public required string Subtitle { get; init; }
    public required IReadOnlyList<LimitRow> Rows { get; init; }
    public required string Note { get; init; }
    public required Visibility NoteVisibility { get; init; }

    /// A hairline between environments, but not above the first one.
    public required Visibility SeparatorVisibility { get; init; }

    public static EnvironmentBlock From(CardState card, bool first, bool noAccess)
    {
        var rows = card.Rows.Select(LimitRow.From).ToList();
        return new EnvironmentBlock
        {
            Name = card.Environment,

            // The header already says when the numbers arrived. A second word for the same thing,
            // in a different place and a different wording, was one of five ways this card had of
            // saying "nothing here".
            Subtitle = string.Empty,
            Rows = rows,
            // Somebody who said "not now" in the wizard is told that, not told to open a
            // terminal they have already opened. The advice has to match their situation.
            Note = noAccess ? CardText.NoAccessNote : CardText.NoDataNote,
            NoteVisibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
            SeparatorVisibility = first ? Visibility.Collapsed : Visibility.Visible,
        };
    }
}

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
        Name = CardText.WindowName(row.Kind),
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

    public static Brush ToneBrush(LimitTone tone) => tone switch
    {
        LimitTone.Normal => Brush("Positive"),
        LimitTone.Caution => Brush("Caution"),
        LimitTone.Critical => Brush("Destructive"),
        _ => Brush("Muted"),
    };
}
