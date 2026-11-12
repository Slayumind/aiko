using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

public partial class EnvironmentPage : UserControl
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly EnvironmentsEditor _editor;

    /// The page follows the account folder, not the name: the name is what changes on this page.
    private readonly string _folder;

    private string? _suggestion;
    private bool _filling;
    private Waiter? _signIn;

    public EnvironmentPage(EnvironmentsEditor editor, string folder)
    {
        InitializeComponent();
        _editor = editor;
        _folder = folder;
        _editor.Changed += Fill;
        Fill();
    }

    /// Opens the checklist at the item that sets up what this page is missing.
    public event Action<ChecklistItem>? ChecklistRequested;

    /// A folder that came here from another environment, and the name of that one: the row says so
    /// until the page is left.
    private (string Folder, string From)? _moved;

    /// Raised after the environment is gone, with the words to show on the page that comes next.
    public event Action<string>? Removed;

    /// Called when the page is left or the window closes. A name typed and not yet confirmed is
    /// applied, the way leaving the field applies it.
    public void Leave()
    {
        CommitName();
        CommitCommand();
        _editor.Changed -= Fill;
        _signIn?.Dispose();
    }

    public void ShowNote(string text)
    {
        NoteText.Text = text;
        NotePanel.IsOpen = true;
    }

    private AikoEnvironment? Current => _editor.Current.Environments.FirstOrDefault(e => e.Holds(_folder));

    private void Fill()
    {
        if (Current is not { } environment)
        {
            return;
        }

        _filling = true;

        var settings = _editor.Current;
        var account = ClaudeAccounts.Read(_folder);
        var signedIn = File.Exists(ClaudeInstall.CredentialsPathIn(ThisComputer.Platform, _folder));
        var two = settings.Environments.Count > 1;
        var isDefault = settings.Default(Home)?.Name == environment.Name;

        Title.Text = environment.Name;
        TitlePlan.Text = account.PlanLabel;
        TitleChip.Visibility = account.PlanLabel.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        DefaultMark.Visibility = isDefault && two ? Visibility.Visible : Visibility.Collapsed;

        Email.Text = account.Email ?? "—";
        PlanText.Text = account.PlanLabel.Length > 0 ? account.PlanLabel : "—";
        StateDot.Fill = signedIn ? Tokens.Brush("Positive") : Brushes.Transparent;
        StateDot.Stroke = signedIn ? null : Tokens.Brush("Muted");
        StateDot.StrokeThickness = 1.5;
        StateText.Text = signedIn ? Strings.StateConnected : Strings.StateSignInNeeded;
        FolderLine.Text = ShortPath(_folder);
        SignInButton.Content = signedIn ? Strings.SignInAgain : Strings.SignIn;

        DirectToggle.IsChecked = environment.DirectMode;
        DirectWhat.Text = account.Plan is ClaudePlan.Team or ClaudePlan.Enterprise
            ? Strings.DirectModeWhat + " " + Strings.DirectModeAsk
            : Strings.DirectModeWhat;

        // A field somebody is typing in keeps what they typed.
        if (!NameBox.IsKeyboardFocusWithin)
        {
            NameBox.Text = environment.Name;
        }

        if (!CommandBox.IsKeyboardFocusWithin)
        {
            CommandBox.Text = environment.Command;
        }

        ShowCommandNote(CommandProblem.None);
        FillBound(environment, two);
        NeedCommandsPanel.IsOpen = two && !CommandFolder.IsSetUp;
        AddFolderButton.Visibility = two ? Visibility.Visible : Visibility.Collapsed;
        DefaultRow.Visibility = two ? Visibility.Visible : Visibility.Collapsed;
        DefaultToggle.IsChecked = isDefault;
        OneEnvironmentNote.Text = string.Format(Strings.FoldersOneEnvironment, environment.Name);
        OneEnvironmentNote.Visibility = two ? Visibility.Collapsed : Visibility.Visible;

        var removable = EnvironmentEdits.CanRemove(settings, environment.Name, Home);
        KeepClaudeNote.Visibility = removable ? Visibility.Collapsed : Visibility.Visible;
        RemoveButton.Visibility = removable && !ConfirmPanel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        ConfirmLine.Text = string.Format(Strings.RemoveEnvironmentLine, environment.Command, environment.Name);

        _filling = false;
    }

    private void FillBound(AikoEnvironment environment, bool two)
    {
        BoundList.Children.Clear();
        if (!two)
        {
            return;
        }

        foreach (var folder in environment.ProjectFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            BoundList.Children.Add(FolderRow(folder));
        }

        if (environment.ProjectFolders.Count == 0)
        {
            BoundList.Children.Add(new TextBlock { Text = Strings.NoneBound, Style = (Style)FindResource("RowHint"), Margin = new Thickness(0, 0, 0, 6) });
        }
    }

    /// One bound folder: its path and a cross that unbinds it; under a folder that just came from
    /// another environment, a line that says from which.
    private Border FolderRow(string folder)
    {
        var path = new TextBlock
        {
            Text = folder,
            Style = (Style)FindResource("RowNote"),
            Foreground = Tokens.Brush("Ink"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = folder,
        };

        var remove = new Button
        {
            Style = (Style)FindResource("IconButton"),
            ToolTip = Strings.RemoveBinding,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Content = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,0 L 8,8 M 8,0 L 0,8"),
                Stroke = Tokens.Brush("Muted"),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 8,
                Height = 8,
            },
        };
        remove.Click += (_, _) => _editor.Commit(EnvironmentEdits.Unbind(_editor.Current, folder), "unbound a folder");

        var line = new DockPanel();
        DockPanel.SetDock(remove, Dock.Right);
        line.Children.Add(remove);
        line.Children.Add(path);

        var inside = new StackPanel();
        inside.Children.Add(line);
        if (_moved is { } moved && RealClaude.SameFolder(moved.Folder, folder))
        {
            inside.Children.Add(new TextBlock { Text = string.Format(Strings.FolderMovedFrom, moved.From), Style = (Style)FindResource("RowHint"), Margin = new Thickness(0, 2, 0, 0) });
        }

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Tokens.Brush("HoverLayer"),
            Padding = new Thickness(10, 4, 4, 4),
            Margin = new Thickness(0, 0, 0, 4),
            Child = inside,
        };
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.PickProject };
        if (dialog.ShowDialog() == true)
        {
            AddFolder(dialog.FolderName);
        }
    }

    /// Binds the folder here. A folder of another environment moves here, and its row says from
    /// where (D-398). For --try-settings, which cannot click through the folder dialog.
    internal void AddFolder(string folder)
    {
        if (Current is not { } environment || environment.ProjectFolders.Any(f => RealClaude.SameFolder(f, folder)))
        {
            return;
        }

        var from = EnvironmentEdits.Bindings(_editor.Current).FirstOrDefault(b => RealClaude.SameFolder(b.Folder, folder))?.Environment;
        _moved = from is null ? null : (folder, from);
        _editor.Commit(EnvironmentEdits.Bind(_editor.Current, folder, environment.Name), from is null ? "bound a folder" : "moved a folder to another environment");
    }

    /// On: every folder that is not bound runs here. Off: it runs in the other environment.
    private void OnDefaultChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || Current is not { } environment)
        {
            return;
        }

        var target = DefaultToggle.IsChecked == true
            ? environment.Name
            : _editor.Current.Environments.FirstOrDefault(x => x.Name != environment.Name)?.Name;
        if (target is not null)
        {
            _editor.Commit(EnvironmentEdits.SetDefault(_editor.Current, target), "changed the default environment");
        }
    }

    private void OnSetUpCommands(object sender, RoutedEventArgs e) => ChecklistRequested?.Invoke(ChecklistItem.Commands);

    /// The folder the way people read it: ~\.claude-work rather than the whole path.
    private static string ShortPath(string folder) =>
        folder.StartsWith(Home, StringComparison.OrdinalIgnoreCase) ? "~" + folder[Home.Length..] : folder;

    // ---- the account ----

    private void OnSignIn(object sender, RoutedEventArgs e)
    {
        if (!ClaudeLauncher.Open(_folder, Home))
        {
            return;
        }

        OpenedPanel.IsOpen = true;

        // The dot turns green by itself once the sign-in is done in the browser.
        if (!File.Exists(ClaudeInstall.CredentialsPathIn(ThisComputer.Platform, _folder)))
        {
            _signIn?.Dispose();
            _signIn = Waiter.ForSignIn(_folder, Fill);
        }
    }

    private void OnDirectChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || Current is not { } environment)
        {
            return;
        }

        var on = DirectToggle.IsChecked == true;
        _editor.Commit(EnvironmentEdits.SetDirectMode(_editor.Current, environment.Name, on), $"direct mode {(on ? "on" : "off")}");
    }

    // ---- name and command: applied on Enter or when the field is left (D-179) ----

    private void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitName();
            e.Handled = true;
        }
    }

    private void OnNameLeft(object sender, KeyboardFocusChangedEventArgs e) => CommitName();

    private void OnCommandKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitCommand();
            e.Handled = true;
        }
    }

    private void OnCommandLeft(object sender, KeyboardFocusChangedEventArgs e) => CommitCommand();

    private void CommitName()
    {
        if (Current is not { } environment)
        {
            return;
        }

        var wanted = NameBox.Text.Trim();
        var problem = wanted == environment.Name
            ? Core.NameProblem.None
            : EnvironmentEdits.CheckName(_editor.Current, environment.Name, wanted);

        NameProblemText.Text = problem switch
        {
            Core.NameProblem.Empty => Strings.NameEmpty,
            Core.NameProblem.TooLong => Strings.NameTooLong,
            Core.NameProblem.Taken => Strings.NameTaken,
            _ => NameProblemText.Text,
        };
        NameProblemPanel.IsOpen = problem != Core.NameProblem.None;

        if (problem != Core.NameProblem.None || wanted == environment.Name)
        {
            return;
        }

        var renamed = EnvironmentEdits.Rename(_editor.Current, environment.Name, wanted, _editor.CommandsInstalled);
        _suggestion = renamed.SuggestedCommand;

        // The sessions of the terminal know their environment by its name: they go with it. An
        // open terminal window does the same when it follows the settings.
        SettingsStore.SaveTerminal(SettingsStore.LoadTerminal().RenameEnvironment(environment.Name, wanted));
        _editor.Commit(renamed.Settings, "renamed an environment");
        ShowSuggestion();
    }

    private void CommitCommand()
    {
        if (Current is not { } environment)
        {
            return;
        }

        var wanted = CommandBox.Text.Trim();
        if (wanted == environment.Command)
        {
            ShowCommandNote(CommandProblem.None);
            return;
        }

        var problem = EnvironmentEdits.CheckCommand(_editor.Current, environment.Name, wanted);
        ShowCommandNote(problem);
        if (problem != CommandProblem.None)
        {
            return;
        }

        _suggestion = null;
        ShowSuggestion();
        _editor.Commit(EnvironmentEdits.SetCommand(_editor.Current, environment.Name, wanted), "changed a command");
    }

    private void OnTakeSuggestion(object sender, RoutedEventArgs e)
    {
        if (_suggestion is null)
        {
            return;
        }

        CommandBox.Text = _suggestion;
        CommitCommand();
    }

    private void ShowSuggestion()
    {
        SuggestionPanel.IsOpen = _suggestion is not null;
        if (_suggestion is not null)
        {
            SuggestionButton.Content = string.Format(Strings.RenameCommandTo, _suggestion);
        }

        CommandNote.Visibility = _suggestion is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowCommandNote(CommandProblem problem)
    {
        CommandNote.Text = problem switch
        {
            CommandProblem.None => Strings.CmdShells,
            CommandProblem.Empty => Strings.CmdEmpty,
            CommandProblem.TooLong => Strings.CmdTooLong,
            CommandProblem.BadCharacters => Strings.CmdBadCharacters,
            CommandProblem.Reserved => Strings.CmdReserved,
            _ => Strings.CmdTaken,
        };
        CommandNote.Foreground = Tokens.Brush(problem == CommandProblem.None ? "Muted" : "Caution");
    }


    // ---- removing: a second click, and the Recycle Bin only when ticked ----

    private void OnRemoveAsked(object sender, RoutedEventArgs e)
    {
        RemoveButton.Visibility = Visibility.Collapsed;
        ConfirmPanel.IsOpen = true;
    }

    private void OnRemoveCancelled(object sender, RoutedEventArgs e)
    {
        ConfirmPanel.IsOpen = false;
        RecycleBox.IsChecked = false;
        RemoveButton.Visibility = Visibility.Visible;
    }

    private void OnRecycleChanged(object sender, RoutedEventArgs e) => RecycleWhyPanel.IsOpen = RecycleBox.IsChecked == true;

    private void OnRemoveConfirmed(object sender, RoutedEventArgs e)
    {
        if (Current is not { } environment)
        {
            return;
        }

        var name = environment.Name;
        Leave();
        var recycled = _editor.Remove(name, RecycleBox.IsChecked == true);
        Removed?.Invoke(string.Format(recycled ? Strings.EnvironmentRemovedToBin : Strings.EnvironmentRemoved, name));
    }
}
