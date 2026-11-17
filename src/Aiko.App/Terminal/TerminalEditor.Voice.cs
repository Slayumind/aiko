using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;
using Path = System.IO.Path;

namespace Aiko.App;

/// Voice input in the editor (IDictationField).
sealed partial class TerminalEditor
{
    public void ShowVoice(VoiceLine line, VoiceNotice notice)
    {
        _voiceActive = line.Word != VoiceWord.None;
        _mic.Visibility = line.Button == VoiceButton.Hidden ? Visibility.Collapsed : Visibility.Visible;
        var recording = line.Button == VoiceButton.Recording;
        _micBack.Fill = recording ? new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x64, 0x67)) : Brushes.Transparent;
        _micIcon.Stroke = Tokens.Brush(recording ? "Destructive" : "Muted");
        _mic.ToolTip = recording ? Strings.VoiceStopTip : Strings.VoiceButtonTip;
        if (!line.Wave)
        {
            foreach (FrameworkElement bar in _wave.Children)
            {
                bar.Height = BarLow;
            }
        }

        _wave.Visibility = line.Wave ? Visibility.Visible : Visibility.Collapsed;
        _hint.Visibility = _voiceActive ? Visibility.Collapsed : Visibility.Visible;
        _voiceLine.Visibility = _voiceActive ? Visibility.Visible : Visibility.Collapsed;
        _voiceWord.Text = line.Word switch
        {
            VoiceWord.Warming => Strings.VoiceWarming,
            VoiceWord.Listening => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.VoiceListening, line.Time),
            VoiceWord.Finishing => Strings.VoiceFinishing,
            _ => "",
        };
        _voiceWord.Foreground = Tokens.Brush(line.Word == VoiceWord.Listening ? "Destructive" : "Muted");
        _voiceWhere.Text = line.Engine switch
        {
            VoiceEngine.Local => Strings.VoiceOnComputer,
            VoiceEngine.OpenAI => "OpenAI",
            _ => "",
        };
        _voiceCancel.Text = line.CancelHint ? Strings.VoiceCancelHint : "";

        // The line lives in the editor. In a session of Claude Code the editor is not on screen
        // until Ctrl+G, so a new line opens it the way Ctrl+G does, and Ctrl+G closes it again.
        var newNotice = notice != VoiceNotice.None && notice != _shownNotice;
        _shownNotice = notice;
        _notice.Visibility = notice == VoiceNotice.None ? Visibility.Collapsed : Visibility.Visible;
        if (newNotice && !Wanted && _session?.Kind == SessionKind.Claude)
        {
            Compose();
        }
        (_noticeText.Text, _noticeLink.Text) = notice switch
        {
            VoiceNotice.TurnedOff => (Strings.VoiceTurnedOff, Strings.VoiceOpenSettings),
            VoiceNotice.NoMicrophone => (Strings.VoiceNoMic, Strings.VoiceNoMicLink),
            VoiceNotice.NeedsModel => (Strings.VoiceNeedsModel, Strings.VoiceOpenSettings),
            VoiceNotice.NeedsKey => (Strings.VoiceNeedsKey, Strings.VoiceOpenSettings),
            VoiceNotice.CloudFailed => (Strings.VoiceCloudFailed, Strings.VoiceOpenSettings),
            VoiceNotice.CloudNoMoney => (Strings.VoiceCloudNoMoney, Strings.VoiceOpenSettings),
            VoiceNotice.CloudBusy => (Strings.VoiceCloudBusy, ""),
            _ => ("", ""),
        };
    }

    /// The loudness of the last moment, from 0 to 1: the wave moves one bar to the left (D-319).
    public void PushLevel(double level)
    {
        var bars = _wave.Children;
        for (var i = 0; i < bars.Count - 1; i++)
        {
            ((FrameworkElement)bars[i]).Height = ((FrameworkElement)bars[i + 1]).Height;
        }

        ((FrameworkElement)bars[^1]).Height = BarLow + (BarHigh - BarLow) * Math.Clamp(level, 0, 1);
    }

    /// The words go where the caret stands; what is around it stays.
    public void BeginDictation()
    {
        _choices.Close();

        // In a session of Claude Code without the editor, the editor comes up only to show the words;
        // they go into the line of Claude Code at the end (D-370).
        _strip = !Wanted;
        (_before, _after) = _strip ? ("", "") : DictationText.Around(_text.Text, _text.CaretIndex);
        _dictating = true;
        _text.IsReadOnly = true;
        ShowDictation(DictationText.Empty);
        if (_strip)
        {
            WantedChanged?.Invoke();
        }
    }

    public void ShowDictation(DictationText dictation)
    {
        if (!_dictating)
        {
            return;
        }

        // The words that will not change are white, the tail the recogniser may still change grey.
        _dictation.Inlines.Clear();
        _dictation.Inlines.Add(new Run(_before + dictation.ConfirmedText) { Foreground = Tokens.Brush("Ink") });
        if (dictation.Tail.Count > 0)
        {
            var gap = dictation.Confirmed.Count > 0 ? " " : "";
            _dictation.Inlines.Add(new Run(gap + dictation.TailText) { Foreground = Tokens.Brush("Muted") });
        }

        _dictation.Inlines.Add(new Run(_after) { Foreground = Tokens.Brush("Ink") });
        _dictation.Visibility = Visibility.Visible;
        // Hidden, the field would lose the keyboard, and the next press would go nowhere (D-328).
        _text.Opacity = 0;
        _placeholder.Visibility = Visibility.Collapsed;
    }

    /// The final words go into the field, with the caret after them; null leaves it as it was.
    public void EndDictation(string? words)
    {
        if (!_dictating)
        {
            return;
        }

        _dictating = false;
        _text.IsReadOnly = false;
        _dictation.Visibility = Visibility.Collapsed;
        _text.Opacity = 1;
        _placeholder.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_strip)
        {
            _strip = false;
            WantedChanged?.Invoke();
            if (words is { Length: > 0 })
            {
                Inserted?.Invoke(EditorSend.Inserts(words, []));
            }

            return;
        }

        if (words is { Length: > 0 })
        {
            var (text, caret) = DictationText.Placed(_before, words, _after);
            _text.Text = text;
            _text.CaretIndex = caret;
        }

        FocusText();
    }

    private void NoticeLinkClicked()
    {
        if (_shownNotice == VoiceNotice.NoMicrophone)
        {
            Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute = true });
        }
        else
        {
            VoiceSettingsAsked?.Invoke();
        }
    }

    /// The microphone of the slayumind set, in a 24 box (D-315).
    private static Shape MicIcon() => new System.Windows.Shapes.Path
    {
        Data = Geometry.Parse("M12,1.5 A4,4 0 0 1 16,5.5 V10.5 A4,4 0 0 1 8,10.5 V5.5 A4,4 0 0 1 12,1.5 Z M4.5,11 A7.5,7.5 0 0 0 19.5,11 M12,18.5 V22.5 M8.5,22.5 H15.5"),
        Stroke = Tokens.Brush("Muted"),
        StrokeThickness = 1.95,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };

    private const double BarLow = 2;
    private const double BarHigh = 14;

    /// Fourteen red bars, 2 px wide with 2 px between them; silence is a low flat line (D-319).
    private static StackPanel Wave()
    {
        var wave = new StackPanel { Orientation = Orientation.Horizontal, Height = BarHigh, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 14; i++)
        {
            wave.Children.Add(new Border
            {
                Width = 2,
                Height = BarLow,
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 0, i < 13 ? 2 : 0, 0),
                Background = Tokens.Brush("Destructive"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return wave;
    }

    /// A round button with the mic, tinted red while it records (D-315).
    private Button MicButton()
    {
        var hover = new Ellipse { Width = 24, Height = 24, Fill = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var icon = new Canvas { Width = 24, Height = 24 };
        icon.Children.Add(_micIcon);
        var face = new Grid { Width = 24, Height = 24 };
        face.Children.Add(_micBack);
        face.Children.Add(hover);
        face.Children.Add(new Viewbox { Width = 15, Height = 15, Child = icon });

        var button = new Button
        {
            Content = face,
            ToolTip = Strings.VoiceButtonTip,
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(10, -4, 0, -4),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
        };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => VoicePressed?.Invoke();
        return button;
    }
}
