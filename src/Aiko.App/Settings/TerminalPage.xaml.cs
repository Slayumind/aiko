using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The settings of the terminal: the zoom, the notices, voice input, who recognises it, the model and the key (D-313, D-315, D-396).
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
        BuildNotices();
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
        VoiceOn.IsChecked = _settings.VoiceInput;
        var engine = TryEngine ?? _settings.VoiceEngine;
        Local.IsChecked = engine == VoiceEngine.Local;
        Cloud.IsChecked = engine == VoiceEngine.OpenAI;
        SpeechSystem.IsChecked = _settings.VoiceLanguage == AikoLanguage.System;
        SpeechRussian.IsChecked = _settings.VoiceLanguage == AikoLanguage.Russian;
        SpeechEnglish.IsChecked = _settings.VoiceLanguage == AikoLanguage.English;
        FillZoom();
        _waiting.IsChecked = _settings.NotifyWaiting;
        _done.IsChecked = _settings.NotifyDone;
        _stopped.IsChecked = _settings.NotifyStopped;
        _limit.IsChecked = _settings.NotifyLimit;
        _sound.IsChecked = _settings.NotifySound;
        _filling = false;
        ShowSound();

        ShowModel();
        ShowKey();
    }

    // ---- the notices ----

    private readonly ToggleButton _waiting = new();
    private readonly ToggleButton _done = new();
    private readonly ToggleButton _stopped = new();
    private readonly ToggleButton _limit = new();
    private readonly ToggleButton _sound = new();
    private FrameworkElement? _soundLine;

    /// A row per kind, with the mark the notice itself shows (D-396). The ring of a limit is drawn
    /// as it looks over 90 percent.
    private void BuildNotices()
    {
        NoticeRows.Children.Add(NoticeRow(Marks.Notice(NoticeKind.Waiting, null, LimitTone.Normal), Strings.NotifyWaiting, _waiting, first: true));
        NoticeRows.Children.Add(NoticeRow(Marks.Notice(NoticeKind.Done, null, LimitTone.Normal), Strings.NotifyDone, _done, first: false));
        NoticeRows.Children.Add(NoticeRow(Marks.Notice(NoticeKind.Stopped, null, LimitTone.Normal), Strings.NotifyStopped, _stopped, first: false));
        NoticeRows.Children.Add(NoticeRow(Marks.Notice(NoticeKind.Limit, 91, LimitTone.Caution), Strings.NotifyLimit, _limit, first: false));
        var sound = NoticeRow(Marks.Fit(Marks.Sound()), Strings.NotifySound, _sound, first: true);
        SoundRow.Children.Add(sound);
        _soundLine = sound;
    }

    private Border NoticeRow(FrameworkElement mark, string text, ToggleButton toggle, bool first)
    {
        toggle.Style = (Style)FindResource("Switch");
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.Checked += OnNotifyChanged;
        toggle.Unchecked += OnNotifyChanged;

        mark.Margin = new Thickness(0, 0, 10, 0);
        var words = new TextBlock { Text = text, Style = (Style)FindResource("RowName"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var grid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(words, 1);
        Grid.SetColumn(toggle, 2);
        grid.Children.Add(mark);
        grid.Children.Add(words);
        grid.Children.Add(toggle);
        return new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
            Child = grid,
        };
    }

    private void OnNotifyChanged(object sender, RoutedEventArgs e)
    {
        ShowSound();
        Save(settings => settings with
        {
            NotifyWaiting = _waiting.IsChecked == true,
            NotifyDone = _done.IsChecked == true,
            NotifyStopped = _stopped.IsChecked == true,
            NotifyLimit = _limit.IsChecked == true,
            NotifySound = _sound.IsChecked == true,
        });
    }

    /// The sound belongs to the questions: with them off it has nothing to sound for, so it goes
    /// quiet and keeps its own choice for when they come back.
    private void ShowSound()
    {
        var questions = _waiting.IsChecked == true;
        _sound.IsEnabled = questions;
        if (_soundLine is not null)
        {
            _soundLine.Opacity = questions ? 1 : 0.45;
        }
    }

    /// One segment per step of WindowZoom, in the order of the steps.
    private void FillZoom()
    {
        ZoomSteps.Children.Clear();
        var chosen = WindowZoom.Read(_settings.WindowZoom);
        foreach (var step in WindowZoom.Steps)
        {
            var segment = new RadioButton
            {
                Style = (Style)FindResource("Segment"),
                GroupName = "WindowZoom",
                Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ZoomStep, step),
                IsChecked = step == chosen,
            };
            segment.Checked += (_, _) => Save(settings => settings with { WindowZoom = step });
            ZoomSteps.Children.Add(segment);
        }
    }

    /// The change goes to what the file holds now, not to the copy the page read when it opened:
    /// the terminal window may have changed its zoom or its header since.
    private void Save(Func<AppSettings, AppSettings> change)
    {
        if (_filling)
        {
            return;
        }

        _settings = SettingsStore.Change(change);
        Saved?.Invoke();
    }

    private void OnSpeechChanged(object sender, RoutedEventArgs e) =>
        Save(settings => settings with
        {
            VoiceLanguage = SpeechRussian.IsChecked == true ? AikoLanguage.Russian
                : SpeechEnglish.IsChecked == true ? AikoLanguage.English
                : AikoLanguage.System,
        });

    /// Turning voice on with the computer chosen starts the download: that is the first time
    /// voice is turned on (D-313), and the size was on the page before the tick.
    private void OnVoiceChanged(object sender, RoutedEventArgs e)
    {
        Save(settings => settings with { VoiceInput = VoiceOn.IsChecked == true });
        if (!_filling && _settings.VoiceInput && _settings.VoiceEngine == VoiceEngine.Local)
        {
            StartDownload();
        }
    }

    private void OnEngineChanged(object sender, RoutedEventArgs e)
    {
        Save(settings => settings with { VoiceEngine = Cloud.IsChecked == true ? VoiceEngine.OpenAI : VoiceEngine.Local });
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
