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

    /// The version the site named, kept for the line shown while it downloads.
    private string? _newerVersion;

    /// Where the update line stands, and what went wrong with the last download, in words.
    private UpdateStage _stage = UpdateStage.NotAsked;
    private string? _problem;

    /// Cancelled when the page goes away, so a request still in the air does not come back to
    /// controls that are gone.
    private readonly CancellationTokenSource _closing = new();

    public GeneralPage()
    {
        InitializeComponent();
        Fill();
        ShowUpdate(UpdateInstall.ReadyVersion is null ? UpdateStage.NotAsked : UpdateStage.Ready);
        TryUpdateStage();
        LanguageChoiceDrop.Picked += OnLanguagePicked;
        Unloaded += (_, _) => _closing.Cancel();
    }

    /// Raised after a change was saved.
    public event Action? Saved;

    public event Action? QuitRequested;

    /// Raised after the second click on "Start over", with whether the second folder goes to the
    /// Recycle Bin.
    public event Action<bool>? RestartRequested;

    /// Raised after the second click on "Remove Aiko", with whether the memory stays. Nothing here
    /// can be undone afterwards.
    public event Action<bool>? DeleteRequested;

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
            $"Aiko {AppVersion.WithCommit()}",
            $"Windows {Environment.OSVersion.Version}",
            $"shown in: {(_settings.Place == AikoPlace.Island ? "the island" : "the tray")}",
            $"language: {LanguageName(_settings.Language)}",
            $"starts with Windows: {YesNo(Startup.IsEnabled())}",
            $"checks for updates: {YesNo(_settings.CheckUpdates)}",
            $"sends statistics: {YesNo(_settings.SendStats)}",
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

    /// A door for snapshots: AIKO_TRY_UPDATE=UpToDate, Available, Downloading, Ready, CheckFailed,
    /// DownloadFailed or CannotInstall draws the update line at that stage, with a made-up version.
    private void TryUpdateStage()
    {
        if (!Enum.TryParse<UpdateStage>(Environment.GetEnvironmentVariable("AIKO_TRY_UPDATE"), out var stage))
        {
            return;
        }

        _newerVersion = "9.9.9";
        _problem = stage == UpdateStage.CannotInstall ? Strings.UpdateBadSignature : Strings.UpdateDownloadFailed;
        ShowUpdate(stage);
        if (stage == UpdateStage.Downloading)
        {
            ShowPercent(64);
        }
    }

    /// The tray menu has a "Check for updates" item. It opens this page and asks straight away, so
    /// the answer appears where the button already is.
    public async void StartUpdateCheck() => await CheckAsync();

    /// The one button of the line does whatever is the next step from where the line stands.
    private async void OnUpdateButton(object sender, RoutedEventArgs e)
    {
        switch (UpdateBlock.ActionFor(_stage))
        {
            // Asked for by hand, so it runs whatever the switch says: the switch decides whether
            // Aiko checks on its own, not whether the person may ask.
            case UpdateAction.Check:
                await CheckAsync();
                break;
            case UpdateAction.Install:
                await InstallAsync();
                break;
            case UpdateAction.Restart:
                UpdateInstall.ApplyAndRestart();
                break;
            case UpdateAction.OpenDownloadPage:
                OnOpenDownload(sender, e);
                break;
        }
    }

    private async Task CheckAsync()
    {
        if (UpdateBlock.ActionFor(_stage) == UpdateAction.None)
        {
            // Already asking or downloading. Two answers racing into one line is worse than one.
            return;
        }

        ShowUpdate(UpdateStage.Asking);
        var info = await UpdateRun.AskAsync(_closing.Token).ConfigureAwait(true);

        // The window can be closed while the request is in the air, and touching its controls
        // afterwards throws.
        if (_closing.IsCancellationRequested)
        {
            return;
        }

        var state = info.CompareWith(AppVersion.Current());
        _newerVersion = state == UpdateState.Available ? info.Latest : null;
        _downloadUrl = info.DownloadUrl ?? ReleasesPage;

        // A version that was already downloaded and checked in this session stays offered, whatever
        // the site says now.
        ShowUpdate(UpdateInstall.ReadyVersion is null ? UpdateBlock.After(state) : UpdateStage.Ready);
    }

    /// Downloads the new version and puts it in place only if the release list is signed with our
    /// key and the file matches its line (D-246). Pressing this is the consent to download; the
    /// restart asks once more.
    private async Task InstallAsync()
    {
        ShowUpdate(UpdateStage.Downloading);

        // Velopack reports from its own thread; the number is shown on ours.
        var outcome = await UpdateInstall
            .DownloadAsync(percent => Dispatcher.BeginInvoke(() => ShowPercent(percent)), _closing.Token)
            .ConfigureAwait(true);

        if (_closing.IsCancellationRequested)
        {
            return;
        }

        _problem = Words(outcome);
        ShowUpdate(outcome.Step switch
        {
            InstallStep.Ready => UpdateStage.Ready,
            InstallStep.NothingNewer => UpdateStage.UpToDate,
            InstallStep.Failed => UpdateStage.DownloadFailed,

            // A refusal never offers to try the same file again: the answer is the download page,
            // where a person can look for themselves.
            _ => UpdateStage.CannotInstall,
        });
    }

    /// The words for a download that did not end with a version ready to install.
    private static string? Words(InstallOutcome outcome) => outcome.Step switch
    {
        InstallStep.Ready or InstallStep.NothingNewer => null,
        InstallStep.NotInstalled => Strings.UpdateNotInstalled,
        InstallStep.Failed => Strings.UpdateDownloadFailed,
        _ => outcome.Verdict switch
        {
            UpdateVerdict.NoKeyYet => Strings.UpdateNoKeyYet,
            UpdateVerdict.NotListed or UpdateVerdict.HashMismatch => Strings.UpdateBadFile,
            _ => Strings.UpdateBadSignature,
        },
    };

    /// Draws the line for a stage: what it says, the small print under it, and the one action.
    private void ShowUpdate(UpdateStage stage)
    {
        _stage = stage;
        var version = AppVersion.Current();
        var newer = UpdateInstall.ReadyVersion ?? _newerVersion ?? string.Empty;
        var current = string.Format(Strings.UpdateThisVersion, version);
        var comparing = string.Format(Strings.UpdateNow, version);

        (UpdateTitle.Text, UpdateDetail.Text) = stage switch
        {
            UpdateStage.Asking => (Strings.UpdateAsking, current),
            UpdateStage.UpToDate => (Strings.UpdateLatest, UpdateRun.LastAnswered is { } at
                ? string.Format(Strings.UpdateCheckedAt, version, at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))
                : current),
            UpdateStage.Available => (string.Format(Strings.UpdateAvailable, newer), comparing),
            UpdateStage.CheckFailed => (Strings.UpdateFailed, current),
            UpdateStage.Downloading => (string.Format(Strings.UpdateDownloading, newer), comparing),
            UpdateStage.Ready => (string.Format(Strings.UpdateReady, newer), Strings.UpdateReadyDetail),
            UpdateStage.DownloadFailed or UpdateStage.CannotInstall => (_problem ?? Strings.UpdateDownloadFailed, comparing),
            _ => (current, string.Empty),
        };
        UpdateDetail.Visibility = UpdateDetail.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var action = UpdateBlock.ActionFor(stage);
        UpdateButton.Content = action switch
        {
            UpdateAction.Install => string.Format(Strings.UpdateInstallNow, newer),
            UpdateAction.Restart => Strings.UpdateRestartNow,
            UpdateAction.OpenDownloadPage => Strings.OpenDownloadPage,
            _ => Strings.CheckNow,
        };

        // The step forward is the one bright button on the page; asking again is an ordinary one.
        var forward = action is UpdateAction.Install or UpdateAction.Restart;
        UpdateButton.Background = Tokens.Brush(forward ? "Ink" : "Raised");
        UpdateButton.Foreground = Tokens.Brush(forward ? "Background" : "Ink");
        UpdateButton.IsEnabled = action != UpdateAction.None;
        UpdateButton.Visibility = stage == UpdateStage.Downloading ? Visibility.Collapsed : Visibility.Visible;
        UpdatePercent.Visibility = stage == UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;
        if (stage == UpdateStage.Downloading)
        {
            ShowPercent(0);
        }
    }

    private void ShowPercent(int percent) => UpdatePercent.Text = $"{UpdateBlock.Percent(percent)}%";

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

    // ---- removing Aiko ----

    private void OnDeleteAsked(object sender, RoutedEventArgs e)
    {
        var environments = SettingsStore.LoadEnvironments();
        DeleteLine.Text = string.Format(
            Strings.DeleteLine, string.Join(", ", environments.Environments.Select(env => env.Command)));

        // A copy unpacked from the zip has no uninstaller to call, and Windows will not let a
        // running program delete itself. The person is told before they agree, not after.
        DeletePortable.Visibility = Uninstall.HasUninstaller ? Visibility.Collapsed : Visibility.Visible;

        // The memory is the one thing here the person cannot make again, so it stays unless they
        // say no. With nothing in it there is nothing to ask about.
        DeleteKeepMemory.IsChecked = true;
        DeleteMemory.Visibility = Uninstall.MemoryIsWorthKeeping() ? Visibility.Visible : Visibility.Collapsed;
        ShowWhatHappensToMemory();

        DeleteRow.Visibility = Visibility.Collapsed;
        DeleteConfirm.IsOpen = true;
    }

    private void OnDeleteKeepMemoryChanged(object sender, RoutedEventArgs e) => ShowWhatHappensToMemory();

    /// The line under the tick says what the tick does now: where the file stays, or that it goes.
    private void ShowWhatHappensToMemory() =>
        DeleteMemoryLine.Text = DeleteKeepMemory.IsChecked == true
            ? string.Format(Strings.DeleteMemoryStays, MemoryStore.Path)
            : Strings.DeleteMemoryGoes;

    private void OnDeleteCancelled(object sender, RoutedEventArgs e)
    {
        DeleteConfirm.IsOpen = false;
        DeleteRow.Visibility = Visibility.Visible;
    }

    /// Claude Code can take seconds to forget the plugins, so the page says what is happening and
    /// takes no second press while it happens.
    private void OnDeleteConfirmed(object sender, RoutedEventArgs e)
    {
        DeleteButtons.Visibility = Visibility.Collapsed;
        DeleteWorking.Visibility = Visibility.Visible;
        DeleteKeepMemory.IsEnabled = false;
        DeleteRequested?.Invoke(DeleteKeepMemory.IsChecked == true);
    }
}
