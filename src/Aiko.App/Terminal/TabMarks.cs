using System.Windows;
using System.Windows.Controls;

namespace Aiko.App;

/// The sessions of an environment that want attention, on its tab: waiting, done and stopped, each
/// a sign with its number. A mark unfolds and folds as the marks of the island do. It is kept between
/// the drawings of the tab, so the motion goes on when the tab is drawn anew.
sealed class TabMarks
{
    private readonly (UnfoldPanel Panel, TextBlock Number) _waiting;
    private readonly (UnfoldPanel Panel, TextBlock Number) _done;
    private readonly (UnfoldPanel Panel, TextBlock Number) _stopped;
    private bool _shown;

    public TabMarks()
    {
        _waiting = Mark(StatusMark.WaitingSign());
        _done = Mark(StatusMark.DoneSign());
        _stopped = Mark(StatusMark.StoppedSign());
    }

    public StackPanel Root { get; } = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    public void Show(int waiting, int done, int stopped)
    {
        Set(_waiting, waiting);
        Set(_done, done);
        Set(_stopped, stopped);
        _shown = true;
    }

    private void Set((UnfoldPanel Panel, TextBlock Number) mark, int count)
    {
        // A mark that folds keeps its number until it is gone.
        if (count > 0)
        {
            mark.Number.Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        if (mark.Panel.IsOpen != count > 0 || !_shown)
        {
            mark.Panel.SetOpen(count > 0, animate: _shown);
        }
    }

    private (UnfoldPanel, TextBlock) Mark(FrameworkElement sign)
    {
        var (row, number) = StatusMark.Counted(sign);
        row.Margin = new Thickness(8, 0, 0, 0);
        var panel = new UnfoldPanel { Child = row, VerticalAlignment = VerticalAlignment.Center };
        Root.Children.Add(panel);
        return (panel, number);
    }
}
