using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core.Terminal;
using Ellipse = System.Windows.Shapes.Ellipse;
using Shape = System.Windows.Shapes.Path;

namespace Aiko.App;

/// The field of a conversation (D-294, D-302): the text, the pictures that go with it, the stop
/// button and the line "mode · model · environment" under it.
///
/// Enter sends, Shift+Enter is a new line, Esc stops the work, Shift+Tab changes the mode, the
/// arrows walk through what was sent in this session. While a card of permission or of a plan
/// waits, Enter in an empty field allows it and Esc denies it (D-323). A picture comes from the clipboard or from
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

    private const double WaitingOpacity = 0.6;

    /// The room between the text and the frame of the field, and its height before it scrolls.
    private const double FieldSide = 14;
    private const double FieldHeight = 200;

    /// One notch of the wheel, 120, scrolls the field by 40 pixels.
    private const double WheelStep = 3;

    private static readonly string[] PictureFiles = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    private readonly TextBox _text = new();
    private readonly ScrollViewer _scroll;
    private readonly WrapPanel _pictures = new() { Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
    private readonly List<AttachedImage> _attached = [];
    private readonly Button _stop;
    private readonly TextBlock _mode = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _model = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _where = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _modeDot = TerminalPanel.Mono(" · ", "Muted");
    private readonly ContentControl _folderSlot = new() { Focusable = false };
    private bool _folderShown;
    private string? _folderKey;

    private static readonly Brush RecordingTint = Frozen(Color.FromArgb(0x33, 0xFF, 0x64, 0x67));

    private readonly Button _mic;
    private readonly Ellipse _micBack = new() { Width = 26, Height = 26, Fill = Brushes.Transparent };
    private readonly Shape _micIcon = MicIcon();
    private readonly StackPanel _wave = Wave();
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
    private readonly Dictionary<string, string> _drafts = [];
    private string _session = "";
    private int _back = -1;
    private string _draft = "";
    private bool _busy;
    private bool _cardWaits;

    /// The menu of "/" and what it offers. Esc shuts it until the text changes again.
    private readonly SlashMenuView _menu = new();
    private IReadOnlyList<SlashCommand> _commands = [];
    private IReadOnlyList<SlashMatch> _matches = [];
    private IReadOnlyList<string> _files = [];
    private MentionQuery? _mention;
    private string _folder = "";
    private bool _menuShut;
    private bool _menuWithoutFocus;

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
        _text.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _text.VerticalContentAlignment = VerticalAlignment.Top;
        _text.PreviewKeyDown += OnKey;
        CommandManager.AddPreviewCanExecuteHandler(_text, OnCanPaste);
        CommandManager.AddPreviewExecutedHandler(_text, OnPasteCommand);

        _stop = StopButton(() => StopAsked?.Invoke());
        _mic = MicButton();

        var placeholder = _placeholder = TerminalPanel.Text(Strings.TerminalDescribeTask, "TextName", "Muted", FontWeights.Normal);
        placeholder.Margin = new Thickness(2, 1, 0, 0);
        placeholder.VerticalAlignment = VerticalAlignment.Top;
        placeholder.IsHitTestVisible = false;
        _text.TextChanged += (_, _) => ShowPlaceholder();
        _text.TextChanged += (_, _) =>
        {
            _menuShut = false;
            ShowMenu();
        };
        _text.SelectionChanged += (_, _) => ShowMenu();
        _text.LostKeyboardFocus += (_, _) => _menu.Close();
        _menu.Picked += Pick;

        // The mic button is the last at the right; the stop button stands left of it, where the
        // paper clip will be (D-308, D-315).
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, FieldSide, 0),
        };
        buttons.Children.Add(_stop);
        buttons.Children.Add(_mic);
        _dictation.FontFamily = _text.FontFamily;
        _dictation.FontSize = _text.FontSize;
        _dictation.Margin = new Thickness(2, 0, 2, 0);
        var words = new Grid { Margin = new Thickness(0, 0, FieldSide, 0) };
        words.Children.Add(_text);
        words.Children.Add(placeholder);
        words.Children.Add(_dictation);
        buttons.SizeChanged += (_, size) => words.Margin = new Thickness(0, 0, FieldSide + size.NewSize.Width, 0);

        // The text does not scroll itself: this does, over the whole width of the field, so its
        // bar stands at the right edge, right of the buttons (AIKO-245). The window gives every
        // scroll viewer the thin bar of QuietScroll.
        _scroll = new ScrollViewer
        {
            MaxHeight = FieldHeight,
            Focusable = false,
            Content = words,
        };
        _scroll.PreviewMouseWheel += OnFieldWheel;
        // A new line is laid out after the caret moved, so the caret is checked again then.
        _text.SelectionChanged += (_, _) => KeepCaretInView();
        _scroll.ScrollChanged += (_, e) =>
        {
            if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0)
            {
                KeepCaretInView();
            }
        };

        var field = new Grid();
        field.Children.Add(_scroll);
        field.Children.Add(buttons);

        var inside = new StackPanel();
        _pictures.Margin = new Thickness(0, 0, FieldSide, 8);
        inside.Children.Add(_pictures);
        inside.Children.Add(field);

        var compose = new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(FieldSide, 12, 0, 12),
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
        // The line under the field, in the order of the approved prototype (D-342): folder, model,
        // mode, environment. Until the first message the folder is a quiet list to choose from.
        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 6, 0, 14) };
        _folderSlot.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(_folderSlot);
        status.Children.Add(_model);
        status.Children.Add(_modeDot);
        status.Children.Add(_mode);
        status.Children.Add(_where);
        status.Children.Add(_wave);
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

        // The menu stands over the feed, right above the field, and moves nothing when it opens:
        // a canvas of no height lets it reach up out of its place.
        var menuPlace = new Canvas { Height = 0 };
        menuPlace.Children.Add(_menu);
        Canvas.SetBottom(_menu, -4);
        menuPlace.SizeChanged += (_, size) => _menu.Width = size.NewSize.Width;

        var all = new StackPanel();
        all.Children.Add(_notice);
        all.Children.Add(menuPlace);
        all.Children.Add(compose);
        all.Children.Add(status);
        Content = all;
    }

    /// Text as if it was typed, with the caret at its end. For the snapshot door.
    public void Typed(string text)
    {
        _text.Text = text;
        _text.CaretIndex = text.Length;
    }

    /// Text as if it was typed into a field with the keyboard in it, so the menu of "/" opens.
    /// For the snapshot door, whose window never has the keyboard.
    public void TypedWithMenu(string text)
    {
        _menuWithoutFocus = true;
        Typed(text);
    }

    /// The text box would take the wheel for itself and not scroll; the field scrolls instead.
    private void OnFieldWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroll.ScrollableHeight <= 0)
        {
            return;
        }

        _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset - e.Delta / WheelStep);
        e.Handled = true;
    }

    /// Typing and the arrows move the caret; the field follows it, as the text box did when it
    /// scrolled itself.
    private void KeepCaretInView()
    {
        if (_scroll.ScrollableHeight <= 0)
        {
            return;
        }

        var caret = _text.GetRectFromCharacterIndex(_text.CaretIndex);
        if (caret.IsEmpty)
        {
            return;
        }

        var top = _text.TranslatePoint(caret.TopLeft, _scroll).Y;
        if (top < 0)
        {
            _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset + top);
        }
        else if (top + caret.Height > _scroll.ViewportHeight)
        {
            _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset + top + caret.Height - _scroll.ViewportHeight);
        }
    }

    public event Action<string, IReadOnlyList<AttachedImage>>? Sent;
    public event Action? StopAsked;

    /// Enter in an empty field and Esc while a card waits (D-323).
    public event Action? AllowAsked;
    public event Action? DenyAsked;
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
        if (!line.Wave && _wave.Visibility == Visibility.Visible)
        {
            foreach (FrameworkElement bar in _wave.Children)
            {
                bar.Height = BarLow;
            }
        }

        _wave.Visibility = line.Wave ? Visibility.Visible : Visibility.Collapsed;

        var usual = _voiceActive ? Visibility.Collapsed : Visibility.Visible;
        var voice = _voiceActive ? Visibility.Visible : Visibility.Collapsed;
        _mode.Visibility = _modeDot.Visibility = _model.Visibility = _where.Visibility = usual;
        _folderSlot.Visibility = _folderShown ? usual : Visibility.Collapsed;
        _voiceWord.Visibility = _voiceWhere.Visibility = _voiceCancel.Visibility = voice;
        _voiceWord.Text = line.Word switch
        {
            VoiceWord.Warming => Strings.VoiceWarming,
            VoiceWord.Listening => string.Format(Strings.VoiceListening, line.Time),
            VoiceWord.Finishing => Strings.VoiceFinishing,
            _ => "",
        };
        // "listening 0:04" is red, like the button and the wave: the three say one thing.
        _voiceWord.Foreground = Tokens.Brush(line.Word switch
        {
            VoiceWord.Warming => "Muted",
            VoiceWord.Listening => "Destructive",
            _ => "Ink",
        });
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

    /// The loudness of the last moment, from 0 to 1. The wave moves one bar to the left and the
    /// new one comes in at the right, so the line shows the last 1.3 s (D-319).
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
        // Hidden, the field would lose the keyboard, and the next Ctrl+Space would go nowhere (D-328).
        _text.Opacity = 0;
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
        _text.Opacity = 1;
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
    public void Show(string session, bool busy, bool cardWaits, PermissionMode mode, ModelLabel model, string environment)
    {
        _cardWaits = cardWaits;
        if (session != _session)
        {
            // A recording belongs to the session it started in.
            if (_voiceActive)
            {
                VoiceCancelled?.Invoke();
            }

            // Each session keeps its own draft while the window lives (LobeChat, Zed).
            _drafts[_session] = _text.Text;
            _session = session;
            _text.Text = _drafts.GetValueOrDefault(session, "");
            _text.CaretIndex = _text.Text.Length;
            _attached.Clear();
            DrawPictures();
            _back = -1;
        }

        _busy = busy;
        _stop.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _mode.Text = PermissionModes.Name(mode);
        DrawModel(model);
        _modeDot.Text = _folderShown || model.Text.Length > 0 ? " · " : "";
        _where.Text = " · " + environment;
    }

    /// Raised when a folder of the list under the field is picked, and when "Choose a folder"
    /// is: before the first message of a new session only.
    public event Action<int>? FolderPicked;
    public event Action? OtherFolderAsked;

    /// The folder at the start of the line under the field. In a new session (draft) it is a quiet
    /// list of the folders to choose from; once the session started, its name only.
    public void ShowFolder(string folder, FormView? draft)
    {
        // The list is built again only when what it shows changed: a draw while it is open would
        // close it.
        var key = draft is { Folder: >= 0 } ? $"draft|{draft.Folder}|{draft.Folders.Count}|{draft.Folders[draft.Folder].Path}" : folder;
        if (key == _folderKey)
        {
            return;
        }

        _folderKey = key;
        if (draft is { Folder: >= 0 })
        {
            var list = new DropDown
            {
                Items = [.. draft.Folders.Select(TerminalPanel.FolderLabel), Strings.TerminalChooseFolder],
                Short = [.. draft.Folders.Select(choice => choice.Name)],
                Headers = NewSessionChoices.Headers(draft.Folders).ToDictionary(
                    header => header.Before,
                    header => header.Group == FolderGroup.Recent ? Strings.TerminalFoldersRecent : Strings.TerminalFoldersBound),
                SelectedIndex = draft.Folder,
                ToolTip = draft.Folders[draft.Folder].Path,
            };
            list.MakeQuiet();
            list.Picked += index =>
            {
                if (index < draft.Folders.Count)
                {
                    FolderPicked?.Invoke(index);
                }
                else
                {
                    OtherFolderAsked?.Invoke();
                }
            };
            _folderSlot.Content = list;
            _folderShown = true;
        }
        else if (folder.Length > 0)
        {
            var name = TerminalPanel.Mono(SessionName.FolderName(folder), "Muted");
            name.Margin = new Thickness(4, 0, 0, 0);
            name.ToolTip = folder;
            _folderSlot.Content = name;
            _folderShown = true;
        }
        else
        {
            _folderSlot.Content = null;
            _folderShown = false;
        }

        _folderSlot.Visibility = _folderShown && !_voiceActive ? Visibility.Visible : Visibility.Collapsed;
    }

    /// A model Claude Code has not named yet is quieter than the rest of the line.
    private void DrawModel(ModelLabel model)
    {
        _model.Inlines.Clear();
        if (model.Text.Length == 0)
        {
            return;
        }

        var name = new Run(model.Text);
        if (model.Waiting)
        {
            var quiet = Tokens.Brush("Muted").Clone();
            quiet.Opacity = WaitingOpacity;
            name.Foreground = quiet;
        }

        if (_folderShown)
        {
            _model.Inlines.Add(new Run(" · "));
        }

        _model.Inlines.Add(name);
    }

    // ---- the keys ----

    /// The commands of the environment of the session, for the menu of "/".
    public void SetCommands(IReadOnlyList<SlashCommand> commands)
    {
        if (ReferenceEquals(commands, _commands))
        {
            return;
        }

        _commands = commands;
        ShowMenu();
    }

    /// The folder of the session, for the files of "@".
    public void SetFolder(string folder) => _folder = folder;

    /// How much room the feed leaves above the field; the menu keeps 80 px of it in view.
    public void FitMenu(double room) => _menu.Fit(room);

    /// The menu is open while the caret stands in a "/" word at the very start of the field, or in
    /// an "@" word anywhere.
    private void ShowMenu()
    {
        _matches = [];
        _files = [];
        _mention = null;
        if (_dictating || _menuShut || !(_text.IsKeyboardFocused || _menuWithoutFocus))
        {
            _menu.Close();
            return;
        }

        var text = _text.Text;
        var caret = _text.CaretIndex;
        if (SlashMenu.Query(text, caret) is { } query)
        {
            _matches = SlashMenu.Matches(_commands, query);
            _menu.ShowCommands(_matches);
            return;
        }

        if (FileMention.Query(text, caret) is { } mention && ProjectFiles.Get(_folder, ShowMenu) is { } files)
        {
            _mention = mention;
            _files = FileMention.Matches(files, mention.Text);
            _menu.ShowFiles(_files, mention.Text);
            return;
        }

        _menu.Close();
    }

    /// A command takes the place of the "/" word, and what was typed after it stays; a file takes
    /// the place of the "@" word.
    private void Pick(int index)
    {
        var text = _text.Text;
        if (_mention is { } mention && index >= 0 && index < _files.Count)
        {
            var (result, caret) = FileMention.Chosen(text, mention, _text.CaretIndex, _files[index]);
            _text.Text = result;
            _text.CaretIndex = caret;
        }
        else if (index >= 0 && index < _matches.Count)
        {
            var end = text.IndexOfAny([' ', '\t', '\r', '\n']);
            var rest = end < 0 ? "" : text[end..].TrimStart(' ', '\t');
            var chosen = SlashMenu.Chosen(_matches[index].Command);
            _text.Text = chosen + rest;
            _text.CaretIndex = chosen.Length;
        }

        _menuShut = true;
        _menu.Close();
        _text.Focus();
    }

    /// While the menu is open, the arrows, Enter, Tab and Esc are its own. Whether the key was taken.
    private bool MenuKey(Key key, ModifierKeys modifiers)
    {
        if (!_menu.IsOpen || modifiers != ModifierKeys.None)
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
                _menu.Move(-1);
                return true;
            case Key.Down:
                _menu.Move(+1);
                return true;
            case Key.PageUp or Key.PageDown:
                _menu.MovePage(key == Key.PageDown ? 1 : -1);
                return true;
            case Key.Home or Key.End:
                _menu.MoveToEnd(last: key == Key.End);
                return true;
            // With no line lit, as after a typo, Enter goes to the field and sends the text as typed.
            case Key.Enter or Key.Tab when _menu.Highlighted >= 0:
                Pick(_menu.Highlighted);
                return true;
            case Key.Escape:
                _menuShut = true;
                _menu.Close();
                return true;
            default:
                return false;
        }
    }

    /// While an IME composes (the Japanese of the persona), WPF reports every key as
    /// Key.ImeProcessed, so Enter that ends a composition never sends the message.
    private void OnKey(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (MenuKey(e.Key, modifiers))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Space && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            VoicePressed?.Invoke();
        }
        else if ((e.Key == Key.Enter && (modifiers == ModifierKeys.None || _dictating)) || e.Key == Key.Escape)
        {
            e.Handled = Act(TalkKeys.For(e.Key == Key.Enter ? TalkKey.Enter : TalkKey.Escape, KeyState()));
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

    /// Ctrl+Space pressed in another place of the window: the words go into this field, so the
    /// keyboard comes back to it first (AIKO-311).
    public void PressVoice()
    {
        _text.Focus();
        VoicePressed?.Invoke();
    }

    /// Enter or Esc pressed in another place of the window, where the keyboard went after a
    /// click on the feed or the list. There they only cancel a recording and answer a waiting
    /// card (D-328). Whether the key was taken.
    public bool PressOutside(TalkKey key) => Act(TalkKeys.For(key, KeyState() with { Outside = true }));

    /// Shift+Tab pressed in another place of the window changes the mode, as in the field.
    public void AskMode() => ModeAsked?.Invoke();

    /// Ctrl+V pressed in another place of the window pastes into the field.
    public void PasteHere()
    {
        _text.Focus();
        ApplicationCommands.Paste.Execute(null, _text);
    }

    /// Letters typed in another place of the window go into the field at its caret. Not during a
    /// recording: its words are not typed over. Whether the letters were taken.
    public bool TypeHere(string text)
    {
        if (_dictating)
        {
            return false;
        }

        _text.Focus();
        _text.SelectedText = text;
        _text.CaretIndex = _text.SelectionStart + _text.SelectionLength;
        return true;
    }

    /// Whether the keys come from the field itself.
    public bool Owns(object source) => ReferenceEquals(source, _text);

    private TalkKeyState KeyState() =>
        new(_cardWaits, _text.Text.Trim().Length == 0 && _attached.Count == 0, _busy, _dictating, _voiceActive);

    /// Whether the key was taken.
    private bool Act(TalkKeyAction action)
    {
        switch (action)
        {
            case TalkKeyAction.Send:
                Send();
                break;
            case TalkKeyAction.Allow:
                AllowAsked?.Invoke();
                break;
            case TalkKeyAction.Deny:
                DenyAsked?.Invoke();
                break;
            case TalkKeyAction.Stop:
                StopAsked?.Invoke();
                break;
            case TalkKeyAction.CancelVoice:
                VoiceCancelled?.Invoke();
                break;
            case TalkKeyAction.None:
                return false;
        }

        return true;
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

    /// A TextBox allows Paste only when the clipboard holds text. A screenshot from Win+Shift+S
    /// holds only a picture, so without this Ctrl+V did nothing at all (check of 2026-10-15).
    private static void OnCanPaste(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste && (Clipboard.ContainsImage() || Clipboard.ContainsFileDropList()))
        {
            e.CanExecute = true;
            e.Handled = true;
        }
    }

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

    private const double BarLow = 2;
    private const double BarHigh = 14;

    /// Sixteen red bars, 2 px wide with 2 px between them; silence is a low flat line (D-319).
    private static StackPanel Wave()
    {
        var wave = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = BarHigh,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        for (var i = 0; i < 16; i++)
        {
            wave.Children.Add(new Border
            {
                Width = 2,
                Height = BarLow,
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 0, i < 15 ? 2 : 0, 0),
                Background = Tokens.Brush("Destructive"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return wave;
    }

    /// A round button with the mic, tinted red while it records (D-315).
    private Button MicButton()
    {
        var hover = new Ellipse { Width = 26, Height = 26, Fill = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var icon = new Canvas { Width = 24, Height = 24 };
        icon.Children.Add(_micIcon);
        var face = new Grid { Width = 26, Height = 26 };
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
