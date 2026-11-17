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

/// The list over the editor: commands after a slash and files after an at sign.
sealed partial class TerminalEditor
{
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
        WaitForSuggestion();
        ShowHint();
        Offer();
    }

    /// New words in a command line drop the old proposal; words that ask for a command wait for a
    /// pause in the typing before the question goes out.
    private void WaitForSuggestion()
    {
        _suggestion = null;
        _suggestFor = null;
        _suggestFailedFor = null;
        _suggestPause.Stop();
        if (_session?.Kind == SessionKind.Shell && ShellAsk.IsRequest(_text.Text))
        {
            _suggestPause.Start();
        }
    }

    private void AskForSuggestion()
    {
        _suggestPause.Stop();
        if (_session?.Kind != SessionKind.Shell || !ShellAsk.IsRequest(_text.Text))
        {
            return;
        }

        _suggestFor = _text.Text;
        ShowHint();
        SuggestionAsked?.Invoke(_text.Text);
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
}
