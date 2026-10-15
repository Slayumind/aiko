using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using Shape = System.Windows.Shapes.Path;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The field of a conversation (D-294, D-302): the text, the pictures that go with it, the stop
/// button and the line "mode · model · environment" under it.
///
/// Enter sends, Shift+Enter is a new line, Esc stops the work, Shift+Tab changes the mode, the
/// arrows walk through what was sent in this session. A picture comes from the clipboard or from
/// a drop and waits above the text as a small picture with a cross; any other file or folder that
/// is dropped becomes its path in the text.
///
/// Ctrl+Space or the round mic button starts and finishes voice input (D-313, D-315). While it
/// goes on the button is tinted red, the line under the field says what is happening, and the
/// words lie over the field: the confirmed ones white, the tail the recogniser may still change
/// grey. A TextBox has one colour, so the field itself hides until the words are final.
sealed class TalkInput : UserControl
{
    /// Pictures larger than this are made smaller before they go: Claude Code takes up to about
    /// 5 MB of a picture, and a screenshot of a large screen is often more as PNG.
    private const int LargestSide = 1800;

    private static readonly string[] PictureFiles = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    private readonly TextBox _text = new();
    private readonly WrapPanel _pictures = new() { Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
    private readonly List<AttachedImage> _attached = [];
    private readonly Button _stop;
    private readonly TextBlock _mode = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _model = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _where = TerminalPanel.Mono("", "Muted");

    private static readonly Brush RecordingTint = Frozen(Color.FromArgb(0x33, 0xFF, 0x64, 0x67));
    private static readonly Brush RingTint = Frozen(Color.FromArgb(0x1F, 0xFF, 0x64, 0x67));

    private readonly Button _mic;
    private readonly Ellipse _micBack = new() { Width = 26, Height = 26, Fill = Brushes.Transparent };
    private readonly Shape _micIcon = MicIcon();
    private readonly Ellipse _ring = new() { Width = 34, Height = 34, Margin = new Thickness(-4), Fill = RingTint, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _ringScale = new(1, 1, 17, 17);
    private readonly TextBlock _dictation = new() { TextWrapping = TextWrapping.Wrap, MaxHeight = 200, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Border _notice;
    private readonly TextBlock _noticeText = TerminalPanel.Text("", "TextName", "Ink", FontWeights.Normal);
    private readonly TextBlock _noticeLink = TerminalPanel.Text("", "TextName", "Ink", FontWeights.Normal);
    private readonly TextBlock _voiceWord = TerminalPanel.Mono("", "Ink");
    private readonly TextBlock _voiceWhere = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _voiceCancel = TerminalPanel.Mono("", "Muted");
    private TextBlock _placeholder;
    private VoiceNotice _shownNotice;
    private bool _voiceActive;
    private bool _dictating;
    private string _before = "";
    private string _after = "";

    /// What was sent, per session, oldest first, and where the arrows stand in it.
    private readonly Dictionary<string, List<string>> _sent = [];
    private string _session = "";
    private int _back = -1;
    private string _draft = "";
    private bool _busy;

    public TalkInput()
    {
        Focusable = false;

        TerminalPanel.Field(_text);
        _text.Background = Brushes.Transparent;
        _text.BorderThickness = new Thickness(0);
        _text.Padding = new Thickness(0);
        _text.AcceptsReturn = true;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.MinHeight = 24;
        _text.MaxHeight = 200;
        _text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _text.VerticalContentAlignment = VerticalAlignment.Top;
        _text.PreviewKeyDown += OnKey;
        CommandManager.AddPreviewExecutedHandler(_text, OnPasteCommand);

        _stop = StopButton(() => StopAsked?.Invoke());
        _mic = MicButton();

        var placeholder = _placeholder = TerminalPanel.Text(Strings.TerminalDescribeTask, "TextName", "Muted", FontWeights.Normal);
        placeholder.Margin = new Thickness(2, 1, 0, 0);
        placeholder.VerticalAlignment = VerticalAlignment.Top;
        placeholder.IsHitTestVisible = false;
        _text.TextChanged += (_, _) => ShowPlaceholder();

        // The mic button is the last at the right; the stop button stands left of it, where the
        // paper clip will be (D-308, D-315).
        var field = new DockPanel();
        DockPanel.SetDock(_mic, Dock.Right);
        DockPanel.SetDock(_stop, Dock.Right);
        field.Children.Add(_mic);
        field.Children.Add(_stop);
        _dictation.FontFamily = _text.FontFamily;
        _dictation.FontSize = _text.FontSize;
        _dictation.Margin = new Thickness(2, 0, 2, 0);
        var words = new Grid();
        words.Children.Add(_text);
        words.Children.Add(placeholder);
        words.Children.Add(_dictation);
        field.Children.Add(words);

        var inside = new StackPanel();
        inside.Children.Add(_pictures);
        inside.Children.Add(field);

        var compose = new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursors.IBeam,
            AllowDrop = true,
            Child = inside,
        };
        compose.MouseLeftButtonDown += (_, _) => _text.Focus();
        compose.PreviewDragOver += OnDragOver;
        compose.PreviewDrop += OnDrop;
        _text.AllowDrop = true;

        // The mode changes with a click or Shift+Tab, in the order of Claude Code (D-302).
        _mode.Cursor = Cursors.Hand;
        _mode.ToolTip = "Shift+Tab";
        _mode.MouseLeftButtonUp += (_, _) => ModeAsked?.Invoke();

        // The model changes with a click too, in the order of the form of a new session.
        _model.Cursor = Cursors.Hand;
        _model.MouseLeftButtonUp += (_, _) => ModelAsked?.Invoke();
        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 6, 0, 14) };
        status.Children.Add(_mode);
        status.Children.Add(_model);
        status.Children.Add(_where);
        status.Children.Add(_voiceWord);
        status.Children.Add(_voiceWhere);
        status.Children.Add(_voiceCancel);
        _voiceWord.FontWeight = FontWeights.Medium;

        // The line above the field when voice input cannot start, with the way to fix it.
        _noticeLink.TextDecorations = TextDecorations.Underline;
        _noticeLink.Cursor = Cursors.Hand;
        _noticeLink.Margin = new Thickness(12, 0, 0, 0);
        _noticeLink.MouseLeftButtonUp += (_, _) => NoticeLinkClicked();
        var noticeWords = new WrapPanel();
        noticeWords.Children.Add(_noticeText);
        noticeWords.Children.Add(_noticeLink);
        _notice = new Border
        {
            BorderBrush = Tokens.Brush("Caution"),
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed,
            Child = noticeWords,
        };

        var all = new StackPanel();
        all.Children.Add(_notice);
        all.Children.Add(compose);
        all.Children.Add(status);
        Content = all;
    }

