using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The panel "Tasks" above the field (D-342, direction 3 of the prototype): the to-do list of the
/// agent, the subagents at work and how full the context is. It opens and closes by its height;
/// with nothing to show it is not there at all.
sealed class TasksDock : Border
{
    private const double BoxSize = 12;

    private readonly StackPanel _inside = new();
    private TaskPanel _shown = TaskPanel.Empty;
    private int? _percent;
    private bool _open;
    private bool _atOnce;

    public TasksDock()
    {
        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Margin = new Thickness(0, 8, 0, 0);
        ClipToBounds = true;
        Height = 0;
        Visibility = Visibility.Collapsed;
        Child = new Border { Padding = new Thickness(10, 8, 10, 8), Child = _inside };
    }

    /// AtOnce: another session came into view, and its panel stands as it is, with no motion.
    public void Show(TaskPanel panel, int? percent, bool atOnce)
    {
        if (Same(panel, _shown) && percent == _percent)
        {
            return;
        }

        _atOnce = atOnce;

        _shown = panel;
        _percent = percent;

        if (panel.IsEmpty)
        {
            Close();
            return;
        }

        Draw(panel, percent);
        Open();
    }

    private void Draw(TaskPanel panel, int? percent)
    {
        _inside.Children.Clear();

        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        if (percent is { } used)
        {
            var share = TerminalPanel.Mono(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TerminalContextShare, used), "Muted");
            DockPanel.SetDock(share, Dock.Right);
            head.Children.Add(share);
        }
        head.Children.Add(TerminalPanel.Mono(
            panel.Todos.Count > 0 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TerminalTasksCount, panel.Done, panel.Todos.Count) : Strings.TerminalTasks,
            "Muted"));
        _inside.Children.Add(head);

        foreach (var todo in panel.Todos)
        {
            _inside.Children.Add(TodoRow(todo));
        }

        foreach (var agent in panel.Agents)
        {
            _inside.Children.Add(AgentRow(agent));
        }
    }

    private static FrameworkElement TodoRow(TodoItem todo)
    {
        var words = TerminalPanel.Text(
            todo.State == TodoState.Now && todo.Doing.Length > 0 ? todo.Doing : todo.Text,
            "TextNumber",
            todo.State == TodoState.Now ? "Ink" : "Muted",
            FontWeights.Normal);
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        if (todo.State == TodoState.Done)
        {
            words.TextDecorations = Struck();
        }

        var row = new DockPanel { Margin = new Thickness(0, 1.5, 0, 1.5) };
        var box = Box(todo.State);
        DockPanel.SetDock(box, Dock.Left);
        row.Children.Add(box);
        row.Children.Add(words);
        return row;
    }

    /// Done is a filled box, the line at work has a firm frame, the rest a thin one.
    private static Border Box(TodoState state) => new()
    {
        Width = BoxSize,
        Height = BoxSize,
        CornerRadius = new CornerRadius(3),
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Background = state == TodoState.Done ? Tokens.Brush("Positive") : Brushes.Transparent,
        BorderBrush = state == TodoState.Now ? Tokens.Brush("Ink") : Tokens.Brush("Muted"),
        BorderThickness = new Thickness(state switch
        {
            TodoState.Done => 0,
            TodoState.Now => 1.5,
            _ => 1,
        }),
    };

    private static TextDecorationCollection Struck()
    {
        var line = new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Tokens.Brush("InputLine"), 1), 0,
            TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended);
        var all = new TextDecorationCollection { line };
        all.Freeze();
        return all;
    }

    private static FrameworkElement AgentRow(AgentAtWork agent)
    {
        var line = TerminalPanel.Mono("", "Muted");
        line.Margin = new Thickness(0, 4, 0, 0);
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        line.Inlines.Add(new System.Windows.Documents.Run(Strings.TerminalAgent + "  "));
        line.Inlines.Add(new System.Windows.Documents.Run(agent.Task) { Foreground = Tokens.Brush("Ink") });
        if (agent.Doing.Length > 0)
        {
            line.Inlines.Add(new System.Windows.Documents.Run(" · " + agent.Doing));
        }
        return line;
    }

    // ---- by height, 0.4 s ----

    private void Open()
    {
        Visibility = Visibility.Visible;
        var to = Wanted();
        if (_open && Math.Abs(to - ActualHeight) < 0.5)
        {
            return;
        }

        _open = true;
        Grow(to, done: null);
    }

    private void Close()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        Grow(0, done: () =>
        {
            if (!_open)
            {
                Visibility = Visibility.Collapsed;
            }
        });
    }

    private double Wanted()
    {
        var width = ActualWidth > 0 ? ActualWidth - BorderThickness.Left - BorderThickness.Right : double.PositiveInfinity;
        Child.Measure(new Size(width, double.PositiveInfinity));
        return Child.DesiredSize.Height + BorderThickness.Top + BorderThickness.Bottom;
    }

    /// The animation holds the height only while it runs; at its end the height is set for good,
    /// so nothing keeps ticking (CLAUDE.md, "Aiko is not noticeable to the system").
    private void Grow(double to, Action? done)
    {
        // Another session, or motion turned off: the height is there at once, with no animation
        // that would only set it on the next frame.
        if (_atOnce || !Motion.IsOn)
        {
            BeginAnimation(HeightProperty, null);
            Height = to;
            done?.Invoke();
            return;
        }

        var animation = new DoubleAnimation(ActualHeight, to, Motion.Unfold) { EasingFunction = Motion.Standard };
        animation.Completed += (_, _) =>
        {
            BeginAnimation(HeightProperty, null);
            Height = to;
            done?.Invoke();
        };
        BeginAnimation(HeightProperty, animation);
    }

    private static bool Same(TaskPanel one, TaskPanel other) =>
        one.Todos.SequenceEqual(other.Todos) && one.Agents.SequenceEqual(other.Agents);
}
