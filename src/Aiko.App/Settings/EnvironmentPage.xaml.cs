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

    public event Action? FoldersRequested;

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
        var signedIn = File.Exists(ClaudeInstall.CredentialsPathIn(_folder));
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
        FillBound(environment);
        RestToo.Visibility = isDefault && two ? Visibility.Visible : Visibility.Collapsed;
        EditFolders.Visibility = two ? Visibility.Visible : Visibility.Collapsed;

        var removable = EnvironmentEdits.CanRemove(settings, environment.Name, Home);
        KeepClaudeNote.Visibility = removable ? Visibility.Collapsed : Visibility.Visible;
        RemoveButton.Visibility = removable && !ConfirmPanel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        ConfirmLine.Text = string.Format(Strings.RemoveEnvironmentLine, environment.Command, environment.Name);

        _filling = false;
    }

    private void FillBound(AikoEnvironment environment)
    {
        BoundList.Children.Clear();
        foreach (var folder in environment.ProjectFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            BoundList.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = Tokens.Brush("HoverLayer"),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4),
                ToolTip = folder,
                Child = new TextBlock { Text = folder, Style = (Style)FindResource("RowNote"), Foreground = Tokens.Brush("Ink") },
            });
        }

        if (environment.ProjectFolders.Count == 0)
        {
            BoundList.Children.Add(new TextBlock { Text = Strings.NoneBound, Style = (Style)FindResource("RowHint") });
        }
    }

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
        if (!File.Exists(ClaudeInstall.CredentialsPathIn(_folder)))
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

    private void OnEditFolders(object sender, RoutedEventArgs e) => FoldersRequested?.Invoke();

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