    public event Action<string, IReadOnlyList<AttachedImage>>? Sent;
    public event Action? StopAsked;
    public event Action? ModeAsked;
    public event Action? ModelAsked;

    /// Ctrl+Space or the mic button.
    public event Action? VoicePressed;

    /// Esc while a recording goes on.
    public event Action? VoiceCancelled;

    /// The link of a line above the field that says the model or the key is missing.
    public event Action? VoiceSettingsAsked;

    public new void Focus() => _text.Focus();

    /// What voice input shows now: the button, the line under the field and the line above it.
    public void ShowVoice(VoiceLine line, VoiceNotice notice)
    {
        _voiceActive = line.Word != VoiceWord.None;
        _mic.Visibility = line.Button == VoiceButton.Hidden ? Visibility.Collapsed : Visibility.Visible;
        var recording = line.Button == VoiceButton.Recording;
        _micBack.Fill = recording ? RecordingTint : Brushes.Transparent;
        _micIcon.Stroke = Tokens.Brush(recording ? "Destructive" : "Muted");
        _mic.ToolTip = recording ? Strings.VoiceStopTip : Strings.VoiceButtonTip;
        _ring.Visibility = line.Ring ? Visibility.Visible : Visibility.Collapsed;

        var usual = _voiceActive ? Visibility.Collapsed : Visibility.Visible;
        var voice = _voiceActive ? Visibility.Visible : Visibility.Collapsed;
        _mode.Visibility = _model.Visibility = _where.Visibility = usual;
        _voiceWord.Visibility = _voiceWhere.Visibility = _voiceCancel.Visibility = voice;
        _voiceWord.Text = line.Word switch
        {
            VoiceWord.Warming => Strings.VoiceWarming,
            VoiceWord.Listening => string.Format(Strings.VoiceListening, line.Time),
            VoiceWord.Finishing => Strings.VoiceFinishing,
            _ => "",
        };
        _voiceWord.Foreground = Tokens.Brush(line.Word == VoiceWord.Warming ? "Muted" : "Ink");
        _voiceWhere.Text = line.Engine switch
        {
            VoiceEngine.Local => " · " + Strings.VoiceOnComputer,
            VoiceEngine.OpenAI => " · OpenAI",
            _ => "",
        };
        _voiceCancel.Text = line.CancelHint ? " · " + Strings.VoiceCancelHint : "";

        _shownNotice = notice;
        _notice.Visibility = notice == VoiceNotice.None ? Visibility.Collapsed : Visibility.Visible;
        (_noticeText.Text, _noticeLink.Text) = notice switch
        {
            VoiceNotice.NoMicrophone => (Strings.VoiceNoMic, Strings.VoiceNoMicLink),
            VoiceNotice.NeedsModel => (Strings.VoiceNeedsModel, Strings.VoiceOpenSettings),
            VoiceNotice.NeedsKey => (Strings.VoiceNeedsKey, Strings.VoiceOpenSettings),
            VoiceNotice.CloudFailed => (Strings.VoiceCloudFailed, Strings.VoiceOpenSettings),
            _ => ("", ""),
        };
    }

