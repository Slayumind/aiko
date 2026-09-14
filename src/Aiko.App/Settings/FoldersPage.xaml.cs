using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

public partial class FoldersPage : UserControl
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly EnvironmentsEditor _editor;

    /// The folder added last, so only its row plays the entrance.
    private string? _added;

    public FoldersPage(EnvironmentsEditor editor)
    {
        InitializeComponent();
        _editor = editor;
        RestChoice.Picked += OnRestPicked;
        _editor.Changed += Fill;
        Fill();
    }

    /// Opens the checklist at the item that sets up what this page is missing.
    public event Action<ChecklistItem>? ChecklistRequested;

    public void Leave() => _editor.Changed -= Fill;

    private void Fill()
    {
        var settings = _editor.Current;
        var names = settings.Environments.Select(e => e.Name).ToList();
        var two = names.Count > 1;

        Lead.Text = string.Format(Strings.FoldersTableLead, EnvironmentEdits.ForNewBinding(settings, Home)?.Command ?? "aiko");
        OneEnvironment.Visibility = two ? Visibility.Collapsed : Visibility.Visible;
        OneEnvironmentText.Text = string.Format(Strings.FoldersOneEnvironment, names.FirstOrDefault() ?? "");
        Table.Visibility = two ? Visibility.Visible : Visibility.Collapsed;
        NeedCommands.Visibility = CommandFolder.IsSetUp ? Visibility.Collapsed : Visibility.Visible;

        Rows.Children.Clear();
        foreach (var binding in EnvironmentEdits.Bindings(settings))
        {
            Rows.Children.Add(Row(binding, names));
        }

        RestChoice.Items = names;
        RestChoice.SelectedIndex = names.IndexOf(settings.Default(Home)?.Name ?? "");
        _added = null;
    }

    private Border Row(Binding binding, List<string> names)
    {
        var path = new TextBlock
        {
            Text = binding.Folder,
            Style = (Style)FindResource("RowNote"),
            FontSize = Tokens.Get<double>("TextSmall"),
            Foreground = Tokens.Brush("Ink"),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = binding.Folder,
        };

        var choice = new DropDown { Items = names, SelectedIndex = names.IndexOf(binding.Environment), Margin = new Thickness(10, 0, 0, 0) };
        choice.Picked += index => _editor.Commit(EnvironmentEdits.Bind(_editor.Current, binding.Folder, names[index]), "moved a folder to another environment");
        Grid.SetColumn(choice, 1);

        var remove = new Button
        {
            Style = (Style)FindResource("IconButton"),
            ToolTip = Strings.RemoveBinding,
            HorizontalAlignment = HorizontalAlignment.Right,
            Content = new Path
            {
                Data = Geometry.Parse("M 0,0 L 8,8 M 8,0 L 0,8"),
                Stroke = Tokens.Brush("Muted"),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 8,
                Height = 8,
            },
        };
        remove.Click += (_, _) => _editor.Commit(EnvironmentEdits.Unbind(_editor.Current, binding.Folder), "unbound a folder");
        Grid.SetColumn(remove, 2);

        var grid = new Grid { Margin = new Thickness(12, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.Children.Add(path);
        grid.Children.Add(choice);
        grid.Children.Add(remove);

        var row = new Border { BorderBrush = Tokens.Brush("Hairline"), BorderThickness = new Thickness(0, 1, 0, 0), Child = grid };
        if (_added is not null && RealClaude.SameFolder(_added, binding.Folder))
        {
            Motion.Appear(row);
        }

        return row;
    }

    private void OnRestPicked(int index)
    {
        var names = _editor.Current.Environments.Select(e => e.Name).ToList();
        _editor.Commit(EnvironmentEdits.SetDefault(_editor.Current, names[index]), "changed the default environment");
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.PickProject };
        if (dialog.ShowDialog() != true || EnvironmentEdits.ForNewBinding(_editor.Current, Home) is not { } target)
        {
            return;
        }

        // A folder that is already in the table keeps its environment: picking it again is not a
        // request to move it.
        if (EnvironmentEdits.Bindings(_editor.Current).Any(b => RealClaude.SameFolder(b.Folder, dialog.FolderName)))
        {
            return;
        }

        _added = dialog.FolderName;
        _editor.Commit(EnvironmentEdits.Bind(_editor.Current, dialog.FolderName, target.Name), "bound a folder");
    }

    private void OnAddSecond(object sender, RoutedEventArgs e) => ChecklistRequested?.Invoke(ChecklistItem.SecondEnvironment);

    private void OnSetUpCommands(object sender, RoutedEventArgs e) => ChecklistRequested?.Invoke(ChecklistItem.Commands);
}
