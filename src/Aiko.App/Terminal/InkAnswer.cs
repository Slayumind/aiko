using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// An answer while Claude writes it, with its words showing like ink (D-332): each new word comes
/// up in grey and dries to the colour of the text in 360 ms, the words one after another at an
/// even pace, not in the lumps the stream brings.
///
/// The pieces of the answer that are done — paragraphs before the last one, code, tables — are
/// built once and left alone. Only the last piece is built again on every lump. A word that was
/// still drying then goes on from where it was, so the ink does not start over on each lump.
/// With animations off in Windows the words are there at once.
sealed partial class InkAnswer : StackPanel
{
    private static readonly TimeSpan Dry = TimeSpan.FromMilliseconds(360);

    /// The new words of one lump are given out within this time, and no slower than one a step.
    private static readonly TimeSpan Spread = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan LongestStep = TimeSpan.FromMilliseconds(60);

    /// When each word of the last piece began to show, in the order of the words.
    private readonly List<DateTime> _starts = [];
    private int _settled;
    private int _liveAt = -1;
    private UIElement? _live;

    public InkAnswer(int id, string markdown)
    {
        Id = id;
        Margin = new Thickness(0, 6, 0, 10);
        Update(markdown);
    }

    /// The answer of the feed this view draws.
    public int Id { get; }

    public void Update(string markdown)
    {
        var segments = AnswerSegments.Of(Markdown.Parse(markdown));
        if (segments.Count == 0)
        {
            return;
        }

        // Every piece but the last is done: it is drawn once, in its place.
        while (_settled < segments.Count - 1)
        {
            if (_live is not null && _liveAt == _settled)
            {
                Children.Remove(_live);
                _live = null;
            }

            Children.Insert(_settled, AnswerView.Segment(segments[_settled]));
            _settled++;
        }

        if (_liveAt != segments.Count - 1)
        {
            _liveAt = segments.Count - 1;
            _starts.Clear();
        }

        if (_live is not null)
        {
            Children.Remove(_live);
        }

        _live = segments[^1] is AnswerSegment.Prose prose && Motion.IsOn
            ? Inked(AnswerView.ProseBox(prose.Blocks))
            : AnswerView.Segment(segments[^1]);
        Children.Add(_live);
    }

    // ---- the ink ----

    private UIElement Inked(RichTextBox box)
    {
        var runs = Runs(box.Document.Blocks).ToList();
        var words = runs.Sum(run => Words(run.Text).Count);
        Schedule(words);

        var now = DateTime.UtcNow;
        var at = 0;
        foreach (var run in runs)
        {
            var parts = Words(run.Text);
            var wet = parts.Select((_, index) => now - _starts[at + index] < Dry).ToList();
            if (wet.Any(isWet => isWet))
            {
                Split(run, parts, wet, at, now);
            }

            at += parts.Count;
        }

        return box;
    }

    /// Gives a start to the words that have none yet: one after another from now, the whole lump
    /// within Spread, and no slower than one word a LongestStep.
    private void Schedule(int words)
    {
        var fresh = words - _starts.Count;
        if (fresh <= 0)
        {
            return;
        }

        var step = TimeSpan.FromTicks(Math.Min(LongestStep.Ticks, Spread.Ticks / fresh));
        var next = _starts.Count > 0 && _starts[^1] > DateTime.UtcNow ? _starts[^1] + step : DateTime.UtcNow;
        for (var index = 0; index < fresh; index++)
        {
            _starts.Add(next);
            next += step;
        }
    }

    /// The run is cut into one run per word; each wet word gets its own brush that dries from
    /// grey to the colour of the run, picking up where it was.
    private void Split(Run run, IReadOnlyList<string> parts, IReadOnlyList<bool> wet, int first, DateTime now)
    {
        var target = run.Foreground is SolidColorBrush solid ? solid.Color : Tokens.Get<Color>("InkColor");
        var grey = Tokens.Get<Color>("MutedColor");
        var inlines = run.SiblingInlines;
        Inline after = run;
        for (var index = 0; index < parts.Count; index++)
        {
            var word = new Run(parts[index])
            {
                FontWeight = run.FontWeight,
                FontStyle = run.FontStyle,
                FontFamily = run.FontFamily,
                FontSize = run.FontSize,
                Background = run.Background,
                Tag = run.Tag,
            };
            if (wet[index])
            {
                var brush = new SolidColorBrush(grey) { Opacity = 0 };
                var begin = _starts[first + index] - now;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(grey, target, new Duration(Dry)) { BeginTime = begin, EasingFunction = Motion.Standard });
                brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromTicks(Dry.Ticks * 6 / 10))) { BeginTime = begin });
                word.Foreground = brush;
            }
            else
            {
                word.Foreground = run.Foreground;
            }

            inlines.InsertAfter(after, word);
            after = word;
        }

        inlines.Remove(run);
    }

    /// A word and the space after it, so the spaces travel with the words.
    private static List<string> Words(string text) =>
        [.. Word().Matches(text).Select(match => match.Value)];

    [GeneratedRegex(@"\s*\S+\s*|\s+")]
    private static partial Regex Word();

    private static IEnumerable<Run> Runs(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            var inner = block switch
            {
                Paragraph paragraph => Runs(paragraph.Inlines),
                List list => list.ListItems.SelectMany(item => Runs(item.Blocks)),
                Section section => Runs(section.Blocks),
                _ => [],
            };
            foreach (var run in inner)
            {
                yield return run;
            }
        }
    }

    private static IEnumerable<Run> Runs(InlineCollection inlines)
    {
        foreach (var inline in inlines.ToList())
        {
            switch (inline)
            {
                case Run run:
                    yield return run;
                    break;
                case Span span:
                    foreach (var inner in Runs(span.Inlines))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }
}
