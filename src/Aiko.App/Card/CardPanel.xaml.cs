using System.Windows;
using System.Windows.Controls;

namespace Aiko.App;

/// The card itself. One and the same view in the tray and in the island: the island only has
/// different top corners, because it is pressed against the edge of the screen.
public partial class CardPanel : UserControl
{
    public CardPanel()
    {
        InitializeComponent();
    }

    /// Raised by the gear. The window above decides what to open.
    public event Action? SettingsRequested;

    /// Raised by "Open Claude Code" under an environment, with its name.
    public event Action<string>? OpenClaudeRequested;

    /// Raised by the cross. The card never hides by itself.
    public event Action? CloseRequested;

    /// The strip the card is dragged by.
    public FrameworkElement DragHandle => HeaderRow;

    public void Show(CardModel model)
    {
        UpdatedText.Text = model.Updated;
        Blocks.ItemsSource = model.Blocks;
    }

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void OnOpenClaude(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string environment })
        {
            OpenClaudeRequested?.Invoke(environment);
        }
    }
}
