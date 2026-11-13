using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;
using static Aiko.App.Widgets;

namespace Aiko.App;

/// The list of sessions: the folders, the rows and what each row says.
sealed partial class TerminalPanel
{
    private void ShowList(TerminalView view)
    {
        // While a name is being changed the list stands still: a new list would take the field
        // away from under the keys. Another environment ends the change first: its list must come.
        if (_renaming is not null)
        {
            if (view.Environment == _renamingIn)
            {
                return;
            }

            _finishRename?.Invoke(true);
        }

        // A row being dragged stands still too: the list comes when it is let go.
        if (_drag.Dragging)
        {
            return;
        }

        _rows.Children.Clear();
        _drag.Clear();
        _renameStarts.Clear();
        _rowNames.Clear();
        foreach (var group in view.Groups)
        {
            _rows.Children.Add(GroupHeader(group));
            foreach (var row in group.Rows)
            {
                _rows.Children.Add(Row(row, group.Folder, row.Id == view.SessionId && view.Main == MainView.Terminal, view.Now));
            }
        }

        // One button under the list: the quick pick of a folder, with its key (D-353).
        var key = Mono(Strings.TerminalNewSessionKey, "Muted");
        key.Margin = new Thickness(12, 0, 0, 0);
        var newSession = Flat(Strings.TerminalNewSession, () => CreateAsked?.Invoke(), ghost: false, plus: true, aside: key);
        newSession.HorizontalAlignment = HorizontalAlignment.Stretch;
        newSession.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var create = new Border { Margin = new Thickness(8), Child = newSession };

        // The list lives in the window or, in a narrow one, under the button with the session name.
        _list.Children.Clear();
        _listPopup.Child = null;
        var holder = new DockPanel { Background = _narrow ? Brushes.Transparent : Tokens.Brush("Surface") };
        DockPanel.SetDock(create, Dock.Bottom);
        holder.Children.Add(create);
        holder.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(8, 4, 8, 0),
            Content = _rows,
        });

        if (_narrow)
        {
            SlideList(0);
            // Drawn anew while open too, so it takes the zoom here and not only on opening.
            _listPopup.Child = new Border
            {
                LayoutTransform = PopupZoom.Of(_listToggle),
                Width = ListWidth + 20,
                MaxHeight = 480,
                Margin = new Thickness(0, 4, 12, 12),
                CornerRadius = new CornerRadius(12),
                BorderBrush = Tokens.Brush("InputLine"),
                BorderThickness = new Thickness(1),
                Background = Tokens.Brush("Surface"),
                Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
                Child = holder,
            };
        }
        else
        {
            _listPopup.IsOpen = false;
            SlideList(_listHidden ? 0 : ListWidth);
            // The list keeps its width while its column slides, so it is cut, not squeezed.
            holder.Width = ListWidth;
            holder.HorizontalAlignment = HorizontalAlignment.Left;
            _list.Children.Add(holder);
        }
    }

    /// The folder of a group, and on hover three quiet buttons: a session of Claude Code and a
    /// command line in this folder, with no question (D-350), and the folder in Explorer (D-408).
    private UIElement GroupHeader(RowGroup group)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0 };
        foreach (var (icon, tip, click) in new (string, string, Action<Button>)[]
        {
            ("M5,0 L5,10 M0,5 L10,5", Strings.TerminalGroupNewSession, _ => GroupCreateAsked?.Invoke(group.Folder, SessionKind.Claude)),
            ("M0,1 L4,5 L0,9 M6,9 L11,9", Strings.TerminalGroupNewShell, _ => GroupCreateAsked?.Invoke(group.Folder, SessionKind.Shell)),
            ("M0.5,1.5 L4,1.5 L5.5,3 L11.5,3 L11.5,9.5 L0.5,9.5 Z", Strings.TerminalGroupOpenFolder, button => OpenFolder(button, group.Folder)),
        })
        {
            var button = Icon(Marks.Line(icon));
            button.ToolTip = tip;
            var face = (FrameworkElement)button.Content;
            face.Width = 22;
            face.Height = 22;
            button.Click += (_, _) => click(button);
            // The keyboard reaches them too: a button in focus is shown.
            button.GotKeyboardFocus += (_, _) => Fade(buttons, 1);
            button.LostKeyboardFocus += (_, _) => Fade(buttons, 0);
            buttons.Children.Add(button);
        }

        var header = new DockPanel { Margin = new Thickness(10, 8, 4, 0), Background = Brushes.Transparent, ToolTip = group.Folder };
        DockPanel.SetDock(buttons, Dock.Right);
        header.Children.Add(buttons);
        var name = Mono(group.Name, "Muted");
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        header.Children.Add(name);
        KeepLit(header, buttons, "group:" + group.Folder, () => !buttons.IsKeyboardFocusWithin);
        return header;
    }

    /// Opens the folder of a group in Explorer. When the folder is gone from the disk, nothing
    /// opens: the button says so in its tooltip until the mouse leaves it.
    private void OpenFolder(Button button, string folder)
    {
        if (FolderOpenAsked?.Invoke(folder) != false)
        {
            return;
        }

        var gone = new ToolTip { Content = Strings.TerminalFolderGone, PlacementTarget = button };
        button.ToolTip = gone;
        gone.IsOpen = true;
        button.MouseLeave += (_, _) =>
        {
            gone.IsOpen = false;
            button.ToolTip = Strings.TerminalGroupOpenFolder;
        };
    }

    /// What the mouse was over in the list when it was drawn last, by a key of its own.
    private string? _lit;

    /// The list is drawn anew on every change of a session, so the row under the mouse is a new
    /// element each time. Starting dark, its hover came in again and the row blinked. So the area
    /// remembers that the mouse is over it, and its new copy starts lit; if the mouse left in the
    /// meantime, the light fades. mayFade says whether the light may go when the mouse leaves.
    private void KeepLit(FrameworkElement area, UIElement light, string key, Func<bool>? mayFade = null)
    {
        if (_lit == key)
        {
            light.Opacity = 1;
            Dispatcher.BeginInvoke(() =>
            {
                if (!area.IsMouseOver && (mayFade?.Invoke() ?? true))
                {
                    Fade(light, 0);
                    if (_lit == key)
                    {
                        _lit = null;
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        area.MouseEnter += (_, _) =>
        {
            _lit = key;
            Fade(light, 1);
        };
        area.MouseLeave += (_, _) =>
        {
            // An area taken out of a list that is drawn anew is not left by the mouse.
            if (VisualTreeHelper.GetParent(area) is null)
            {
                return;
            }

            if (_lit == key)
            {
                _lit = null;
            }

            if (mayFade?.Invoke() ?? true)
            {
                Fade(light, 0);
            }
        };
    }

    private static void Fade(UIElement element, double to) =>
        element.BeginAnimation(OpacityProperty, Motion.To(to, Motion.Or0(Motion.Hover), Motion.Standard));

    /// For the snapshot: the buttons of the first group shown, and the hover of its first button.
    internal void ShowGroupHoverForSnapshot()
    {
        if (_rows.Children.OfType<DockPanel>().FirstOrDefault() is { } header
            && header.Children[0] is StackPanel { Children: [Button first, ..] } buttons)
        {
            buttons.Opacity = 1;
            ((Panel)first.Content).Children.OfType<Border>().First().Opacity = 1;
        }
    }

    private UIElement Row(SessionRow row, string folder, bool selected, DateTimeOffset now)
    {
        var (lines, top, name) = RowLines(row, now, whole: false);
        _renameStarts.Add(() => Rename(row, top, name));
        _rowNames.Add(row.Name);

        // A double click on the name changes it in place (LibreChat, Open WebUI).
        name.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                Rename(row, top, name);
            }
        };

        var hover = new Border { CornerRadius = new CornerRadius(10), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid();
        face.Children.Add(hover);
        face.Children.Add(new Border { Padding = new Thickness(10, 8, 10, 8), Child = lines });

        var back = new Border
        {
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 1, 0, 1),
            Background = selected ? Tokens.Brush("Raised") : Brushes.Transparent,
            BorderBrush = selected ? Tokens.Brush("InputLine") : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = face,
            ContextMenu = RowMenu(row.Id, () => Rename(row, top, name)),
        };
        KeepLit(back, hover, "row:" + row.Id);
        _drag.Watch(back, row.Id, folder, () => RowLines(row, now, whole: true).Lines);
        back.MouseLeftButtonUp += (_, _) =>
        {
            // The click that lets a dragged row go opens nothing.
            if (_drag.Swallows)
            {
                return;
            }

            _listPopup.IsOpen = false;
            RowPicked?.Invoke(row.Id);
        };
        return back;
    }

    /// What a row says: the kind, the name and the state on top, the time or the deed under them.
    /// whole: for the card that is dragged, with the name in full instead of cut to the list.
    private static (StackPanel Lines, DockPanel Top, TextBlock Name) RowLines(SessionRow row, DateTimeOffset now, bool whole)
    {
        var top = new DockPanel();
        var mark = Mark(row.State);
        DockPanel.SetDock(mark, Dock.Right);
        top.Children.Add(mark);

        // What runs in the session (the mockup of D-362): the mark of Claude Code or "$".
        UIElement kind = row.Kind == SessionKind.Shell
            ? Mono("$", "Muted")
            : Marks.Line("M6,1 L6,11 M1,6 L11,6 M2.5,2.5 L9.5,9.5 M9.5,2.5 L2.5,9.5");
        var kindBox = new Border { Width = 14, Margin = new Thickness(0, 0, 8, 0), Child = kind };
        ((FrameworkElement)kind).HorizontalAlignment = HorizontalAlignment.Center;
        DockPanel.SetDock(kindBox, Dock.Left);
        top.Children.Add(kindBox);
        var name = Text(row.Name, "TextName", "Ink", FontWeights.Normal);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        if (whole)
        {
            name.Margin = new Thickness(0, 0, 12, 0);
        }

        top.Children.Add(name);

        var lines = new StackPanel();
        lines.Children.Add(top);
        var second = SecondLine(row, now);
        if (second.Length > 0)
        {
            var line = Text(second, "TextSmall", "Muted", FontWeights.Normal);
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            // Under the name, past the mark of the kind.
            line.Margin = new Thickness(22, 3, 0, 0);
            lines.Children.Add(line);
        }

        return (lines, top, name);
    }

    private ContextMenu RowMenu(string id, Action rename)
    {
        var name = new MenuItem { Header = Strings.TerminalRenameSession };
        name.Click += (_, _) => rename();
        var close = new MenuItem { Header = Strings.TerminalCloseSession };
        close.Click += (_, _) => RowClosed?.Invoke(id);
        // A menu set on an element is not in its tree, so the styles of the panel are handed to it.
        var menu = new ContextMenu { Items = { name, close }, Style = (Style)FindResource(typeof(ContextMenu)) };
        menu.Resources[typeof(MenuItem)] = FindResource(typeof(MenuItem));
        return menu;
    }

    /// The name becomes a field with all of it selected: Enter or leaving the field keeps the new
    /// name, Esc keeps the old one. An empty name changes nothing (TerminalSessions.Rename).
    private void Rename(SessionRow row, DockPanel top, TextBlock name)
    {
        var field = new TextBox { Text = row.Name, MaxLength = 100 };
        Field(field);
        field.Padding = new Thickness(4, 1, 4, 1);
        top.Children.Remove(name);
        top.Children.Add(field);
        _renaming = row.Id;
        _renamingIn = _view?.Environment;
        _renameField = field;

        // The list stands still until the change ends, so every way it can end is caught: Enter,
        // Esc, the field losing the keys, the field leaving the window, a click away from it (a pill
        // or a row takes no keys, so the field would keep them), another environment, and a field
        // that never got the keys at all. Missing one froze the list for good.
        var done = false;
        void Finish(bool keep)
        {
            if (done)
            {
                return;
            }

            done = true;
            _renaming = null;
            _renamingIn = null;
            _renameField = null;
            _finishRename = null;
            if (keep && field.Text.Trim() != row.Name)
            {
                RowRenamed?.Invoke(row.Id, field.Text);
            }
            else if (_view is { } view)
            {
                ShowList(view);
            }
        }

        field.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                Finish(keep: e.Key == Key.Enter);
            }
        };
        field.LostKeyboardFocus += (_, _) => Finish(keep: true);
        field.Unloaded += (_, _) => Finish(keep: true);
        _finishRename = Finish;
        Dispatcher.BeginInvoke(() =>
        {
            if (done)
            {
                return;
            }

            if (!field.Focus())
            {
                Finish(keep: false);
                return;
            }

            field.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    /// The environment, the field and the way to end a change of a name while one goes on.
    private string? _renamingIn;
    private TextBox? _renameField;
    private Action<bool>? _finishRename;

    /// A press anywhere but the field ends a change of a name and keeps the new one.
    private void EndRenameOnPressAway(object sender, MouseButtonEventArgs e)
    {
        if (_renameField is not { } field || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        for (DependencyObject? at = source; at is not null; at = at is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, field))
            {
                return;
            }
        }

        _finishRename?.Invoke(true);
    }

    /// For --try-rename: starts a change of the name of the first row, as a double click would.
    internal bool StartRenameForCheck()
    {
        if (_renameStarts.Count == 0)
        {
            return false;
        }

        _renameStarts[0]();
        return true;
    }

    /// For --try-rename: the names in the list now, in order.
    internal IReadOnlyList<string> RowNamesForCheck => [.. _rowNames];

    /// How each row of the list starts a change of its name, and its name, in the order of the list.
    private readonly List<Action> _renameStarts = [];
    private readonly List<string> _rowNames = [];

    internal bool RenamingForCheck => _renaming is not null;

    /// The pills end before the gear: for --try-rename, which also looks at the title bar.
    internal bool PillsClearOfButtonsForCheck =>
        _pills.Root.TranslatePoint(new Point(_pills.Root.ActualWidth, 0), _bar).X <= _right.TranslatePoint(new Point(0, 0), _bar).X + 0.5;

    /// For --try-rename: the first row of the list, and how lit its hover is now.
    internal FrameworkElement? FirstRowForCheck => _rows.Children.OfType<Border>().FirstOrDefault();

    internal double FirstRowLightForCheck =>
        FirstRowForCheck is Border { Child: Grid { Children: [Border light, ..] } } ? light.Opacity : -1;

    /// For --try-rename: the field of a change of a name has the keys, so leaving it ends the change.
    internal bool RenameFieldFocusedForCheck => _renameField?.IsKeyboardFocused == true;

    private static UIElement Mark(RowState state)
    {
        var (icon, word, tone) = state switch
        {
            RowState.Working => ((UIElement)Dot(filled: true), Strings.TerminalWorking, "Muted"),
            RowState.Waiting => (Marks.Pause(), Strings.TerminalWaiting, "Caution"),
            RowState.Done => (SiteIcons.Draw(SiteIcons.Check, 10, "Positive"), Strings.TerminalDone, "Positive"),
            RowState.Asleep => (Dot(filled: false), Strings.TerminalAsleep, "Muted"),
            RowState.Stopped => (Marks.Line("M0,0 L8,8 M8,0 L0,8", "Destructive"), Strings.TerminalStopped, "Destructive"),
            _ => (new Border(), "", "Muted"),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        if (word.Length > 0)
        {
            row.Children.Add(icon);
            var text = Mono(word, tone);
            text.Margin = new Thickness(6, 0, 0, 0);
            row.Children.Add(text);
        }

        return row;
    }

    /// What the terminal did last, or, for a sleeping session, when it was used.
    private static string SecondLine(SessionRow row, DateTimeOffset now) =>
        row.Line.Length > 0 ? row.Line : UsedWords(row.LastUsed, now);
    internal static string UsedWords(long lastUsed, DateTimeOffset now)
    {
        if (lastUsed <= 0)
        {
            return "";
        }

        var used = DateTimeOffset.FromUnixTimeSeconds(lastUsed).ToOffset(now.Offset);
        var time = used.ToString("t", CultureInfo.CurrentCulture);
        return SessionRows.DayOf(lastUsed, now.ToUnixTimeSeconds(), (int)now.Offset.TotalMinutes) switch
        {
            UsedDay.Today => time,
            UsedDay.Yesterday => string.Format(CultureInfo.CurrentCulture, Strings.TerminalYesterday, time),
            _ => used.ToString("d MMMM", CultureInfo.CurrentCulture),
        };
    }
}