    /// The loudness of the last tenth of a second, from 0 to 1: the ring grows with it.
    public void SetLevel(double level) =>
        _ringScale.ScaleX = _ringScale.ScaleY = 1 + 0.3 * Math.Clamp(level, 0, 1);

    /// The words go where the caret stands; what is around it stays.
    public void BeginDictation()
    {
        var at = _text.CaretIndex;
        _before = _text.Text[..at];
        _after = _text.Text[at..];
        if (_before.Length > 0 && !char.IsWhiteSpace(_before[^1]))
        {
            _before += " ";
        }

        _dictating = true;
        _text.IsReadOnly = true;
        ShowDictation(DictationText.Empty);
    }

    public void ShowDictation(DictationText dictation)
    {
        if (!_dictating)
        {
            return;
        }

        _dictation.Inlines.Clear();
        _dictation.Inlines.Add(new Run(_before + dictation.ConfirmedText) { Foreground = Tokens.Brush("Ink") });
        if (dictation.Tail.Count > 0)
        {
            var gap = dictation.Confirmed.Count > 0 ? " " : "";
            _dictation.Inlines.Add(new Run(gap + dictation.TailText) { Foreground = Tokens.Brush("Muted") });
        }

        _dictation.Inlines.Add(new Run(_after) { Foreground = Tokens.Brush("Ink") });
        _dictation.Visibility = Visibility.Visible;
        _text.Visibility = Visibility.Hidden;
        ShowPlaceholder();
    }

    /// The words "describe the task" show in an empty field, but not over the words of a recording.
    private void ShowPlaceholder() =>
        _placeholder.Visibility = _text.Text.Length == 0 && !_dictating ? Visibility.Visible : Visibility.Collapsed;

    /// The final words go into the field, with the caret after them. Null puts the field back as
    /// it was before the recording.
    public void EndDictation(string? words)
    {
        if (!_dictating)
        {
            return;
        }

        _dictating = false;
        _text.IsReadOnly = false;
        _dictation.Visibility = Visibility.Collapsed;
        _text.Visibility = Visibility.Visible;
        ShowPlaceholder();
        if (words is { Length: > 0 })
        {
            var gap = _after.Length > 0 && !char.IsWhiteSpace(_after[0]) ? " " : "";
            _text.Text = _before + words + gap + _after;
            _text.CaretIndex = (_before + words).Length;
        }

        _text.Focus();
    }

