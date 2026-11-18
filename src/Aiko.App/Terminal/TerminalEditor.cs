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
sealed partial class TerminalEditor : UserControl, IDictationField
{
    /// The field grows with its text up to this height, then scrolls.
    private const double MostHeight = 180;

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
    private readonly TextBlock _hint = Widgets.Mono("", "Muted");
    private readonly StackPanel _voiceLine = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly StackPanel _wave = Wave();
    private readonly TextBlock _voiceWord = Widgets.Mono("", "Destructive");
    private readonly TextBlock _voiceWhere = Widgets.Mono("", "Muted");
    private readonly TextBlock _voiceCancel = Widgets.Mono("", "Muted");
    private readonly Border _notice;
    private readonly TextBlock _noticeText = Widgets.Text("", "TextBody", "Ink", FontWeights.Normal);
    private readonly TextBlock _noticeLink = Widgets.Text("", "TextBody", "Ink", FontWeights.Normal);
    private readonly Button _mic;
    private readonly Ellipse _micBack = new() { Width = 24, Height = 24, Fill = Brushes.Transparent };
    private readonly Shape _micIcon = MicIcon();
    private readonly ChoicePopup _choices;

    private EditorSession? _session;

    /// A command Claude Code proposed for the words in a command line (D-358), and the words it
    /// answers; null while there is none. The timer waits for a pause in the typing and stops.
    private string? _suggestion;
    private string? _suggestFor;

    /// The words Claude Code gave no command for: the line says so until they change.
    private string? _suggestFailedFor;
    private readonly System.Windows.Threading.DispatcherTimer _suggestPause = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private Draft _draft = new();

    // A session of Claude Code: the editor opened with Ctrl+G, or a recording that shows its words in
    // the editor and puts them into the line of Claude Code at its end (D-370).
    private bool _composing;
    private bool _strip;
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
        Background = Brushes.Transparent;

        _text.FontFamily = Tokens.Mono;
        _text.FontSize = Tokens.Get<double>("TextBody");
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
        _suggestPause.Tick += (_, _) => AskForSuggestion();
        _text.SelectionChanged += (_, _) => Offer();
        _text.LostKeyboardFocus += (_, _) => _choices!.Close();
        CommandManager.AddPreviewCanExecuteHandler(_text, OnCanPaste);
        CommandManager.AddPreviewExecutedHandler(_text, OnPasteCommand);
        _text.PreviewDragOver += OnDragOver;
        _text.PreviewDrop += OnDrop;

        _placeholder = Widgets.Text(Strings.TerminalEditorPlaceholder, "TextBody", "Muted", FontWeights.Normal);
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

        var sign = Widgets.Mono("›", "Muted");
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
        _notice = new PixelBorder
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
        all.Children.Add(inside);
        all.MouseLeftButtonDown += (_, _) => FocusText();
        Content = all;

        _choices = new ChoicePopup(this);
        _choices.Clicked += Choose;
        ShowHint();
    }

    /// Keys and text for the program as they are.
    public event Action<string>? Passed;

    /// Text and pictures for the line of Claude Code, without Enter (D-370).
    public event Action<IReadOnlyList<string>>? Inserted;

    /// The editor came on screen or left it: a command line always has it, a session of Claude
    /// Code only while it is opened with Ctrl+G or a recording goes on (D-370).
    public event Action? WantedChanged;

    public bool Wanted => _session?.Kind == SessionKind.Shell || _composing || _strip;

    /// Ctrl+G in a session of Claude Code opens the editor for a long text, and closes it again.
    public void Compose()
    {
        if (_session?.Kind != SessionKind.Claude || _dictating)
        {
            return;
        }

        _composing = !_composing;
        ShowHint();
        WantedChanged?.Invoke();
        if (_composing)
        {
            FocusText();
        }
    }

    private void Hide()
    {
        _composing = false;
        _choices.Close();
        ShowHint();
        WantedChanged?.Invoke();
    }

    /// A message: the writes in their order (EditorSend).
    public event Action<IReadOnlyList<string>>? Sent;

    /// Words in a command line that ask for a command, after a pause in the typing.
    public event Action<string>? SuggestionAsked;

    /// The command Claude Code proposed for these words; null when it gave none. It shows only while
    /// the words in the field are still the same.
    public void ShowSuggestion(string words, string? command)
    {
        if (_text.Text != words)
        {
            return;
        }

        _suggestFor = null;
        _suggestion = command;
        _suggestFailedFor = command is null ? words : null;
        ShowHint();
    }

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
        var wanted = Wanted;
        _session = session;
        _composing = false;
        _strip = false;
        _draft = _drafts.TryGetValue(session.Id, out var draft) ? draft : _drafts[session.Id] = new Draft();
        _text.Text = _draft.Text;
        _text.CaretIndex = Math.Min(_draft.Caret, _text.Text.Length);
        _choices.Close();
        DrawPictures();
        ShowHint();
        if (Wanted != wanted)
        {
            WantedChanged?.Invoke();
        }
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

    // ---- the line under the field ----

    private void ShowHint()
    {
        var state = State();
        var shell = state.Kind == SessionKind.Shell;
        var words = shell && _suggestion is { } proposed ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ShellSuggestion, proposed.Replace("**", "* *"))
            : shell && _suggestFor == _text.Text && _suggestFor is not null ? Strings.ShellSuggesting
            : shell && _suggestFailedFor == _text.Text && _suggestFailedFor is not null ? Strings.ShellSuggestFailed
            : state.Composing ? Strings.TerminalEditorHintCompose
            : state.Passing && state.Empty ? Strings.TerminalEditorHintPassing
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
}
