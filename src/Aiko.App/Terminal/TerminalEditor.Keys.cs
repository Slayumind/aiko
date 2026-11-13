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

/// The keys of the editor: what Enter, Esc, the arrows and Tab do.
sealed partial class TerminalEditor
{
    private EditorState State()
    {
        var line = _text.GetLineIndexFromCharacterIndex(_text.CaretIndex);
        var empty = _text.Text.Length == 0 && _draft.Pictures.Count == 0;
        return new EditorState(
            _session?.Kind ?? SessionKind.Claude,
            empty,
            line <= 0,
            line < 0 || line >= _text.LineCount - 1,
            _draft.Passing,
            _composing);
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

        // Tab takes the proposed command into the field; it never runs by itself (D-363).
        if (key == Key.Tab && !control && !shift && !alt && _suggestion is { } proposed)
        {
            _suggestion = null;
            _text.Text = proposed;
            _text.CaretIndex = proposed.Length;
            return true;
        }

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
}
