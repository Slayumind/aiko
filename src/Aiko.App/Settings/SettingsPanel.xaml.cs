using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

public partial class SettingsPanel : UserControl
{
    public const string FoldersPageKey = "folders";
    public const string GeneralPageKey = "general";
    private const string EnvironmentPagePrefix = "env:";

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly EnvironmentsEditor _editor = new();
    private readonly Dictionary<string, string> _plans = new(StringComparer.OrdinalIgnoreCase);
    private string _page;
    private bool _buildingNav;

    public SettingsPanel() : this(null)
    {
    }

    /// The page to open: "general", "folders", or "env:" and an account folder. Null opens the first
    /// environment.
    public SettingsPanel(string? page)
    {
        InitializeComponent();

        // A small screen still shows the header; the page scrolls inside.
        Body.Height = Math.Clamp(SystemParameters.WorkArea.Height - 160, 420, 600);
        VersionLine.Text = $"Aiko {AppVersion.Current()}";

        _editor.Changed += OnEnvironmentsChanged;
        _page = page ?? EnvironmentPage(0) ?? GeneralPageKey;
        BuildNav();
        ShowPage(_page, animate: false);
    }

    public event Action? CloseRequested;
    public event Action? QuitRequested;
    public event Action? WizardRequested;

    /// Raised when the window has to come back in another language.
    public event Action? ReopenRequested;

    /// Raised after any change, so the tray, the island and direct mode follow it at once.
    public event Action? SettingsChanged;

    public FrameworkElement DragHandle => HeaderRow;

    public string CurrentPage => _page;

    /// The key of the n-th environment's page: environment 1 is always the one in .claude.
    public string? EnvironmentPage(int index) =>
        Ordered(_editor.Current).ElementAtOrDefault(index) is { } environment
            ? EnvironmentPagePrefix + environment.ConfigDirectories[0]
            : null;

    /// Applies a name or a command still being typed. Called when the window closes.
    public void Leave() => LeaveCurrentPage();

    public void StartUpdateCheck()
    {
        if (_page != GeneralPageKey)
        {
            ShowPage(GeneralPageKey, animate: false);
            BuildNav();
        }

        (PageHost.Content as GeneralPage)?.StartUpdateCheck();
    }

    private static IEnumerable<AikoEnvironment> Ordered(EnvironmentSettings settings)
    {
        var first = settings.First(Home);
        return first is null
            ? settings.Environments
            : settings.Environments.Where(e => e != first).Prepend(first);
    }

    // ---- the menu ----

    private void BuildNav()
    {
        _buildingNav = true;
        Nav.Children.Clear();

        var settings = _editor.Current;
        Nav.Children.Add(new TextBlock
        {
            Text = Strings.SectionEnvironments,
            Style = (Style)FindResource("SectionLabel"),
            Margin = new Thickness(10, 4, 0, 6),
        });

        foreach (var environment in Ordered(settings))
        {
            var folder = environment.ConfigDirectories[0];
            var plan = PlanOf(folder);
            var sub = plan.Length > 0 ? $"{plan} · {environment.Command}" : environment.Command;
            Nav.Children.Add(NavItem(EnvironmentPagePrefix + folder, environment.Name, sub));
        }

        if (settings.Environments.Count < EnvironmentSettings.MaxEnvironments)
        {
            Nav.Children.Add(AddSecondSlot());
        }

        Nav.Children.Add(new Border { Height = 1, Background = Tokens.Brush("Hairline"), Margin = new Thickness(10, 8, 10, 8) });

        var bound = EnvironmentEdits.Bindings(settings).Count;
        Nav.Children.Add(NavItem(FoldersPageKey, Strings.ItemFolders, bound > 0 ? string.Format(Strings.NavBound, bound) : null));
        Nav.Children.Add(NavItem(GeneralPageKey, Strings.NavGeneral, null));

        _buildingNav = false;
    }

