using System.Text.Json;

namespace Aiko.Core.Tests;

/// The words of the limits, the card model and the tooltip of the icon. They moved into the core
/// from the app (D-422), where no test could reach them; the Mac has had them in AikoKit with the
/// same cases. The table rows are in spec/cases/card-words and the Swift tests read them too.
[Collection(StringsCollection.Name)]
public class CardWordsTests
{
    private static readonly DateTimeOffset Zero = DateTimeOffset.FromUnixTimeSeconds(0);
    private static readonly JsonElement Cases = SpecCases.Json("card-words");

    [Theory]
    [InlineData(StringLanguage.English, "en")]
    [InlineData(StringLanguage.Russian, "ru")]
    public void Every_word_of_the_table_is_said_the_same(StringLanguage language, string column)
    {
        using var _ = Speak(language);

        foreach (var row in Cases.GetProperty("window").EnumerateArray())
        {
            Assert.Equal(row.GetProperty(column).GetString(), CardText.WindowName(Enum.Parse<LimitKind>(row.GetProperty("kind").GetString()!), row.GetProperty("model").GetString()));
        }

        foreach (var row in Cases.GetProperty("short").EnumerateArray())
        {
            Assert.Equal(row.GetProperty(column).GetString(), CardText.ShortWindowName(Enum.Parse<LimitKind>(row.GetProperty("kind").GetString()!), row.GetProperty("model").GetString()));
        }

        foreach (var row in Cases.GetProperty("countdown").EnumerateArray())
        {
            var countdown = ResetCountdown.Between(Zero, Zero.AddSeconds(row.GetProperty("resetsIn").GetInt32()));
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Countdown(countdown));
        }

        foreach (var row in Cases.GetProperty("percent").EnumerateArray())
        {
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Percent(row.GetProperty("percent").GetInt32()));
        }

        foreach (var row in Cases.GetProperty("tone").EnumerateArray())
        {
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Tone(Enum.Parse<LimitTone>(row.GetProperty("tone").GetString()!)));
        }

        foreach (var row in Cases.GetProperty("resets").EnumerateArray())
        {
            var countdown = ResetCountdown.Between(Zero, Zero.AddSeconds(row.GetProperty("resetsIn").GetInt32()));
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Resets(countdown));
        }

        foreach (var row in Cases.GetProperty("pace").EnumerateArray())
        {
            var pace = new PaceEstimate(Enum.Parse<PaceVerdict>(row.GetProperty("verdict").GetString()!), TimeSpan.FromSeconds(row.GetProperty("timeLeft").GetInt32()));
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Pace(pace));
        }

        foreach (var row in Cases.GetProperty("updated").EnumerateArray())
        {
            var at = row.GetProperty("at").ValueKind == JsonValueKind.Null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(row.GetProperty("at").GetInt64());
            Assert.Equal(row.GetProperty(column).GetString(), CardText.Updated(Enum.Parse<DataFreshness>(row.GetProperty("freshness").GetString()!), at, TimeZoneInfo.Utc));
        }
    }

    [Fact]
    public void The_clock_is_the_one_of_the_machine_not_of_the_offset_the_time_came_with()
    {
        var at = new DateTimeOffset(2026, 9, 11, 9, 5, 0, TimeSpan.Zero);
        var plusThree = TimeZoneInfo.CreateCustomTimeZone("Plus three", TimeSpan.FromHours(3), "Plus three", "Plus three");

        Assert.Equal("09:05", CardText.Clock(at, TimeZoneInfo.Utc));
        Assert.Equal("12:05", CardText.Clock(at, plusThree));
        Assert.Equal("", CardText.Clock(null, TimeZoneInfo.Utc));
    }

    [Fact]
    public void The_header_has_a_short_name_and_the_time_without_words()
    {
        using var _ = Speak(StringLanguage.English);
        var countdown = ResetCountdown.Between(Zero, Zero.AddSeconds(3960));

        Assert.Equal("1h 6m", CardText.Countdown(countdown));
        Assert.Equal("just reset", CardText.Countdown(ResetCountdown.Reset));
        Assert.Equal("Fable", CardText.ShortWindowName(LimitKind.ModelWeek, "Fable"));
    }

    [Fact]
    public void The_tooltip_names_every_environment_with_its_percent()
    {
        using var _ = Speak(StringLanguage.English);

        Assert.Equal("Personal 42% · Work 82%", TrayText.Tooltip([Card("Personal", 42), Card("Work", 82)], zone: TimeZoneInfo.Utc));
        Assert.Equal("Aiko", TrayText.Tooltip([], zone: TimeZoneInfo.Utc));
        Assert.Equal("Personal 42% · Aiko 0.4.0 is out", TrayText.Tooltip([Card("Personal", 42)], "0.4.0", TimeZoneInfo.Utc));
    }

    [Fact]
    public void A_long_tooltip_is_cut_where_Windows_cuts_it()
    {
        using var _ = Speak(StringLanguage.English);
        var cards = Enumerable.Range(0, 20).Select(i => Card($"Environment {i}", i)).ToList();

        Assert.Equal(TrayText.Room, TrayText.Tooltip(cards, zone: TimeZoneInfo.Utc).Length);
    }

    [Fact]
    public void A_block_says_the_state_of_the_account_and_offers_what_fits_it()
    {
        using var _ = Speak(StringLanguage.English);
        var card = Card("Work", 42);

        var working = EnvironmentBlock.From(card, first: false, noAccess: false, new CardAccount("Max 5x", SignedIn: true), working: true);
        var signedOut = EnvironmentBlock.From(card, first: true, noAccess: false, new CardAccount("", SignedIn: false), working: true);

        Assert.Equal(("Max 5x", true, "working now", StateDot.Filled, true, false, true), (working.Plan, working.ShowsPlan, working.State, working.StateDot, working.ShowsHalo, working.NeedsSignIn, working.ShowsSeparator));
        Assert.Equal((false, StateDot.Hollow, false, true, false), (signedOut.ShowsPlan, signedOut.StateDot, signedOut.ShowsHalo, signedOut.NeedsSignIn, signedOut.ShowsSeparator));
    }

    [Fact]
    public void When_a_limit_runs_low_its_word_takes_the_place_of_the_pace()
    {
        using var _ = Speak(StringLanguage.English);
        var low = new CardRow(LimitKind.FiveHour, 85, LimitTone.Caution, false, ResetCountdown.Between(Zero, Zero.AddSeconds(3960)), new PaceEstimate(PaceVerdict.LastsPastReset, TimeSpan.Zero));
        var calm = low with { Tone = LimitTone.Normal };

        Assert.Equal(("running low", true, 85), (LimitRow.From(low).Pace, LimitRow.From(low).PaceTakesTone, LimitRow.From(low).Fill));
        Assert.Equal(("lasts until reset", false), (LimitRow.From(calm).Pace, LimitRow.From(calm).PaceTakesTone));
    }

    private static CardState Card(string environment, int percent) =>
        new(environment, DataFreshness.Live, Zero, [new CardRow(LimitKind.FiveHour, percent, LimitTone.Normal, false, ResetCountdown.Reset, new PaceEstimate(PaceVerdict.Unknown, TimeSpan.Zero))]);

    /// The language is one value for the whole process, so these tests run one at a time.
    private static IDisposable Speak(StringLanguage language)
    {
        var before = Strings.Language;
        Strings.Language = language;
        return new Restore(() => Strings.Language = before);
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StringsCollection
{
    public const string Name = "The language of the words";
}