    /// A new session starts with an empty field: what was typed for another one stays out of it.
    public void Show(string session, bool busy, PermissionMode mode, string model, string environment)
    {
        if (session != _session)
        {
            // A recording belongs to the session it started in.
            if (_voiceActive)
            {
                VoiceCancelled?.Invoke();
            }

            _session = session;
            _text.Clear();
            _attached.Clear();
            DrawPictures();
            _back = -1;
        }

        _busy = busy;
        _stop.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _mode.Text = PermissionModes.Name(mode);
        _model.Text = model.Length > 0 ? " · " + model : "";
        _where.Text = " · " + environment;
    }

    // ---- the keys ----

    private void OnKey(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Space && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            VoicePressed?.Invoke();
        }
        else if (_dictating && e.Key == Key.Enter)
        {
            // The text of a recording is never sent by itself (D-313), not even half way.
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _voiceActive)
        {
            e.Handled = true;
            VoiceCancelled?.Invoke();
        }
        else if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            Send();
        }
        else if (e.Key == Key.Escape && _busy)
        {
            e.Handled = true;
            StopAsked?.Invoke();
        }
        else if (e.Key == Key.Tab && modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
            ModeAsked?.Invoke();
        }
        else if (e.Key == Key.Up && modifiers == ModifierKeys.None && OnFirstLine())
        {
            e.Handled = Walk(-1);
        }
        else if (e.Key == Key.Down && modifiers == ModifierKeys.None && _back >= 0 && OnLastLine())
        {
            e.Handled = Walk(+1);
        }
    }

    private void Send()
    {
        var text = _text.Text.Trim();
        if (text.Length == 0 && _attached.Count == 0)
        {
            return;
        }

        if (text.Length > 0)
        {
            History().Add(text);
        }

        IReadOnlyList<AttachedImage> pictures = [.. _attached];
        _text.Clear();
        _attached.Clear();
        DrawPictures();
        _back = -1;
        Sent?.Invoke(text, pictures);
    }

    /// One step through what was sent: up is older, down is newer, past the newest is the draft.
    private bool Walk(int step)
    {
        var history = History();
        if (history.Count == 0)
        {
            return false;
        }

        if (_back < 0)
        {
            if (step > 0)
            {
                return false;
            }

            _draft = _text.Text;
            _back = history.Count;
        }

        var next = _back + step;
        if (next < 0)
        {
            return true;
        }

        _back = next;
        _text.Text = next < history.Count ? history[next] : _draft;
        if (next >= history.Count)
        {
            _back = -1;
        }

        _text.CaretIndex = _text.Text.Length;
        return true;
    }

    private List<string> History()
    {
        if (!_sent.TryGetValue(_session, out var history))
        {
            history = [];
            _sent[_session] = history;
        }

        return history;
    }

    private bool OnFirstLine() => _text.GetLineIndexFromCharacterIndex(_text.CaretIndex) <= 0;

    private bool OnLastLine() =>
        _text.GetLineIndexFromCharacterIndex(_text.CaretIndex) >= Math.Max(0, _text.LineCount - 1);

    // ---- pictures and files ----

    /// Ctrl+V with a picture in the clipboard attaches it; copied files are dropped. Text pastes
    /// as it always does.
    private void OnPasteCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste)
        {
            return;
        }

        if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } picture)
        {
            e.Handled = true;
            Attach(picture);
        }
        else if (Clipboard.ContainsFileDropList())
        {
            e.Handled = true;
            Take([.. Clipboard.GetFileDropList().Cast<string>()]);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            e.Handled = true;
            Take(files);
            _text.Focus();
        }
    }

    /// A picture file is attached; any other file or folder is its path in the text, in quotes
    /// when it has a space.
    private void Take(IReadOnlyList<string> paths)
    {
        var words = new List<string>();
        foreach (var path in paths)
        {
            if (PictureFiles.Contains(Path.GetExtension(path).ToLowerInvariant()) && Load(path) is { } picture)
            {
                Attach(picture);
            }
            else
            {
                words.Add(path.Contains(' ') ? $"\"{path}\"" : path);
            }
        }

        if (words.Count > 0)
        {
            var at = _text.CaretIndex;
            var before = at > 0 && !char.IsWhiteSpace(_text.Text[at - 1]) ? " " : "";
            var inserted = before + string.Join(" ", words) + " ";
            _text.Text = _text.Text.Insert(at, inserted);
            _text.CaretIndex = at + inserted.Length;
        }
    }

    private static BitmapSource? Load(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Attach(BitmapSource picture)
    {
        var scale = Math.Min(1.0, LargestSide / (double)Math.Max(picture.PixelWidth, picture.PixelHeight));
        BitmapSource sized = scale < 1 ? new TransformedBitmap(picture, new ScaleTransform(scale, scale)) : picture;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sized));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        _attached.Add(new AttachedImage("image/png", Convert.ToBase64String(stream.ToArray())));
        DrawPictures();
    }

    private void DrawPictures()
    {
        _pictures.Children.Clear();
        for (var at = 0; at < _attached.Count; at++)
        {
            _pictures.Children.Add(Thumbnail(_attached[at], at));
        }

        _pictures.Visibility = _attached.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement Thumbnail(AttachedImage picture, int index)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelHeight = 96;
        image.StreamSource = new MemoryStream(Convert.FromBase64String(picture.Base64));
        image.EndInit();
        image.Freeze();

        var cross = TerminalPanel.Line("M0,0 L7,7 M7,0 L0,7", "Ink");
        var remove = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = Tokens.Brush("Raised"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -6, 0),
            Cursor = Cursors.Hand,
            Child = new Border { Child = cross, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        remove.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _attached.RemoveAt(index);
            DrawPictures();
        };

        var frame = new Grid { Margin = new Thickness(0, 0, 12, 0) };
        frame.Children.Add(new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(8),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill },
        });
        frame.Children.Add(remove);
        return frame;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
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

    /// The microphone of the slayumind set, drawn in a 24 box with a line of one unit (D-315).
    private static Shape MicIcon() => new()
    {
        Data = Geometry.Parse("M12,1.5 A4,4 0 0 1 16,5.5 V10.5 A4,4 0 0 1 8,10.5 V5.5 A4,4 0 0 1 12,1.5 Z M4.5,11 A7.5,7.5 0 0 0 19.5,11 M12,18.5 V22.5 M8.5,22.5 H15.5"),
        Stroke = Tokens.Brush("Muted"),
        StrokeThickness = 1.95,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };

    /// A round button with the mic (D-315). The ring behind it is tinted red and follows the sound.
    private Button MicButton()
    {
        _ring.RenderTransform = _ringScale;
        var hover = new Ellipse { Width = 26, Height = 26, Fill = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var icon = new Canvas { Width = 24, Height = 24 };
        icon.Children.Add(_micIcon);
        var face = new Grid { Width = 26, Height = 26 };
        face.Children.Add(_ring);
        face.Children.Add(_micBack);
        face.Children.Add(hover);
        face.Children.Add(new Viewbox { Width = 16, Height = 16, Child = icon });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        var button = new Button
        {
            Content = face,
            ToolTip = Strings.VoiceButtonTip,
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(10, -1, 0, -1),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Template = new ControlTemplate(typeof(Button)) { VisualTree = presenter },
        };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => VoicePressed?.Invoke();
        return button;
    }

    /// A small square button with a square in it, as in the mockup of a turn that works.
    private static Button StopButton(Action click)
    {
        var square = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(2),
            Background = Tokens.Brush("Ink"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hover = new Border { CornerRadius = new CornerRadius(8), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid { Width = 26, Height = 26 };
        face.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x0B, 0xFF, 0xFF, 0xFF)),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
        });
        face.Children.Add(hover);
        face.Children.Add(square);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        var button = new Button
        {
            Content = face,
            ToolTip = Strings.TerminalStop,
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(10, -1, 0, -1),
            VerticalAlignment = VerticalAlignment.Top,
            Template = new ControlTemplate(typeof(Button)) { VisualTree = presenter },
        };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => click();
        return button;
    }
}