    private RadioButton NavItem(string key, string title, string? sub)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis });
        if (sub is not null)
        {
            content.Children.Add(new TextBlock
            {
                Text = sub,
                Style = (Style)FindResource("RowNote"),
                Margin = new Thickness(0, 1, 0, 0),
            });
        }

        var item = new RadioButton
        {
            Style = (Style)FindResource("NavItem"),
            GroupName = "SettingsNav",
            IsChecked = key == _page,
            Content = content,
            Margin = new Thickness(0, 0, 0, 2),
        };
        item.Checked += (_, _) =>
        {
            if (!_buildingNav)
            {
                ShowPage(key, animate: true);
            }
        };

        return item;
    }

    /// The empty place of environment 2, drawn with a dashed edge, which a Border cannot do.
    private Button AddSecondSlot()
    {
        var face = new Grid();
        face.Children.Add(new Rectangle
        {
            RadiusX = 8,
            RadiusY = 8,
            Stroke = Tokens.Brush("InputLine"),
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
        });

        var words = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 8, 10, 8) };
        words.Children.Add(new Path
        {
            Data = Geometry.Parse("M 5,0 L 5,10 M 0,5 L 10,5"),
            Stroke = Tokens.Brush("Muted"),
            StrokeThickness = 1.5,
            Width = 10,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        words.Children.Add(new TextBlock
        {
            Text = Strings.AddSecondEnvironment,
            Style = (Style)FindResource("RowHint"),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        face.Children.Add(words);

        var slot = new Button
        {
            Style = (Style)FindResource("GhostButton"),
            Height = double.NaN,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 0),
            Content = face,
        };
        slot.Click += (_, _) => WizardRequested?.Invoke();
        return slot;
    }

    private string PlanOf(string folder)
    {
        if (!_plans.TryGetValue(folder, out var plan))
        {
            plan = ClaudeAccounts.Read(folder).PlanLabel;
            _plans[folder] = plan;
        }

        return plan;
    }

    // ---- the pages ----

    private void ShowPage(string key, bool animate)
    {
        LeaveCurrentPage();

        var page = MakePage(key);
        if (page is null)
        {
            key = EnvironmentPage(0) ?? GeneralPageKey;
            page = MakePage(key)!;
        }

        _page = key;
        PageHost.Content = page;
        PageScroll.ScrollToTop();

        if (animate && Motion.IsOn)
        {
            var shift = new TranslateTransform(8, 0);
            page.RenderTransform = shift;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Motion.Expand) { EasingFunction = Motion.Standard });
            shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(8, 0, Motion.Settle) { EasingFunction = Motion.Standard });
        }
    }

    private FrameworkElement? MakePage(string key)
    {
        if (key == GeneralPageKey)
        {
            var general = new GeneralPage();
            general.Saved += OnGeneralSaved;
            general.QuitRequested += () => QuitRequested?.Invoke();
            general.WizardRequested += () => WizardRequested?.Invoke();
            general.ReopenRequested += () => ReopenRequested?.Invoke();
            return general;
        }

        if (key == FoldersPageKey)
        {
            var folders = new FoldersPage(_editor);
            folders.WizardRequested += () => WizardRequested?.Invoke();
            return folders;
        }

        var folder = key.StartsWith(EnvironmentPagePrefix, StringComparison.Ordinal) ? key[EnvironmentPagePrefix.Length..] : null;
        if (folder is null || !_editor.Current.Environments.Any(e => e.Holds(folder)))
        {
            return null;
        }

        var environment = new EnvironmentPage(_editor, folder);
        environment.FoldersRequested += () => Navigate(FoldersPageKey);
        environment.Removed += note =>
        {
            Navigate(EnvironmentPage(0) ?? GeneralPageKey);
            (PageHost.Content as EnvironmentPage)?.ShowNote(note);
        };
        return environment;
    }

    /// Opens a page from inside another one, and moves the mark in the menu with it.
    private void Navigate(string key)
    {
        ShowPage(key, animate: true);
        BuildNav();
    }

    private void LeaveCurrentPage()
    {
        switch (PageHost.Content)
        {
            case EnvironmentPage environment:
                environment.Leave();
                break;
            case FoldersPage folders:
                folders.Leave();
                break;
        }
    }

    private void OnEnvironmentsChanged()
    {
        Saved.Show();
        BuildNav();
        SettingsChanged?.Invoke();
    }

    private void OnGeneralSaved()
    {
        Saved.Show();
        SettingsChanged?.Invoke();
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
}
