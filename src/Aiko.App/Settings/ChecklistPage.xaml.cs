using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aiko.Core;

namespace Aiko.App;

/// The environment checklist (D-165), a page of the settings window (D-177).
///
/// The page only gathers facts and draws them. Which item is ready, blocked or done, and which one
/// opens next, is decided by WizardChecklist in the core. Nothing outside Aiko's own settings is
/// written before Finish, except the one thing the person asks for right away: signing in, which
/// happens in Claude Code.
public partial class ChecklistPage : UserControl
{
    private const string InstallCommandText = "irm https://claude.ai/install.ps1 | iex";

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string FirstFolder = Path.Combine(Home, ClaudeConfigFolder.DefaultFolderName);

    private readonly List<ChecklistRow> _rows;
    private readonly List<string> _projectFolders = [];
    private readonly List<(PowerShellProfiles.Found Profile, ProfileFunction Function, ToggleButtonHolder Toggle)> _functions = [];
    private readonly Dictionary<int, CommandField> _commandFields = [];
    private readonly EnvironmentSettings _existing = SettingsStore.LoadEnvironments();

    private bool _claudeInstalled;
    private bool _firstSignedIn;
    private string? _secondFolder;
    private bool _secondSignedIn;
    private bool _secondSkipped;
    private bool? _accessGranted;
    private bool? _commandsWanted;
    private bool _foldersVisited;
    private Waiter? _waiter;
    private ChecklistItem? _opened;

    /// The item to open first: the second environment when the person asked to add one, and any item
    /// for a snapshot. Null opens the next item that needs an answer.
    public ChecklistPage(ChecklistItem? openFirst)
    {
        InitializeComponent();

        _rows = [InstallRow, FirstRow, SecondRow, FoldersRow, AccessRow, CommandsRow, PlaceRow];
        foreach (var row in _rows)
        {
            row.HeaderClicked += OnRowClicked;
        }

        InstallCommand.Text = InstallCommandText;
        NewName.Text = "";
        DefaultChoice.Picked += _ => Refresh();

        GatherFacts();
        FillFromExisting();
        FillCandidates();
        Refresh();
        Open(openFirst ?? WizardChecklist.NextToOpen(Facts()));
    }

    /// Raised after Finish wrote the environments, with the words for the page that comes next.
    public event Action<string>? Finished;

    /// Opens one item, for "Add a second environment" and the like.
    public void OpenItem(ChecklistItem item) => Open(item);

    /// Stops waiting for Claude Code or a sign-in. The page itself lives on while the person looks at
    /// other pages of the window, so this is called when the window closes, not when the page hides.
    public void Close() => _waiter?.Dispose();

    // ---- facts ----

    private void GatherFacts()
    {
        _claudeInstalled = ClaudeLauncher.FindClaude() is not null;
        _firstSignedIn = File.Exists(ClaudeInstall.CredentialsPathIn(FirstFolder));

        if (_secondFolder is not null)
        {
            _secondSignedIn = File.Exists(ClaudeInstall.CredentialsPathIn(_secondFolder));
        }

        var account = ClaudeAccounts.Read(FirstFolder);
        FirstAccountLine.Text = string.Join(" · ", new[] { account.Email, account.PlanLabel }.Where(s => !string.IsNullOrEmpty(s)));
        FirstSignedOut.Visibility = _firstSignedIn ? Visibility.Collapsed : Visibility.Visible;
        FirstSignedIn.Visibility = _firstSignedIn ? Visibility.Visible : Visibility.Collapsed;

        FillCandidates();
    }

    private WizardFacts Facts() => new()
    {
        ClaudeInstalled = _claudeInstalled,
        FirstSignedIn = _firstSignedIn,
        SecondSignedIn = _secondFolder is not null && _secondSignedIn,
        SecondSkipped = _secondSkipped,
        AccessGranted = _accessGranted,
        CommandsWanted = _commandsWanted,
        FoldersVisited = _foldersVisited,
        BoundFolders = _projectFolders.Count,
    };

