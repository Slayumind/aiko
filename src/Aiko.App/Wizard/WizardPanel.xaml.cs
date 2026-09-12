using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aiko.Core;

namespace Aiko.App;

/// One environment as the wizard offers it: a name the user can change and a switch to leave it
/// out.
public sealed class WizardEnvironment
{
    public required string Name { get; set; }
    public required string FullPath { get; init; }
    public bool Use { get; set; } = true;
}

public partial class WizardPanel : UserControl
{
    private readonly List<WizardEnvironment> _found = [];
    private int _step;

    /// Whether Aiko was allowed to add its line. "Not now" is a real answer, and the last screen
    /// says what that means rather than letting somebody wait for numbers that cannot arrive.
    private bool _accessGranted;

    public WizardPanel() : this(0)
    {
    }

    /// The step to open on. Only a snapshot starts anywhere but the beginning: the later steps
    /// have to be looked at too, and clicking through them for a picture is not possible.
    public WizardPanel(int step)
    {
        InitializeComponent();
        Scan();
        ShowStep(step);
    }

    /// Raised when the wizard is done, whether it wrote anything or not.
    public event Action? Finished;

    public FrameworkElement DragHandle => HeaderRow;

    private void Scan()
    {
        _found.Clear();
        foreach (var environment in EnvironmentScan.Pick(ClaudeFolders.Find(), Strings.EnvironmentPlainName))
        {
            _found.Add(new WizardEnvironment { Name = environment.SuggestedName, FullPath = environment.FullPath });
        }

        Found.ItemsSource = null;
        Found.ItemsSource = _found;
        NothingFound.Visibility = _found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        FoundIntro.Text = _found.Count switch
        {
            0 => string.Empty,
            // One environment is a whole setup, not half of one. Aiko does not push for a second.
            1 => Strings.WizardFoundOne,
            _ => Strings.WizardFoundMany,
        };
    }

    private void ShowStep(int step)
    {
        _step = step;

        StepEnvironments.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepAccess.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepWhere.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepDone.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

        StepTitle.Text = step switch
        {
            0 => Strings.WizardStepEnvironments,
            1 => Strings.WizardStepAccess,
            2 => Strings.WizardStepWhere,
            _ => Strings.WizardStepDone,
        };
        StepCount.Text = step == 3 ? string.Empty : string.Format(Strings.WizardStepCount, step + 1);

        BackButton.Visibility = step is 0 or 3 ? Visibility.Collapsed : Visibility.Visible;
        SkipButton.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step switch
        {
            0 => Strings.Next,
            1 => Strings.Allow,
            2 => Strings.Finish,
            _ => Strings.Close,
        };

        if (step == 1)
        {
            FillAccessStep();
        }
        else if (step == 3)
        {
            FillDoneStep();
        }
    }

    /// What happens next, in the order it will happen.
    private void FillDoneStep()
    {
        var island = PlaceIsland.IsChecked == true;

        DoneWhere.Text = island ? Strings.WizardDoneIsland : Strings.WizardDoneTray;

        DoneOverflow.Visibility = island ? Visibility.Collapsed : Visibility.Visible;

        DoneFirstNumbers.Text = _accessGranted
            ? Strings.WizardDoneFirstNumbers
            : Strings.WizardDoneNoAccess;
    }

    private void FillAccessStep()
    {
        var bridge = BridgePath.Current();
        LinePreview.Text = bridge is null
            ? Strings.WizardBridgeNotFound
            : ClaudeSettingsFile.LineFor(bridge);

        FilesToChange.ItemsSource = Chosen().Select(e => ClaudeSettingsFile.PathIn(e.FullPath)).ToList();
        AccessResult.Visibility = Visibility.Collapsed;
    }

    private List<WizardEnvironment> Chosen() => _found.Where(e => e.Use).ToList();

    private void OnNext(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
                ShowStep(1);
                break;
            case 1:
                Allow();
                break;
            case 2:
                Finish();
                ShowStep(3);
                break;
            default:
                Finished?.Invoke();
                break;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

    /// "Not now" is a real answer. Aiko saves the environments and says so on the last screen and
    /// on the card, with a way back in settings, instead of waiting for numbers that cannot come.
    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _accessGranted = false;
        ShowStep(2);
    }

    private void Allow()
    {
        var bridge = BridgePath.Current();
        if (bridge is null)
        {
            Tell(Strings.AccessNoBridge);
            return;
        }

        var changed = 0;
        var problems = new List<PatchProblem>();

        foreach (var environment in Chosen())
        {
            var outcome = ClaudeSettingsFile.AddBridge(environment.FullPath, bridge);
            if (outcome.Changed)
            {
                changed++;
            }
            else if (outcome.Problem != PatchProblem.None)
            {
                problems.Add(outcome.Problem);
            }
        }

        if (problems.Count > 0)
        {
            Tell(string.Join(" ", problems.Distinct().Select(ClaudeSettingsFile.Words)));
            return;
        }

        Log.Write($"wizard added the bridge to {changed} settings files");
        _accessGranted = true;
        ShowStep(2);
    }

    private void Tell(string message)
    {
        AccessResult.Text = message;
        AccessResult.Visibility = Visibility.Visible;
    }

    private void Finish()
    {
        var environments = new EnvironmentSettings(
            Chosen().Select(e => new AikoEnvironment(Clean(e.Name, e.FullPath), [e.FullPath])).ToList());

        SettingsStore.SaveEnvironments(environments);

        var runAtStartup = RunAtStartup.IsChecked == true;
        Startup.Set(runAtStartup);

        SettingsStore.Save(SettingsStore.Load() with
        {
            Place = PlaceIsland.IsChecked == true ? AikoPlace.Island : AikoPlace.Tray,
            RunAtStartup = runAtStartup,
            CheckUpdates = CheckUpdates.IsChecked == true,
        });

        Log.Write($"wizard finished with {environments.Environments.Count} environments");
    }

    /// An empty name would leave a nameless row on the card, so the folder name steps in.
    private static string Clean(string name, string fullPath) =>
        string.IsNullOrWhiteSpace(name) ? EnvironmentScan.SuggestName(Path.GetFileName(fullPath), Strings.EnvironmentPlainName) : name.Trim();

    private void OnCheckAgain(object sender, RoutedEventArgs e) => Scan();

    /// For a Claude Code installed somewhere unusual: the user points at the folder themselves.
    private void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose a Claude Code folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _found.Add(new WizardEnvironment
        {
            Name = EnvironmentScan.SuggestName(Path.GetFileName(dialog.FolderName), Strings.EnvironmentPlainName),
            FullPath = dialog.FolderName,
        });

        Found.ItemsSource = null;
        Found.ItemsSource = _found;
        NothingFound.Visibility = Visibility.Collapsed;
    }
}
