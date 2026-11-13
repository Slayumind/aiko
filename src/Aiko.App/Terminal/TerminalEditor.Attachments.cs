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

/// Pictures and files in the editor: pasted, dropped and sent as paths.
sealed partial class TerminalEditor
{
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
            if (TerminalPaste.IsPicture(path) && Load(path) is { } picture)
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
        if (_session is null || TerminalPaste.Save(picture, _session.PicturesFolder) is not { } path)
        {
            return;
        }

        _draft.Pictures.Add(path);
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
            var cross = Marks.Line("M0,0 L6,6 M6,0 L0,6");
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
            label.Children.Add(Widgets.Mono(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TerminalEditorPicture, at + 1), "Ink"));
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
}