    /// Going through the wizard again starts from what is set up now, not from nothing.
    private void FillFromExisting()
    {
        FirstName.Text = _existing.First(Home)?.Name ?? Strings.EnvironmentPlainName;

        // What is already in place counts as answered, so adding a second environment asks only
        // about the second environment.
        if (_existing.HasEnvironments)
        {
            _accessGranted = _existing.Environments.SelectMany(e => e.ConfigDirectories).All(ClaudeSettingsFile.HasOurLine) ? true : null;
            _commandsWanted = CommandFolder.IsSetUp ? true : null;
        }

        if (_existing.Second(Home) is { } second)
        {
            ChooseSecond(second.ConfigDirectories[0], second.Name);
            _projectFolders.AddRange(second.ProjectFolders);

            // The default list starts at the default in use, not at environment 1.
            DefaultChoice.Items = [FirstNameText(), SecondNameText()];
            DefaultChoice.SelectedIndex = _existing.Default(Home) == second ? 1 : 0;
        }

        var settings = SettingsStore.Load();
        PlaceIsland.IsChecked = settings.Place == AikoPlace.Island;
        PlaceTray.IsChecked = settings.Place != AikoPlace.Island;
        // What Windows holds, as on the general page: Finish writes this switch back to Windows.
        RunAtStartup.IsChecked = _existing.HasEnvironments ? Startup.IsEnabled() : settings.RunAtStartup;
        CheckUpdates.IsChecked = settings.CheckUpdates;

        FillCommandFields();
        FillProfileFunctions();
        FillProjectFolders();
    }

    // ---- the rows ----

    private void Refresh()
    {
        var facts = Facts();
        foreach (var row in _rows)
        {
            row.State = WizardChecklist.StateOf(row.Item, facts);
            row.Status = StatusOf(row.Item, row.State);
        }

        var (done, total) = WizardChecklist.Progress(facts);
        Counter.Text = string.Format(Strings.ChecklistCount, done, total);
        FinishButton.IsEnabled = WizardChecklist.CanFinish(facts);
        DoneView.IsOpen = FinishButton.IsEnabled;
        DoneWhere.Text = PlaceIsland.IsChecked == true ? Strings.WizardDoneIsland : Strings.WizardDoneTray;
        DoneOverflow.Visibility = PlaceIsland.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

        FillAccessPreview();
        FillCommandFields();
        FillProjectFolders();
    }

    private string StatusOf(ChecklistItem item, ItemState state)
    {
        if (state is ItemState.Locked)
        {
            return item switch
            {
                ChecklistItem.FirstAccount => Strings.StateAfterInstall,
                ChecklistItem.ProjectFolders => Strings.StateAfterSecond,
                _ => Strings.StateAfterSignIn,
            };
        }

        if (state is ItemState.Skipped)
        {
            return Strings.StateLater;
        }

        return item switch
        {
            ChecklistItem.Install => state is ItemState.Done ? Strings.StateInstalled : Strings.StateWaiting,
            ChecklistItem.FirstAccount => state is ItemState.Done ? Strings.StateConnected : Strings.StateSignInNeeded,
            ChecklistItem.SecondEnvironment => state is ItemState.Done ? SecondNameText() : "",
            ChecklistItem.Access => state is ItemState.Done ? Strings.StateAdded : "",
            ChecklistItem.Commands => state is ItemState.Done ? string.Join(" · ", CommandsInUse()) : "",
            ChecklistItem.ProjectFolders => _projectFolders.Count > 0
                ? string.Format(Strings.StateBound, _projectFolders.Count)
                : Strings.StateOptional,
            ChecklistItem.Place => PlaceIsland.IsChecked == true ? Strings.StateIsland : Strings.StateTray,
            _ => "",
        };
    }

    private void OnRowClicked(ChecklistRow row) => Open(row.IsOpen ? null : row.Item);

    /// One item open at a time, so the window stays the size of what is being done.
    private void Open(ChecklistItem? item)
    {
        _opened = item;
        foreach (var row in _rows)
        {
            row.IsOpen = row.Item == item;
        }

        if (item == ChecklistItem.ProjectFolders)
        {
            _foldersVisited = true;
        }

        if (item == ChecklistItem.Install && !_claudeInstalled)
        {
            Wait(Waiter.ForClaude(() =>
            {
                _claudeInstalled = true;
                MoveOn();
            }));
        }
    }

    /// After an item gets its answer, the next open one unfolds by itself.
    private void MoveOn()
    {
        GatherFacts();
        Refresh();
        Open(WizardChecklist.NextToOpen(Facts()));
    }

    private void Wait(Waiter waiter)
    {
        _waiter?.Dispose();
        _waiter = waiter;
    }

