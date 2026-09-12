using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Aiko.Core;

namespace Aiko.App;

/// One environment as the settings window shows it.
public sealed class EnvironmentLine
{
    public required string Name { get; init; }
    public required string Folders { get; init; }
    public required bool DirectMode { get; init; }
}

public partial class SettingsPanel : UserControl
{
    private AppSettings _settings = AppSettings.Default;
    private EnvironmentSettings _environments = EnvironmentSettings.Empty;

    /// Setting the controls raises their own events, and those events save. This keeps the first
    /// fill from writing the file back the moment the window opens.
    private bool _filling;

    public SettingsPanel()
    {
        InitializeComponent();
        Fill();
    }

    public event Action? CloseRequested;
    public event Action? QuitRequested;

    public FrameworkElement DragHandle => HeaderRow;

    private void Fill()
    {
        _filling = true;

        _settings = SettingsStore.Load();
        _environments = SettingsStore.LoadEnvironments();

        var lines = _environments.Environments
            .Select(environment => new EnvironmentLine
            {
                Name = environment.Name,
                Folders = string.Join(", ", environment.ConfigDirectories.Select(Path.GetFileName)),
                DirectMode = environment.DirectMode,
            })
            .ToList();

        Environments.ItemsSource = lines;
        NoEnvironments.Visibility = lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        PlaceTray.IsChecked = _settings.Place == AikoPlace.Tray;
        PlaceIsland.IsChecked = _settings.Place == AikoPlace.Island;
        HideInFullScreen.IsChecked = _settings.HideIslandInFullScreen;
        ShowIslandOptions();

        // The switch shows what Windows actually holds, not what our file remembers: the user may
        // have removed the entry elsewhere.
        RunAtStartup.IsChecked = Startup.IsEnabled();
        CheckUpdates.IsChecked = _settings.CheckUpdates;

        LanguageSystem.IsChecked = _settings.Language == AikoLanguage.System;
        LanguageEnglish.IsChecked = _settings.Language == AikoLanguage.English;
        LanguageRussian.IsChecked = _settings.Language == AikoLanguage.Russian;

        VersionLine.Text = $"{Version()} · github.com/Slayumind/aiko";

        _filling = false;
    }

    private static string Version() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "unknown";

    private void Save(AppSettings settings)
    {
        if (_filling)
        {
            return;
        }
        _settings = settings;
        SettingsStore.Save(settings);
    }

    private void OnPlaceChanged(object sender, RoutedEventArgs e)
    {
        ShowIslandOptions();
        Save(_settings with { Place = PlaceIsland.IsChecked == true ? AikoPlace.Island : AikoPlace.Tray });
    }

    /// A setting that changes nothing should not look as if it does.
    private void ShowIslandOptions() =>
        IslandOnly.Visibility = PlaceIsland.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void OnHideChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with { HideIslandInFullScreen = HideInFullScreen.IsChecked == true });

    private void OnUpdatesChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with { CheckUpdates = CheckUpdates.IsChecked == true });

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        var language = LanguageEnglish.IsChecked == true ? AikoLanguage.English
            : LanguageRussian.IsChecked == true ? AikoLanguage.Russian
            : AikoLanguage.System;
        Save(_settings with { Language = language });
    }

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

    private void OnDirectModeChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || sender is not ToggleButton { Tag: string name } toggle)
        {
            return;
        }

        var updated = _environments.Environments
            .Select(environment => environment.Name == name
                ? environment with { DirectMode = toggle.IsChecked == true }
                : environment)
            .ToList();

        _environments = _environments with { Environments = updated };
        SettingsStore.SaveEnvironments(_environments);
    }

    /// Everything a bug report needs and nothing it does not: no tokens, no numbers from the
    /// limits, no paths from Claude Code.
    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var text = string.Join(
            Environment.NewLine,
            $"Aiko {Version()}",
            $"Windows {Environment.OSVersion.Version}",
            $"place: {_settings.Place}",
            $"language: {_settings.Language}",
            $"start with Windows: {Startup.IsEnabled()}",
            $"check for updates: {_settings.CheckUpdates}",
            $"environments: {_environments.Environments.Count}",
            $"log: {Log.FilePath}");

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception copyFailed)
        {
            Log.Write($"could not copy diagnostics: {copyFailed.GetType().Name}");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void OnQuit(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();
}
