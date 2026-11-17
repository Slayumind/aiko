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

/// Doors for --try-editor in Aiko.Checks: what the check reads and does without a keyboard.
sealed partial class TerminalEditor
{
    /// The check door runs in a window that never has the keyboard.
    internal bool CheckWithoutFocus { get; set; }

    internal string TextForCheck => _text.Text;

    internal void ClearForCheck() => _text.Clear();

    internal bool ChoicesOpenForCheck => _choices.IsOpen;

    internal bool ComposingForCheck => _composing;

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

    /// Files dropped on the terminal of a command line, or while the editor is open.
    public void DropFiles(IReadOnlyList<string> paths)
    {
        Take(paths);
        FocusText();
    }

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
            case EditorAction.Insert:
                var inserts = EditorSend.Inserts(_text.Text, _draft.Pictures);
                _text.Clear();
                _draft.Pictures.Clear();
                DrawPictures();
                Hide();
                Inserted?.Invoke(inserts);
                return true;
            case EditorAction.Hide:
                Hide();
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
}