    // ---- install ----

    private void OnCopyInstall(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(InstallCommandText);
            CopyButton.Content = Strings.Copied;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program holds the clipboard. The command is on screen to copy by hand.
        }
    }

    // ---- environment 1 ----

    private void OnSignInFirst(object sender, RoutedEventArgs e)
    {
        if (!ClaudeLauncher.Open(FirstFolder, Home))
        {
            return;
        }

        FirstWaiting.Visibility = Visibility.Visible;
        Wait(Waiter.ForSignIn(FirstFolder, () =>
        {
            FirstWaiting.Visibility = Visibility.Collapsed;
            MoveOn();
        }));
    }

    // ---- environment 2 ----

    private void FillCandidates()
    {
        var folders = ClaudeFolders.Find()
            .Where(f => !ClaudeConfigFolder.IsDefault(f.FullPath, Home) && f.HasCredentials)
            .OrderByDescending(f => f.LastUsed ?? DateTimeOffset.MinValue)
            .ToList();

        SecondLead.Text = folders.Count > 0 ? Strings.Env2LeadFound : Strings.Env2LeadNew;
        Candidates.Children.Clear();

        foreach (var folder in folders)
        {
            var account = ClaudeAccounts.Read(folder.FullPath);
            var facts = new List<string> { folder.FolderName };
            if (account.PlanLabel.Length > 0)
            {
                facts.Add(account.PlanLabel);
            }

            if (folder.LastUsed is { } used && DateTimeOffset.UtcNow - used > EnvironmentScan.RecentlyUsed)
            {
                facts.Add(string.Format(Strings.LastSession, used.LocalDateTime.ToString("d MMMM")));
            }

            var choice = new RadioButton
            {
                Style = (Style)FindResource("PlaceCard"),
                GroupName = "Second",
                Margin = new Thickness(0, 0, 0, 6),
                IsChecked = _secondFolder is not null && RealClaude.SameFolder(_secondFolder, folder.FullPath),
                Content = TwoLines(EnvironmentScan.SuggestName(folder.FolderName, Strings.EnvironmentPlainName), string.Join(" · ", facts)),
            };

            var path = folder.FullPath;
            choice.Checked += (_, _) =>
            {
                ChooseSecond(path, EnvironmentScan.SuggestName(Path.GetFileName(path), Strings.EnvironmentPlainName));
                MoveOn();
            };
            Candidates.Children.Add(choice);
        }

        UpdateNewFolderNote();
    }

    private void OnCreateChosen(object sender, RoutedEventArgs e)
    {
        CreatePanel.IsOpen = true;
        NewName.Focus();
    }

    private void OnNewNameChanged(object sender, TextChangedEventArgs e) => UpdateNewFolderNote();

    /// The folder name is shown while typing, so nobody is surprised by .claude-osnovnaya later.
    private void UpdateNewFolderNote()
    {
        var name = NewName.Text.Trim();
        NewNameHint.Visibility = name.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewFolderNote.Text = name.Length == 0
            ? ""
            : string.Format(Strings.FolderIs, Path.GetFileName(ClaudeInstall.NewConfigFolder(name, Home, Directory.Exists)));
    }

    private void ChooseSecond(string folder, string name)
    {
        _secondFolder = folder;
        _secondSkipped = false;
        CreatePanel.IsOpen = false;
        _secondSignedIn = File.Exists(ClaudeInstall.CredentialsPathIn(folder));
        SecondName.Text = _existing.Environments.FirstOrDefault(e => e.Holds(folder))?.Name ?? name;
        SecondNamePanel.Visibility = Visibility.Visible;
    }

    private void OnCreateSecond(object sender, RoutedEventArgs e)
    {
        var name = NewName.Text.Trim();
        if (name.Length == 0)
        {
            NewName.Focus();
            return;
        }

        var folder = ClaudeInstall.NewConfigFolder(name, Home, Directory.Exists);
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"wizard: could not create {Path.GetFileName(folder)} ({error.GetType().Name})");
            return;
        }

        ChooseSecond(folder, name);
        if (!ClaudeLauncher.Open(folder, Home))
        {
            return;
        }

        SecondWaiting.Visibility = Visibility.Visible;
        Wait(Waiter.ForSignIn(folder, () =>
        {
            SecondWaiting.Visibility = Visibility.Collapsed;
            MoveOn();
        }));
        Refresh();
    }

    private void OnSecondLater(object sender, RoutedEventArgs e)
    {
        _secondSkipped = true;
        _secondFolder = null;
        SecondNamePanel.Visibility = Visibility.Collapsed;
        MoveOn();
    }

    private string SecondNameText() => string.IsNullOrWhiteSpace(SecondName.Text) ? "" : SecondName.Text.Trim();

    // ---- project folders ----

    private void FillProjectFolders()
    {
        var second = SecondNameText();
        FoldersLead.Text = string.Format(Strings.FoldersLead, second);
        BoundList.Children.Clear();

        foreach (var folder in _projectFolders.ToList())
        {
            var remove = new Button { Style = (Style)FindResource("QuietButton"), Content = Strings.Remove, Height = 26 };
            remove.Click += (_, _) =>
            {
                _projectFolders.Remove(folder);
                Refresh();
            };

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = folder, Style = (Style)FindResource("RowNote"), VerticalAlignment = VerticalAlignment.Center });
            BoundList.Children.Add(row);
        }

        NoBinds.Visibility = _projectFolders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var names = new[] { FirstNameText(), second }.Where(n => n.Length > 0).ToList();
        var chosen = DefaultChoice.SelectedIndex;
        DefaultChoice.Items = names;
        DefaultChoice.SelectedIndex = chosen < 0 ? 0 : Math.Min(chosen, names.Count - 1);

        var commands = CommandsInUse();
        ExplicitWins.Text = commands.Count == 2 && second.Length > 0
            ? string.Format(Strings.ExplicitWins, commands[0], FirstNameText())
            : "";
    }

    private void OnAddProject(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.PickProject };
        if (dialog.ShowDialog() != true || _projectFolders.Any(f => RealClaude.SameFolder(f, dialog.FolderName)))
        {
            return;
        }

        _projectFolders.Add(dialog.FolderName);
        Refresh();
    }

    // ---- access ----

    private void FillAccessPreview()
    {
        var bridge = BridgePath.Current();
        LinePreview.Text = bridge is null ? Strings.WizardBridgeNotFound : ClaudeSettingsFile.LineFor(bridge);
        FilesToChange.ItemsSource = ConfigFolders().Select(ClaudeSettingsFile.PathIn).ToList();
    }

    private void OnAccessAdd(object sender, RoutedEventArgs e)
    {
        if (BridgePath.Current() is null)
        {
            AccessResult.Text = Strings.AccessNoBridge;
            AccessResult.Visibility = Visibility.Visible;
            return;
        }

        _accessGranted = true;
        MoveOn();
    }

    private void OnAccessLater(object sender, RoutedEventArgs e)
    {
        _accessGranted = false;
        MoveOn();
    }

    // ---- commands ----

    private sealed class ToggleButtonHolder(System.Windows.Controls.Primitives.ToggleButton toggle)
    {
        public bool IsOn => toggle.IsChecked == true;
    }

    private sealed class CommandField
    {
        public required TextBox Box { get; init; }
        public required TextBlock Note { get; init; }
        public string? Custom { get; set; }
    }

    /// One field per environment. The command follows the name until the person types their own,
    /// and "Use the name" hands it back to the name (D-166).
    private void FillCommandFields()
    {
        var names = new[] { FirstNameText(), SecondNameText() };
        var count = _secondFolder is null ? 1 : 2;

        while (CommandFields.Children.Count > count * 2)
        {
            CommandFields.Children.RemoveAt(CommandFields.Children.Count - 1);
            _commandFields.Remove(_commandFields.Count - 1);
        }

        for (var i = 0; i < count; i++)
        {
            if (!_commandFields.TryGetValue(i, out var field))
            {
                field = NewCommandField(i);
                _commandFields[i] = field;
            }

            var wanted = field.Custom ?? LaunchCommand.FromEnvironmentName(names[i]);
            if (field.Box.Text != wanted)
            {
                field.Box.Text = wanted;
            }

            ((TextBlock)((StackPanel)CommandFields.Children[i * 2]).Children[0]).Text = string.Format(Strings.CmdFor, names[i]);
        }

        CheckCommands();
    }

    private CommandField NewCommandField(int index)
    {
        var label = new TextBlock { Style = (Style)FindResource("SectionLabel"), Margin = new Thickness(0, 12, 0, 4) };
        var box = new TextBox { Style = (Style)FindResource("NameBox"), FontFamily = (System.Windows.Media.FontFamily)FindResource("Mono") };
        var header = new StackPanel();
        header.Children.Add(label);
        header.Children.Add(box);

        var note = new TextBlock { Style = (Style)FindResource("RowNote"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None };
        var byName = new Button { Style = (Style)FindResource("QuietButton"), Content = Strings.CmdByName, Height = 24, Margin = new Thickness(8, 2, 0, 0), Visibility = Visibility.Collapsed };
        var footer = new DockPanel();
        DockPanel.SetDock(byName, Dock.Right);
        footer.Children.Add(byName);
        footer.Children.Add(note);

        CommandFields.Children.Add(header);
        CommandFields.Children.Add(footer);

        // An environment that is already set up brings its command along (D-180).
        var existing = index == 0 ? _existing.First(Home) : _existing.Second(Home);
        var field = new CommandField { Box = box, Note = note, Custom = EnvironmentEdits.StartingCommand(existing, CommandFolder.IsSetUp) };
        byName.Visibility = field.Custom is null ? Visibility.Collapsed : Visibility.Visible;
        box.TextChanged += (_, _) =>
        {
            var byNameText = LaunchCommand.FromEnvironmentName(index == 0 ? FirstNameText() : SecondNameText());
            field.Custom = box.Text == byNameText ? null : box.Text;
            byName.Visibility = field.Custom is null ? Visibility.Collapsed : Visibility.Visible;
            CheckCommands();
        };
        byName.Click += (_, _) =>
        {
            field.Custom = null;
            FillCommandFields();
        };

        return field;
    }

    private void CheckCommands()
    {
        var allGood = true;
        foreach (var (index, field) in _commandFields)
        {
            var others = _commandFields.Where(f => f.Key != index).Select(f => f.Value.Box.Text);
            var problem = LaunchCommand.Check(field.Box.Text, others);
            allGood &= problem == CommandProblem.None;

            field.Note.Text = problem switch
            {
                CommandProblem.None => field.Custom is null ? Strings.CmdFollows : Strings.CmdOwn,
                CommandProblem.Empty => Strings.CmdEmpty,
                CommandProblem.TooLong => Strings.CmdTooLong,
                CommandProblem.BadCharacters => Strings.CmdBadCharacters,
                CommandProblem.Reserved => Strings.CmdReserved,
                _ => Strings.CmdTaken,
            };
            field.Note.Foreground = (System.Windows.Media.Brush)FindResource(problem == CommandProblem.None ? "Muted" : "Caution");
        }

        PathAddButton.IsEnabled = allGood;
        PathAddButton.Opacity = allGood ? 1 : 0.5;
    }

    private List<string> CommandsInUse() =>
        _commandFields.OrderBy(f => f.Key).Select(f => f.Value.Box.Text).Where(t => t.Length > 0).ToList();

    /// Found functions start ticked (D-167): left in place, they hide Aiko's commands.
    private void FillProfileFunctions()
    {
        ProfileFunctions.Children.Clear();
        _functions.Clear();

        // The same profile often sits in several files: PowerShell 7 loads profile.ps1 and
        // Microsoft.PowerShell_profile.ps1 both, and Windows PowerShell has its own. One switch per
        // function name, and it acts on every file the function is in.
        var byName = PowerShellProfiles.FindSwitchers()
            .SelectMany(profile => profile.Functions.Select(function => (profile, function)))
            .GroupBy(found => found.function.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byName)
        {
            var toggle = new System.Windows.Controls.Primitives.ToggleButton { Style = (Style)FindResource("Switch"), IsChecked = true };
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = string.Format(Strings.FnRemove, group.Key), Style = (Style)FindResource("RowName") });
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);
            ProfileFunctions.Children.Add(row);

            var holder = new ToggleButtonHolder(toggle);
            foreach (var (profile, function) in group)
            {
                _functions.Add((profile, function, holder));
            }
        }

        ProfilePanel.Visibility = _functions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCommandsAdd(object sender, RoutedEventArgs e)
    {
        _commandsWanted = true;
        MoveOn();
    }

    private void OnCommandsLater(object sender, RoutedEventArgs e)
    {
        _commandsWanted = false;
        MoveOn();
    }

    // ---- names and place ----

    private void OnNamesChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Refresh();
        }
    }

    private void OnPlaceChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            Refresh();
        }
    }

    private string FirstNameText() =>
        string.IsNullOrWhiteSpace(FirstName.Text) ? Strings.EnvironmentPlainName : FirstName.Text.Trim();

    private IEnumerable<string> ConfigFolders() =>
        _secondFolder is null ? [FirstFolder] : [FirstFolder, _secondFolder];

    // ---- finish ----

    private void OnFinish(object sender, RoutedEventArgs e)
    {
        if (!WizardChecklist.CanFinish(Facts()))
        {
            return;
        }

        var settings = BuildSettings();
        SettingsStore.SaveEnvironments(settings);

        var problems = ApplyAccess(settings);
        var commands = ApplyCommands(settings);
        ApplyPlace();

        Log.Write($"checklist finished: {settings.Environments.Count} environments, access={_accessGranted}, commands={_commandsWanted}, bound={_projectFolders.Count}");
        Close();
        Finished?.Invoke(DoneNote(problems, commands));
    }

    /// One short paragraph for the environment page that opens after Finish.
    private string DoneNote(List<PatchProblem> problems, string? commands)
    {
        var parts = new List<string> { Strings.SetupDone };
        if (commands is not null)
        {
            parts.Add(string.Format(Strings.DoneCommands, commands));
        }

        parts.Add(problems.Count > 0
            ? string.Join(" ", problems.Distinct().Select(ClaudeSettingsFile.Words))
            : _accessGranted == true ? Strings.WizardDoneFirstNumbers : Strings.WizardDoneNoAccess);

        return string.Join(" ", parts);
    }

    private EnvironmentSettings BuildSettings()
    {
        AikoEnvironment Keep(string name, string folder, int index, IReadOnlyList<string> projects) =>
            new(name, [folder])
            {
                DirectMode = _existing.Environments.FirstOrDefault(e => e.Holds(folder))?.DirectMode ?? false,
                CustomCommand = _commandFields.TryGetValue(index, out var field) ? field.Custom : null,
                ProjectFolders = projects,
            };

        // The wizard only edits the folders of environment 2. Those of environment 1 stay as they were:
        // a live run lost the personal projects binding here.
        var firstFolders = _existing.First(Home)?.ProjectFolders ?? [];
        var environments = new List<AikoEnvironment> { Keep(FirstNameText(), FirstFolder, 0, firstFolders) };
        if (_secondFolder is not null)
        {
            environments.Add(Keep(SecondNameText(), _secondFolder, 1, _projectFolders.ToList()));
        }

        // A default that was written down stays written down, under its current name.
        var defaultName = DefaultChoice.SelectedIndex > 0 && environments.Count > 1 ? environments[1].Name
            : _existing.DefaultEnvironment is null ? null
            : environments[0].Name;
        // The ring keeps the environment the person put there, when it is still in the list.
        var ring = environments.Any(e => e.Name == _existing.RingEnvironment) ? _existing.RingEnvironment : environments[0].Name;
        return new EnvironmentSettings(environments)
        {
            RingEnvironment = ring,
            DefaultEnvironment = defaultName,
        };
    }

    private List<PatchProblem> ApplyAccess(EnvironmentSettings settings) =>
        _accessGranted == true ? EnvironmentSetup.ApplyAccess(settings) : [];

    private string? ApplyCommands(EnvironmentSettings settings) =>
        _commandsWanted == true
            ? EnvironmentSetup.ApplyCommands(settings, _functions.Where(f => f.Toggle.IsOn).Select(f => (f.Profile.Path, f.Function)))
            : null;

    private void ApplyPlace()
    {
        var runAtStartup = RunAtStartup.IsChecked == true;
        Startup.Set(runAtStartup);

        SettingsStore.Save(SettingsStore.Load() with
        {
            Place = PlaceIsland.IsChecked == true ? AikoPlace.Island : AikoPlace.Tray,
            RunAtStartup = runAtStartup,
            CheckUpdates = CheckUpdates.IsChecked == true,
        });
    }

    private StackPanel TwoLines(string first, string second)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = first, Style = (Style)FindResource("RowName") });
        panel.Children.Add(new TextBlock
        {
            Text = second,
            Style = (Style)FindResource("RowNote"),
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
        });
        return panel;
    }
}
