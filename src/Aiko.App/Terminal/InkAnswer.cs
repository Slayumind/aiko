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
/// even pace, not in the lumps the stream brings. The pace follows how fast the words come; when
/// the shown text falls more than a second behind, it hurries. A list marker, the grey ground of
/// inline code and the line of a quote come up with their first word, not before it.
///
/// The pieces of the answer that are done — paragraphs before the last one, code, tables — are
/// built once and left alone. Only the last piece is built again on every lump. A word that was
/// still drying then goes on from where it was, so the ink does not start over on each lump.
/// With animations off in Windows the words are there at once.
sealed partial class InkAnswer : StackPanel
{
    private static readonly TimeSpan Dry = TimeSpan.FromMilliseconds(360);

    /// The time between two words: guessed from how fast the stream brings them, within these.
    private const double FirstStepMs = 55;
    private const double ShortestStepMs = 20;
    private const double LongestStepMs = 90;

    /// Shown text further behind the stream than this hurries to catch up.
    private const double MostBehindMs = 1000;

    /// When each word of the last piece began to show, in the order of the words.
    private readonly List<DateTime> _starts = [];
    private DateTime _lastLump;
    private double _stepMs = FirstStepMs;
    private int _settled;
    private int _liveAt = -1;
    private UIElement? _live;

    public InkAnswer(int id, string markdown)
    {
        Id = id;
        Margin = new Thickness(0, 6, 0, 10);
        Loaded += (_, _) => InkProbe.Start();
        Unloaded += (_, _) => InkProbe.Stop();
        Update(markdown);
    }

    /// The answer of the feed this view draws.
    public int Id { get; }

    /// The answer is written whole. The words still in line dry first; then done builds the
    /// finished answer in place of this view, so the last words do not flash up at once.
    public void Finish(string markdown, Action done)
    {
        Update(markdown);
        var left = _starts.Count > 0 && Motion.IsOn ? _starts[^1] + Dry - DateTime.UtcNow : TimeSpan.Zero;
        if (left <= TimeSpan.Zero)
        {
            done();
            return;
        }

        var wait = new System.Windows.Threading.DispatcherTimer { Interval = left };
        wait.Tick += (_, _) =>
        {
            wait.Stop();
            done();
        };
        wait.Start();
    }

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
        var firstWord = new Dictionary<Run, int>();
        var count = 0;
        foreach (var run in runs)
        {
            firstWord[run] = count;
            count += Words(run.Text).Count;
        }

        Schedule(count);

        // What is drawn around the words comes up with the first word inside it; with no word
        // yet, it waits unseen.
        var now = DateTime.UtcNow;
        foreach (var (frame, first) in Frames(box.Document.Blocks, firstWord))
        {
            if (first < 0 || first >= _starts.Count)
            {
                InkFrame(frame, null);
            }
            else if (now - _starts[first] < Dry)
            {
                InkFrame(frame, _starts[first] - now);
            }
        }

        foreach (var run in runs)
        {
            var parts = Words(run.Text);
            var at = firstWord[run];
            var wet = parts.Select((_, index) => now - _starts[at + index] < Dry).ToList();
            if (wet.Any(isWet => isWet))
            {
                Split(run, parts, wet, at, now);
            }
        }

        return box;
    }

    /// Gives a start to the words that have none yet, one step apart after the last one. The step
    /// follows the stream: the time since the last lump shared among its new words, smoothed so
    /// one slow lump does not stall the text.
    private void Schedule(int words)
    {
        var fresh = words - _starts.Count;
        if (fresh <= 0)
        {
            return;
        }

        InkProbe.Lump(fresh, words);

        var now = DateTime.UtcNow;
        if (_lastLump != default)
        {
            var perWord = (now - _lastLump).TotalMilliseconds / fresh;
            _stepMs = Math.Clamp((_stepMs * 0.6) + (perWord * 0.4), ShortestStepMs, LongestStepMs);
        }

        _lastLump = now;
        var next = _starts.Count > 0 && _starts[^1] > now ? _starts[^1] : now;
        var behind = (next - now).TotalMilliseconds + (fresh * _stepMs);
        var step = behind > MostBehindMs ? Math.Max(ShortestStepMs / 2, _stepMs * MostBehindMs / behind) : _stepMs;
        for (var index = 0; index < fresh; index++)
        {
            next += TimeSpan.FromMilliseconds(index == 0 && _starts.Count == 0 ? 0 : step);
            _starts.Add(next);
        }
    }

    /// The list items and quotes of the document with the first word inside each.
    private static IEnumerable<(TextElement Frame, int First)> Frames(IEnumerable<Block> blocks, IReadOnlyDictionary<Run, int> firstWord)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case List list:
                    foreach (var item in list.ListItems)
                    {
                        yield return (item, First(Runs(item.Blocks), firstWord));
                        foreach (var inner in Frames(item.Blocks, firstWord))
                        {
                            yield return inner;
                        }
                    }

                    break;
                case Section section:
                    yield return (section, First(Runs(section.Blocks), firstWord));
                    foreach (var inner in Frames(section.Blocks, firstWord))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    private static int First(IEnumerable<Run> runs, IReadOnlyDictionary<Run, int> firstWord) =>
        runs.Select(run => firstWord.TryGetValue(run, out var at) ? at : -1).FirstOrDefault(at => at >= 0, -1);

    /// The marker of a list item and the line of a quote come up from nothing with their first
    /// word; begin is null while that word has not come, and they stay unseen.
    private static void InkFrame(TextElement frame, TimeSpan? begin)
    {
        if (frame is Section section && section.BorderBrush is SolidColorBrush line)
        {
            section.BorderBrush = Rising(line.Color, begin);
        }
        else if (frame is ListItem { Blocks.FirstBlock: Paragraph first }
            && first.Inlines.FirstInline is InlineUIContainer { Tag: var tag, Child: UIElement marker }
            && ReferenceEquals(tag, AnswerView.ListMarker))
        {
            marker.Opacity = 0;
            if (begin is { } start)
            {
                marker.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromTicks(Dry.Ticks * 6 / 10))) { BeginTime = start });
            }
        }
    }

    private static SolidColorBrush Rising(Color colour, TimeSpan? begin)
    {
        var brush = new SolidColorBrush(colour) { Opacity = 0 };
        if (begin is { } start)
        {
            brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromTicks(Dry.Ticks * 6 / 10))) { BeginTime = start });
        }

        return brush;
    }

    /// The run is cut into one run per word; each wet word gets its own brush that dries from
    /// grey to the colour of the run, picking up where it was. A word with a ground of its own,
    /// such as inline code, brings its ground up with it.
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
                var begin = _starts[first + index] - now;
                var brush = new SolidColorBrush(grey) { Opacity = 0 };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(grey, target, new Duration(Dry)) { BeginTime = begin, EasingFunction = Motion.Standard });
                brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromTicks(Dry.Ticks * 6 / 10))) { BeginTime = begin });
                word.Foreground = brush;
                if (run.Background is SolidColorBrush ground)
                {
                    word.Background = Rising(ground.Color, begin);
                }
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
