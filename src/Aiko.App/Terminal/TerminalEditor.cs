using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Aiko.Core.Terminal;
using Path = System.IO.Path;

namespace Aiko.App;

/// What the editor needs to know of the open session.
sealed record EditorSession(string Id, SessionKind Kind, string Folder, char Separator, string PicturesFolder);

/// The editor under the terminal (D-354, D-362, D-369): the text is typed and fixed here, with the
/// mouse and Ctrl+Z, and goes to the program as one message. An empty editor lets the keys that
/// answer a program through; what a key does is the core's (EditorKeys). Pictures come from the
/// clipboard or a drop and wait above the text; voice writes into the same field.
///
/// One editor serves every session: the text, the pictures and the history of each are kept, so a
/// switch to another session and back finds them as they were.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalEditor : UserControl, IDictationField
{
    /// Pictures larger than this are made smaller before they go: Claude Code takes up to about
    /// 5 MB of a picture, and a screenshot of a large screen is often more as PNG.
    private const int LargestSide = 1800;

    /// The field grows with its text up to this height, then scrolls.
    private const double MostHeight = 180;

    private static readonly string[] PictureFiles = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    private sealed class Draft
    {
        public string Text = "";
        public int Caret;
        public List<string> Pictures = [];
        public CommandHistory History = CommandHistory.Empty;
        public bool Passing;
    }

    private readonly Dictionary<string, Draft> _drafts = [];
    private readonly TextBox _text = new();
    private readonly TextBlock _placeholder;
    private readonly TextBlock _dictation = new() { TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly WrapPanel _pictures = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Border _top = new() { Height = 1, Background = Tokens.Brush("Hairline") };
    private readonly TextBlock _hint = TerminalPanel.Mono("", "Muted");
    private readonly StackPanel _voiceLine = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly StackPanel _wave = Wave();
    private readonly TextBlock _voiceWord = TerminalPanel.Mono("", "Destructive");
    private readonly TextBlock _voiceWhere = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _voiceCancel = TerminalPanel.Mono("", "Muted");
    private readonly Border _notice;
    private readonly TextBlock _noticeText = TerminalPanel.Text("", "TextNumber", "Ink", FontWeights.Normal);
    private readonly TextBlock _noticeLink = TerminalPanel.Text("", "TextNumber", "Ink", FontWeights.Normal);
    private readonly Button _mic;
    private readonly Ellipse _micBack = new() { Width = 24, Height = 24, Fill = Brushes.Transparent };
    private readonly Shape _micIcon = MicIcon();
    private readonly ChoicePopup _choices;

    private EditorSession? _session;
    private Draft _draft = new();
    private VoiceNotice _shownNotice;
    private bool _voiceActive;
    private bool _dictating;
    private string _before = "";
    private string _after = "";

    // What the list over the editor offers: files of "@" or paths of Tab.
    private MentionQuery? _mention;
    private IReadOnlyList<string> _offered = [];
    private bool _offersPaths;

    public TerminalEditor()
    {
        Focusable = false;
        Background = Tokens.Brush("Background");

        _text.FontFamily = Tokens.Get<FontFamily>("Mono");
        _text.FontSize = 12.5;
        _text.Foreground = Tokens.Brush("Ink");
        _text.CaretBrush = Tokens.Brush("Ink");
        _text.Background = Brushes.Transparent;
        _text.BorderThickness = new Thickness(0);
        _text.Padding = new Thickness(0);
        _text.AcceptsReturn = true;
        _text.AcceptsTab = false;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.MaxHeight = MostHeight;
        _text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _text.AllowDrop = true;
        _text.PreviewKeyDown += OnKey;
        _text.PreviewTextInput += OnTextInput;
        _text.TextChanged += (_, _) => TextChanged();
        _text.SelectionChanged += (_, _) => Offer();
        _text.LostKeyboardFocus += (_, _) => _choices!.Close();
        _text.GotKeyboardFocus += (_, _) => _top.Background = Tokens.Brush("InputLine");
        _text.LostKeyboardFocus += (_, _) => _top.Background = Tokens.Brush("Hairline");
        CommandManager.AddPreviewCanExecuteHandler(_text, OnCanPaste);
        CommandManager.AddPreviewExecutedHandler(_text, OnPasteCommand);
        _text.PreviewDragOver += OnDragOver;
        _text.PreviewDrop += OnDrop;

        _placeholder = TerminalPanel.Text(Strings.TerminalEditorPlaceholder, "TextNumber", "Muted", FontWeights.Normal);
        _placeholder.FontFamily = _text.FontFamily;
        _placeholder.FontSize = _text.FontSize;
        _placeholder.IsHitTestVisible = false;
        _placeholder.VerticalAlignment = VerticalAlignment.Top;
        // The text box keeps 2 units before its text; the hint stands where the text will.
        _placeholder.Margin = new Thickness(2, 0, 0, 0);
        _dictation.Margin = new Thickness(2, 0, 2, 0);
        _dictation.FontFamily = _text.FontFamily;
        _dictation.FontSize = _text.FontSize;

        var words = new Grid();
        words.Children.Add(_text);
        words.Children.Add(_placeholder);
        words.Children.Add(_dictation);

        var sign = TerminalPanel.Mono("›", "Muted");
        sign.FontSize = _text.FontSize;
        sign.VerticalAlignment = VerticalAlignment.Top;
        sign.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(sign, Dock.Left);

        _mic = MicButton();
        DockPanel.SetDock(_mic, Dock.Right);

        var field = new DockPanel();
        field.Children.Add(sign);
        field.Children.Add(_mic);
        field.Children.Add(words);

        _hint.TextWrapping = TextWrapping.Wrap;
        _voiceWord.Margin = new Thickness(8, 0, 10, 0);
        _voiceWhere.Margin = new Thickness(0, 0, 10, 0);
        _voiceLine.Children.Add(_wave);
        _voiceLine.Children.Add(_voiceWord);
        _voiceLine.Children.Add(_voiceWhere);
        _voiceLine.Children.Add(_voiceCancel);
        var under = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        under.Children.Add(_hint);
        under.Children.Add(_voiceLine);

        // When voice input cannot start, a line above the field says why and how to fix it.
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
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed,
            Child = noticeWords,
        };

        var inside = new StackPanel { Margin = new Thickness(14, 10, 14, 8) };
        inside.Children.Add(_notice);
        inside.Children.Add(_pictures);
        inside.Children.Add(field);
        inside.Children.Add(under);

        var all = new DockPanel { Cursor = Cursors.IBeam };
        DockPanel.SetDock(_top, Dock.Top);
        all.Children.Add(_top);
        all.Children.Add(inside);
        all.MouseLeftButtonDown += (_, _) => FocusText();
        Content = all;

        _choices = new ChoicePopup(this);
        _choices.Clicked += Choose;
        ShowHint();
    }

    /// Keys and text for the program as they are.
    public event Action<string>? Passed;

    /// A message: the writes in their order (EditorSend).
    public event Action<IReadOnlyList<string>>? Sent;

    public event Action? VoicePressed;
    public event Action? VoiceCancelled;
    public event Action? VoiceSettingsAsked;

    /// What is selected in the terminal now, or null: Ctrl+C in an empty editor copies it, as
    /// Windows Terminal does, rather than stopping the program.
    public Func<string?>? TerminalSelection { get; set; }

    public void FocusText()
    {
        if (!_text.IsKeyboardFocused)
        {
            _text.Focus();
            Keyboard.Focus(_text);
        }
    }

    /// The session the editor writes for. Its text and pictures come back as they were left.
    public void Show(EditorSession session)
    {
        if (_session?.Id == session.Id)
        {
            _session = session;
            return;
        }

        if (_dictating)
        {
            VoiceCancelled?.Invoke();
        }

        Keep();
        _session = session;
        _draft = _drafts.TryGetValue(session.Id, out var draft) ? draft : _drafts[session.Id] = new Draft();
        _text.Text = _draft.Text;
        _text.CaretIndex = Math.Min(_draft.Caret, _text.Text.Length);
        _choices.Close();
        DrawPictures();
        ShowHint();
    }

    /// A session was closed: what was typed for it goes.
    public void Forget(string id) => _drafts.Remove(id);

    /// Ctrl+Shift+Space, taken by the window before any field sees it (D-354).
    public void PressVoice() => VoicePressed?.Invoke();

    /// Text as if it was typed, and pictures as if pasted, for the snapshot door.
    internal void TypedForSnapshot(string text, int pictures)
    {
        _text.Text = text;
        for (var at = 0; at < pictures; at++)
        {
            _draft.Pictures.Add($"picture-{at + 1}.png");
        }

        DrawPictures();
        ShowHint();
    }

    private void Keep()
    {
        _draft.Text = _text.Text;
        _draft.Caret = _text.CaretIndex;
    }

    // ---- keys ----

    private EditorState State()
    {
        var line = _text.GetLineIndexFromCharacterIndex(_text.CaretIndex);
        var empty = _text.Text.Length == 0 && _draft.Pictures.Count == 0;
        return new EditorState(
            _session?.Kind ?? SessionKind.Claude,
            empty,
            line <= 0,
            line < 0 || line >= _text.LineCount - 1,
            _draft.Passing);
    }

    private void OnKey(object sender, KeyEventArgs e) =>
        e.Handled = Press(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);

    /// A key with its modifiers; true when the editor used it up. The live keys come here, and so
    /// do the keys of the check door (--try-editor), which has no keyboard.
    internal bool Press(Key key, ModifierKeys modifiers)
    {
        if (_dictating)
        {
            if (key == Key.Escape)
            {
                VoiceCancelled?.Invoke();
                return true;
            }

            if (key == Key.Enter)
            {
                // The second press of Enter ends the recording: the words go into the field, not out.
                VoicePressed?.Invoke();
                return true;
            }

            return false;
        }

        var control = modifiers.HasFlag(ModifierKeys.Control);
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        var alt = modifiers.HasFlag(ModifierKeys.Alt);

        if (_choices.IsOpen && OnChoiceKey(key))
        {
            return true;
        }

        EditorKey? editorKey = (key, control, alt) switch
        {
            (Key.Up, false, false) => EditorKey.Up,
            (Key.Down, false, false) => EditorKey.Down,
            (Key.Left, false, false) => EditorKey.Left,
            (Key.Right, false, false) => EditorKey.Right,
            (Key.Enter, false, false) => EditorKey.Enter,
            (Key.Escape, false, false) => EditorKey.Escape,
            (Key.Tab, false, false) => EditorKey.Tab,
            (Key.Back, false, false) => EditorKey.Backspace,
            (Key.C, true, false) when !shift => EditorKey.Interrupt,
            _ => null,
        };
        if (editorKey is not { } pressed)
        {
            return false;
        }

        if (pressed == EditorKey.Interrupt && _text.Text.Length == 0 && TerminalSelection?.Invoke() is { Length: > 0 } selected)
        {
            CopyText(selected);
            return true;
        }

        return Act(EditorKeys.For(State(), pressed, shift));
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Length == 1 && Typed(e.Text[0]);

    /// A digit, "/" or a letter while the editor passes keys to a menu of the program. True when it
    /// went to the program; otherwise the text box takes it.
    internal bool Typed(char character)
    {
        if (_dictating)
        {
            return false;
        }

        var press = EditorKeys.For(State(), EditorKey.Character, false, character);
        return press.Action == EditorAction.Pass && Act(press);
    }

    // ---- doors for --try-editor: what the check reads and does without a keyboard ----

    /// The check door runs in a window that never has the keyboard.
    internal bool CheckWithoutFocus { get; set; }

    internal string TextForCheck => _text.Text;

    internal void ClearForCheck() => _text.Clear();

    internal bool ChoicesOpenForCheck => _choices.IsOpen;

    internal IReadOnlyList<string> OfferedForCheck => _offered;

    /// A letter the editor does not take goes into the text, as a key would put it there.
    internal void TypeForCheck(string text)
    {
        foreach (var character in text)
        {
            if (!Typed(character))
            {
                var at = _text.CaretIndex;
                _text.Text = _text.Text.Insert(at, character.ToString());
                _text.CaretIndex = at + 1;
            }
        }
    }

    internal void DropForCheck(IReadOnlyList<string> paths) => Take(paths);

    /// Does what the core said. True when the key was used up.
    private bool Act(EditorPress press)
    {
        _draft.Passing = press.Passing;
        ShowHint();
        switch (press.Action)
        {
            case EditorAction.Pass:
                Passed?.Invoke(press.Sequence);
                return true;
            case EditorAction.Send:
                Send();
                return true;
            case EditorAction.NewLine:
                var at = _text.CaretIndex;
                _text.Text = _text.Text.Insert(at, Environment.NewLine);
                _text.CaretIndex = at + Environment.NewLine.Length;
                return true;
            case EditorAction.HistoryBack:
                Walk(_draft.History.Back(_text.Text));
                return true;
            case EditorAction.HistoryForward:
                Walk(_draft.History.Forward());
                return true;
            case EditorAction.Complete:
                Complete();
                return true;
            default:
                return false;
        }
    }

    private void Walk((CommandHistory History, string? Text) step)
    {
        _draft.History = step.History;
        if (step.Text is { } text)
        {
            _text.Text = text;
            _text.CaretIndex = text.Length;
        }
    }

    private void Send()
    {
        var text = _text.Text;
        var writes = EditorSend.Writes(text, _draft.Pictures);
        if (writes.Count == 0)
        {
            return;
        }

        if (_session?.Kind == SessionKind.Shell)
        {
            _draft.History = _draft.History.Sent(text);
        }

        _text.Clear();
        _draft.Pictures.Clear();
        DrawPictures();
        Sent?.Invoke(writes);
    }

    // ---- the list over the editor ----

    private bool OnChoiceKey(Key key)
    {
        switch (key)
        {
            case Key.Up or Key.Down:
                _choices.Move(key == Key.Down ? 1 : -1);
                return true;
            case Key.Enter or Key.Tab:
                Choose(_choices.Lit);
                return true;
            case Key.Escape:
                _choices.Close();
                return true;
            default:
                return false;
        }
    }

    private void TextChanged()
    {
        _draft.Text = _text.Text;
        if (_text.Text.Length > 0)
        {
            _draft.Passing = false;
        }

        _placeholder.Visibility = _text.Text.Length == 0 && !_dictating ? Visibility.Visible : Visibility.Collapsed;
        _offersPaths = false;
        ShowHint();
        Offer();
    }

    /// "@" in a session of Claude Code offers the files of its folder (FileMention).
    private void Offer()
    {
        if (_offersPaths)
        {
            return;
        }

        _mention = _session?.Kind == SessionKind.Claude && (_text.IsKeyboardFocused || CheckWithoutFocus) ? FileMention.Query(_text.Text, _text.CaretIndex) : null;
        if (_mention is not { } mention || _session is null)
        {
            _choices.Close();
            return;
        }

        var files = ProjectFiles.Get(_session.Folder, Offer);
        if (files is null)
        {
            return;
        }

        _offered = FileMention.Matches(files, mention.Text);
        _choices.Show([.. _offered.Select(path => (path, FileMention.Lit(path, mention.Text).Start, FileMention.Lit(path, mention.Text).Length))], CaretLeft());
    }

    private void Complete()
    {
        if (_session is null)
        {
            return;
        }

        var done = PathCompletion.Complete(_text.Text, _text.CaretIndex, _session.Folder, _session.Separator, List);
        if (done is null)
        {
            return;
        }

        _text.Text = done.Text;
        _text.CaretIndex = done.Caret;
        _offered = done.Choices;
        _offersPaths = done.Choices.Count > 0;
        if (_offersPaths)
        {
            _choices.Show([.. done.Choices.Select(choice => (choice, 0, 0))], CaretLeft());
        }
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= _offered.Count)
        {
            return;
        }

        var choice = _offered[index];
        _choices.Close();
        if (_offersPaths)
        {
            // The choice takes the place of the word before the caret, then Tab goes on from it.
            _offersPaths = false;
            var caret = _text.CaretIndex;
            var start = caret;
            while (start > 0 && !char.IsWhiteSpace(_text.Text[start - 1]) && _text.Text[start - 1] is not ('/' or '\\' or '"'))
            {
                start--;
            }

            _text.Text = _text.Text[..start] + choice + _text.Text[caret..];
            _text.CaretIndex = start + choice.Length;
        }
        else if (_mention is { } mention)
        {
            var (text, caret) = FileMention.Chosen(_text.Text, mention, _text.CaretIndex, choice);
            _text.Text = text;
            _text.CaretIndex = caret;
        }

        FocusText();
    }

    private double CaretLeft()
    {
        var place = _text.GetRectFromCharacterIndex(_text.CaretIndex);
        return double.IsFinite(place.Left) ? Math.Max(0, place.Left) : 0;
    }

    private static IReadOnlyList<FolderEntry> List(string folder)
    {
        var path = folder.StartsWith('~') ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + folder[1..] : folder;
        try
        {
            var info = new DirectoryInfo(path);
            return info.Exists
                ? [.. info.EnumerateFileSystemInfos().Take(2000).Select(entry => new FolderEntry(entry.Name, entry is DirectoryInfo))]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    // ---- the line under the field ----

    private void ShowHint()
    {
        var state = State();
        var shell = state.Kind == SessionKind.Shell;
        var words = state.Passing && state.Empty ? Strings.TerminalEditorHintPassing
            : state.Empty ? (shell ? Strings.TerminalEditorHintShellEmpty : Strings.TerminalEditorHintEmpty)
            : shell ? Strings.TerminalEditorHintShellText
            : Strings.TerminalEditorHintText;

        // **…** marks a key: it is written white, the rest grey.
        _hint.Inlines.Clear();
        var parts = words.Split("**");
        for (var at = 0; at < parts.Length; at++)
        {
            _hint.Inlines.Add(at % 2 == 1 ? new Run(parts[at]) { Foreground = Tokens.Brush("Ink") } : new Run(parts[at]));
        }
    }

    // ---- pictures and files ----

    /// A TextBox allows Paste only when the clipboard holds text. A screenshot from Win+Shift+S
    /// holds only a picture.
    private static void OnCanPaste(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste && (Clipboard.ContainsImage() || Clipboard.ContainsFileDropList()))
        {
            e.CanExecute = true;
            e.Handled = true;
        }
    }

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
            FocusText();
        }
    }

    /// A picture file goes as a picture; any other file or folder is its path in the text, in
    /// quotes when it has a space.
    private void Take(IReadOnlyList<string> paths)
    {
        if (_dictating)
        {
            return;
        }

        var words = new List<string>();
        foreach (var path in paths)
        {
            if (PictureFiles.Contains(Path.GetExtension(path).ToLowerInvariant()) && Load(path) is { } picture)
            {
                Attach(picture);
            }
            else
            {
                words.Add(path);
            }
        }

        if (words.Count > 0)
        {
            var (text, caret) = FileMention.Dropped(_text.Text, _text.CaretIndex, words);
            _text.Text = text;
            _text.CaretIndex = caret;
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

    /// The picture is written as a PNG into the folder of the session; its path goes to Claude Code.
    private void Attach(BitmapSource picture)
    {
        if (_session is null)
        {
            return;
        }

        var scale = Math.Min(1.0, LargestSide / (double)Math.Max(picture.PixelWidth, picture.PixelHeight));
        BitmapSource sized = scale < 1 ? new TransformedBitmap(picture, new ScaleTransform(scale, scale)) : picture;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sized));

        try
        {
            Directory.CreateDirectory(_session.PicturesFolder);
            var path = Path.Combine(_session.PicturesFolder, $"picture-{Guid.NewGuid().ToString("N")[..8]}.png");
            using (var file = File.Create(path))
            {
                encoder.Save(file);
            }

            _draft.Pictures.Add(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        DrawPictures();
        ShowHint();
    }

    /// The pictures as small labels above the text (D-362): "picture 1", with a cross.
    private void DrawPictures()
    {
        _pictures.Children.Clear();
        for (var at = 0; at < _draft.Pictures.Count; at++)
        {
            var index = at;
            var cross = TerminalPanel.Line("M0,0 L6,6 M6,0 L0,6");
            cross.Margin = new Thickness(8, 0, 0, 0);
            cross.Cursor = Cursors.Hand;
            cross.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _draft.Pictures.RemoveAt(index);
                DrawPictures();
                ShowHint();
            };

            var square = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), Background = Tokens.Brush("Muted"), Margin = new Thickness(0, 0, 6, 0) };
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(square);
            label.Children.Add(TerminalPanel.Mono(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TerminalEditorPicture, at + 1), "Ink"));
            label.Children.Add(cross);
            _pictures.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Tokens.Brush("Raised"),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Child = label,
                ToolTip = _draft.Pictures[at],
            });
        }

        _pictures.Visibility = _draft.Pictures.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program holds the clipboard; the person copies again.
        }
    }

    // ---- voice (IDictationField) ----

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
        (_before, _after) = DictationText.Around(_text.Text, _text.CaretIndex);
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
        if (words is { Length: > 0 })
        {
            var (text, caret) = DictationText.Placed(_before, words, _after);
            _text.Text = text;
            _text.CaretIndex = caret;
        }

        _placeholder.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
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
