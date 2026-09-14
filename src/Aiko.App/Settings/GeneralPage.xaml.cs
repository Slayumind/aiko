using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Aiko.Core;

namespace Aiko.App;

public partial class GeneralPage : UserControl
{
    private const string ReleasesPage = "https://github.com/Slayumind/aiko/releases/latest";

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly AikoLanguage[] Languages = [AikoLanguage.System, AikoLanguage.Russian, AikoLanguage.English];

    private AppSettings _settings = SettingsStore.Load();

    /// Setting the controls raises their own events, and those events save. This keeps the first
    /// fill from writing the file back the moment the page opens.
    private bool _filling;

    private string _downloadUrl = ReleasesPage;

    /// Cancelled when the page goes away, so a request still in the air does not come back to
    /// controls that are gone.
    private readonly CancellationTokenSource _closing = new();

    public GeneralPage()
    {
        InitializeComponent();
        Fill();
        LanguageChoiceDrop.Picked += OnLanguagePicked;
        Unloaded += (_, _) => _closing.Cancel();
    }

    /// Raised after a change was saved.
    public event Action? Saved;

    public event Action? QuitRequested;

    /// Raised after the second click on "Start over", with whether the second folder goes to the
    /// Recycle Bin.
    public event Action<bool>? RestartRequested;

    /// Raised when the window has to come back in another language.
    public event Action? ReopenRequested;

    private void Fill()
    {
        _filling = true;

        PlaceTray.IsChecked = _settings.Place == AikoPlace.Tray;
        PlaceIsland.IsChecked = _settings.Place == AikoPlace.Island;
        HideInFullScreen.IsChecked = _settings.HideIslandInFullScreen;
        IslandOnly.IsOpen = _settings.Place == AikoPlace.Island;

        // The switch shows what Windows actually holds, not what our file remembers: the person may
        // have removed the entry elsewhere.
        RunAtStartup.IsChecked = Startup.IsEnabled();
        CheckUpdates.IsChecked = _settings.CheckUpdates;

        LanguageChoiceDrop.Items = [Strings.LanguageSystem, "Русский", "English"];
        LanguageChoiceDrop.SelectedIndex = Array.IndexOf(Languages, _settings.Language);

        ShowAccess();

        _filling = false;
    }

    private void Save(AppSettings settings)
    {
        if (_filling)
        {
            return;
        }

        _settings = settings;
        SettingsStore.Save(settings);
        Saved?.Invoke();
    }

    private void OnPlaceChanged(object sender, RoutedEventArgs e)
    {
        // A setting that changes nothing should not look as if it does.
        IslandOnly.IsOpen = PlaceIsland.IsChecked == true;
        Save(_settings with { Place = PlaceIsland.IsChecked == true ? AikoPlace.Island : AikoPlace.Tray });
    }

