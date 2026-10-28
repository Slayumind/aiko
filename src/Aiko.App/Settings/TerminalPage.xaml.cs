using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The settings of the terminal: the to-do list of the agent (D-344), voice input, who recognises it, the model and the key (D-313, D-315).
public partial class TerminalPage : UserControl
{
    private static readonly long TotalMegabytes = VoiceModel.Megabytes(VoiceModel.Bytes);

    private AppSettings _settings = SettingsStore.Load();

    /// For snapshots: draws the page with this engine chosen, without saving it.
    internal static VoiceEngine? TryEngine { get; set; }

    /// Filling the controls raises their events, and those events save. See GeneralPage.
    private bool _filling;
    private bool _failed;

    public TerminalPage()
    {
        InitializeComponent();
        Fill();
        Track.SizeChanged += (_, _) => ShowModel();

        // The download outlives the page; while the page is shown it follows it.
        Loaded += (_, _) =>
        {
            ModelDownload.Progress += OnProgress;
            ModelDownload.Ended += OnEnded;
        };
        Unloaded += (_, _) =>
        {
            ModelDownload.Progress -= OnProgress;
            ModelDownload.Ended -= OnEnded;
        };
    }

    /// Raised after a change was saved, so the navigation can redraw its line under the item.
    public event Action? Saved;

    private void Fill()
    {
        _filling = true;
        TodosOn.IsChecked = _settings.AgentTodos;
        VoiceOn.IsChecked = _settings.VoiceInput;
        var engine = TryEngine ?? _settings.VoiceEngine;
        Local.IsChecked = engine == VoiceEngine.Local;
        Cloud.IsChecked = engine == VoiceEngine.OpenAI;
        SpeechSystem.IsChecked = _settings.VoiceLanguage == AikoLanguage.System;
        SpeechRussian.IsChecked = _settings.VoiceLanguage == AikoLanguage.Russian;
        SpeechEnglish.IsChecked = _settings.VoiceLanguage == AikoLanguage.English;
        _filling = false;

        ShowModel();
        ShowKey();
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

    private void OnTodosChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with { AgentTodos = TodosOn.IsChecked == true });

    private void OnSpeechChanged(object sender, RoutedEventArgs e) =>
        Save(_settings with
        {
            VoiceLanguage = SpeechRussian.IsChecked == true ? AikoLanguage.Russian
                : SpeechEnglish.IsChecked == true ? AikoLanguage.English
                : AikoLanguage.System,
        });

    /// Turning voice on with the computer chosen starts the download: that is the first time
    /// voice is turned on (D-313), and the size was on the page before the tick.
    private void OnVoiceChanged(object sender, RoutedEventArgs e)
    {
        Save(_settings with { VoiceInput = VoiceOn.IsChecked == true });
        if (!_filling && _settings.VoiceInput && _settings.VoiceEngine == VoiceEngine.Local)
        {
            StartDownload();
        }
    }

    private void OnEngineChanged(object sender, RoutedEventArgs e)
    {
        Save(_settings with { VoiceEngine = Cloud.IsChecked == true ? VoiceEngine.OpenAI : VoiceEngine.Local });
        if (!_filling && _settings.VoiceInput && _settings.VoiceEngine == VoiceEngine.Local)
        {
            StartDownload();
        }

        ShowKey();
    }

    // ---- the model ----

    private void StartDownload()
    {
        _failed = false;
        ModelDownload.Start();
        ShowModel();
    }

    private void OnModelAction(object sender, RoutedEventArgs e)
    {
        if (ModelDownload.Running)
        {
            ModelDownload.Stop();
        }
        else if (ModelDownload.ModelBytes == VoiceModel.Bytes)
        {
            ModelDownload.Remove();
        }
        else
        {
            StartDownload();
        }

        ShowModel();
    }

    private void OnProgress(long bytes) => Dispatcher.BeginInvoke(ShowModel);

    private void OnEnded(string? problem) => Dispatcher.BeginInvoke(() =>
    {
        _failed = problem is not null and not "stopped";
        ShowModel();
    });

    /// One line and one button: what the model is now and the thing to do with it.
    private void ShowModel()
    {
        var ready = ModelDownload.ModelBytes == VoiceModel.Bytes;
        var running = ModelDownload.Running;
        var done = VoiceModel.Megabytes(ModelDownload.Done);

        Track.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        if (running && Track.ActualWidth > 0)
        {
            TrackFill.Width = Track.ActualWidth * ModelDownload.Done / VoiceModel.Bytes;
        }

        ModelLine.Text = ready ? string.Format(Strings.VoiceModelReady, TotalMegabytes)
            : running ? string.Format(Strings.VoiceModelDownloaded, done, TotalMegabytes)
            : _failed ? Strings.VoiceModelFailed
            : ModelDownload.Done > 0 ? string.Format(Strings.VoiceModelStopped, done, TotalMegabytes)
            : string.Format(Strings.VoiceModelWillDownload, TotalMegabytes);
        ModelAction.Content = running ? Strings.VoiceDownloadStop : ready ? Strings.VoiceModelRemove : Strings.VoiceDownloadGo;
    }

    // ---- the key ----

    private void ShowKey()
    {
        KeyPanel.Visibility = Cloud.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var stored = VoiceKey.Load();
        KeyMask.Text = stored is null ? "" : VoiceModel.MaskKey(stored);
        KeyMask.Visibility = stored is null ? Visibility.Collapsed : Visibility.Visible;
        KeyBox.Visibility = stored is null ? Visibility.Visible : Visibility.Collapsed;
        KeyAction.Content = stored is null ? Strings.VoiceKeySave : Strings.VoiceKeyRemove;
    }

    private void OnKeyBoxKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnKeyAction(sender, e);
        }
    }

    private void OnKeyAction(object sender, RoutedEventArgs e)
    {
        KeyProblem.Visibility = Visibility.Collapsed;
        if (VoiceKey.Stored)
        {
            VoiceKey.Remove();
        }
        else if (VoiceModel.CleanKey(KeyBox.Password) is not { } key)
        {
            Problem(Strings.VoiceKeyBad);
        }
        else if (!VoiceKey.Save(key))
        {
            Problem(Strings.VoiceKeyNotSaved);
        }

        KeyBox.Clear();
        ShowKey();
        Saved?.Invoke();
    }

    private void Problem(string text)
    {
        KeyProblem.Text = text;
        KeyProblem.Visibility = Visibility.Visible;
    }
}
