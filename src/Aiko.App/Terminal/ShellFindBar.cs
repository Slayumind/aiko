using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Aiko.App;

/// The search in the output of a command line (Ctrl+Shift+F, D-358): a field in the top right
/// corner over the blocks, with how many places it found. The search starts from the newest output
/// at the bottom: Enter goes to the place above, Shift+Enter to the one below, Esc closes it.
sealed class ShellFindBar : Border
{
    private readonly ShellBlocksView _view;
    private readonly TextBox _field = new();
    private readonly TextBlock _count = new();

    public ShellFindBar(ShellBlocksView view)
    {
        _view = view;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 8, 16, 0);
        Padding = new Thickness(10, 5, 10, 5);
        CornerRadius = new CornerRadius(8);
        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        Visibility = Visibility.Collapsed;

        _field.MinWidth = 200;
        _field.Background = Brushes.Transparent;
        _field.BorderThickness = new Thickness(0);
        _field.Foreground = Tokens.Brush("Ink");
        _field.CaretBrush = Tokens.Brush("Ink");
        _field.FontFamily = Tokens.Get<FontFamily>("Mono");
        _field.VerticalContentAlignment = VerticalAlignment.Center;
        _field.ToolTip = Strings.ShellFind;
        _field.TextChanged += (_, _) => Search();
        _field.PreviewKeyDown += OnKey;

        _count.Foreground = Tokens.Brush("Muted");
        _count.FontFamily = Tokens.Get<FontFamily>("Mono");
        _count.Margin = new Thickness(10, 0, 0, 0);
        _count.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_field);
        row.Children.Add(_count);
        Child = row;
    }

    /// The search closed: the keys go back to the editor.
    public event Action? Closed;

    public void Open()
    {
        Visibility = Visibility.Visible;
        _field.Focus();
        _field.SelectAll();
        UpdateLayout();
        _view.MakeRoom(ActualHeight + Margin.Top);
        Search();
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        _view.MakeRoom(0);
        _view.Find("");
        _count.Text = "";
        Closed?.Invoke();
    }

    private void Search()
    {
        _view.Find(_field.Text);
        _count.Text = _view.FindStatus;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _view.FindNext(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 1 : -1);
                _count.Text = _view.FindStatus;
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    internal void SearchForCheck(string text)
    {
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            UpdateLayout();
            _view.MakeRoom(ActualHeight + Margin.Top);
        }

        _field.Text = text;
    }

    internal string CountForCheck => _count.Text;
}