    private void OnHideChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with { HideIslandInFullScreen = HideInFullScreen.IsChecked == true });

    private void OnUpdatesChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with { CheckUpdates = CheckUpdates.IsChecked == true });

    /// Startup is a key in the registry, not a line in our file, so this one writes to Windows.
    private void OnStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_filling)
        {
            return;
        }

        var wanted = RunAtStartup.IsChecked == true;
        Startup.Set(wanted);
        Save(_settings with { RunAtStartup = wanted });
    }

    private void OnLanguagePicked(int index)
    {
        var language = Languages[index];
        if (language == _settings.Language)
        {
            return;
        }

        Save(_settings with { Language = language });
        LanguageChoice.Apply(language);

        // Every word on screen was picked when its control was made, so the window has to be built
        // again. Aiko makes windows on demand anyway, which is what lets a language change take
        // effect at once instead of "after a restart".
        ReopenRequested?.Invoke();
    }

    /// Everything a bug report needs and nothing it does not: no tokens, no email addresses, no
    /// numbers from the limits, no paths from Claude Code.
    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var environments = SettingsStore.LoadEnvironments();

        // Written for a person to paste into a bug report, so it says yes and no and Git Bash
        // rather than True and GitBash, which are how the code happens to spell them.
        var text = string.Join(
            Environment.NewLine,
            $"Aiko {AppVersion.Current()}",
            $"Windows {Environment.OSVersion.Version}",
            $"shown in: {(_settings.Place == AikoPlace.Island ? "the island" : "the tray")}",
            $"language: {LanguageName(_settings.Language)}",
            $"starts with Windows: {YesNo(Startup.IsEnabled())}",
            $"checks for updates: {YesNo(_settings.CheckUpdates)}",
            $"environments: {environments.Environments.Count}"
                + $", direct mode on for {environments.Environments.Count(env => env.DirectMode)}"
                + $", bound folders {EnvironmentEdits.Bindings(environments).Count}",
            $"launch commands set up: {YesNo(CommandFolder.IsSetUp)}",
            $"status line shell: {(ShellDetect.Current() == ClaudeShell.GitBash ? "Git Bash" : "PowerShell")}",
            $"log: {Log.FilePath}");

        try
        {
            Clipboard.SetText(text);
            DiagnosticsLine.Text = Strings.DiagnosticsCopied;
        }
        catch (Exception copyFailed)
        {
            Log.Write($"could not copy diagnostics: {copyFailed.GetType().Name}");
        }
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string LanguageName(AikoLanguage language) => language switch
    {
        AikoLanguage.English => "English",
        AikoLanguage.Russian => "Русский",
        _ => "the one Windows uses",
    };

    /// Asked for by hand, so it runs whatever the switch says: the switch decides whether Aiko
    /// checks on its own, not whether the person may ask.
    private async void OnCheckNow(object sender, RoutedEventArgs e) => await CheckAsync();

    /// The tray menu has a "Check for updates" item. It opens this page and asks straight away, so
    /// the answer appears where the button already is.
    public async void StartUpdateCheck() => await CheckAsync();

    private async Task CheckAsync()
    {
        if (!CheckNow.IsEnabled)
        {
            // Already asking. Two answers racing into one line is worse than one answer.
            return;
        }

        CheckNow.IsEnabled = false;
        UpdateLine.Text = Strings.UpdateAsking;
        UpdateLine.Visibility = Visibility.Visible;
        OpenDownload.Visibility = Visibility.Collapsed;

        using var client = new UpdateClient();
        var info = await client.AskAsync(_closing.Token).ConfigureAwait(true);

        // The window can be closed while the request is in the air, and touching its controls
        // afterwards throws.
        if (_closing.IsCancellationRequested)
        {
            return;
        }

        var version = AppVersion.Current();
        var state = info.CompareWith(version);

        UpdateLine.Text = state switch
        {
            UpdateState.Available => string.Format(Strings.UpdateAvailable, info.Latest),
            UpdateState.UpToDate => string.Format(Strings.UpdateLatest, version),
            // Never "you are up to date" when we do not know: that is the one answer that would
            // keep every copy quiet after a bad deploy.
            _ => Strings.UpdateFailed,
        };

        _downloadUrl = info.DownloadUrl ?? ReleasesPage;
        OpenDownload.Visibility = state == UpdateState.Available ? Visibility.Visible : Visibility.Collapsed;
        CheckNow.IsEnabled = true;
    }

    private void OnOpenDownload(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_downloadUrl) { UseShellExecute = true });
        }
        catch (Exception failed) when (failed is Win32Exception or InvalidOperationException)
        {
            Log.Write($"could not open the download page: {failed.GetType().Name}");
        }
    }

    /// Whether the line that lets Claude Code report its limits is still in place, and a way to
    /// put it back.
    ///
    /// Answering "not now" in the wizard, or another tool overwriting the status line afterwards,
    /// would otherwise leave Aiko waiting for numbers that never come, with nothing to press.
    private bool _offeringToAdd;

    private void ShowAccess()
    {
        var missing = FoldersWithoutOurLine();

        if (missing.Count == 0)
        {
            AccessLine.Text = SettingsStore.LoadEnvironments().HasEnvironments ? Strings.AccessOk : string.Empty;
            AccessButton.Content = Strings.AccessCheck;
            _offeringToAdd = false;
            return;
        }

        AccessLine.Text = missing.Count == 1
            ? Strings.AccessMissingOne
            : string.Format(Strings.AccessMissingMany, missing.Count);
        AccessButton.Content = Strings.AccessSetUp;
        _offeringToAdd = true;
    }

    private static List<string> FoldersWithoutOurLine() =>
        SettingsStore.LoadEnvironments().Environments
            .SelectMany(environment => environment.ConfigDirectories)
            .Where(folder => !ClaudeSettingsFile.HasOurLine(folder))
            .ToList();

    /// The first press only looks. The second one writes, and the button says so before it does:
    /// this is somebody else's settings file, and pressing "check" should never change it.
    private void OnCheckAccess(object sender, RoutedEventArgs e)
    {
        if (!_offeringToAdd)
        {
            ShowAccess();
            return;
        }

        if (BridgePath.Current() is not { } bridge)
        {
            AccessLine.Text = Strings.AccessNoBridge;
            return;
        }

        var problem = PatchProblem.None;
        foreach (var folder in FoldersWithoutOurLine())
        {
            var outcome = ClaudeSettingsFile.AddBridge(folder, bridge);
            if (problem == PatchProblem.None)
            {
                problem = outcome.Problem;
            }
        }

        ShowAccess();
        Saved?.Invoke();
        if (problem != PatchProblem.None)
        {
            AccessLine.Text = ClaudeSettingsFile.Words(problem);
        }
    }

    private void OnQuit(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();

    // ---- starting over ----

    private void OnRestartAsked(object sender, RoutedEventArgs e)
    {
        var environments = SettingsStore.LoadEnvironments();
        RestartLine.Text = string.Format(Strings.RestartLine, string.Join(", ", environments.Environments.Select(env => env.Command)));

        // Only the second environment's folder can go: .claude belongs to VS Code and Claude Desktop too.
        var second = environments.Second(Home);
        RestartBin.IsChecked = false;
        RestartBin.Visibility = second is null ? Visibility.Collapsed : Visibility.Visible;
        RestartBinText.Text = second is null ? "" : string.Format(Strings.RestartBin, System.IO.Path.GetFileName(second.ConfigDirectories[0]));

        RestartRow.Visibility = Visibility.Collapsed;
        RestartConfirm.IsOpen = true;
    }

    private void OnRestartCancelled(object sender, RoutedEventArgs e)
    {
        RestartConfirm.IsOpen = false;
        RestartRow.Visibility = Visibility.Visible;
    }

    private void OnRestartConfirmed(object sender, RoutedEventArgs e) => RestartRequested?.Invoke(RestartBin.IsChecked == true);
}
