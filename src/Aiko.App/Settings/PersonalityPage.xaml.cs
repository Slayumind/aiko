using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Aiko.Core;

namespace Aiko.App;

public partial class PersonalityPage : UserControl
{
    // A commit Aiko would write for the sample fix. It is English in every language, like any commit.
    private const string SampleCommit = "Round reset countdown to the nearest minute";

    private static readonly Dictionary<string, string> SkillAbout = new()
    {
        ["copy"] = Strings.SkillAikoCopy,
        ["release-gate"] = Strings.SkillAikoReleaseGate,
        ["docs-hygiene"] = Strings.SkillAikoDocsHygiene,
        ["playtest"] = Strings.SkillAikoPlaytest,
        ["polishing"] = Strings.SkillAikoPolishing,
        ["blender-to-unity"] = Strings.SkillAikoBlenderToUnity,
        ["texturing"] = Strings.SkillAikoTexturing,
        ["glb-for-web"] = Strings.SkillAikoGlbForWeb,
        ["palette"] = Strings.SkillAikoPalette,
        ["gamedesign-research"] = Strings.SkillAikoGamedesignResearch,
        ["balance"] = Strings.SkillAikoBalance,
        ["calendar"] = Strings.SkillAikoCalendar,
        ["drive"] = Strings.SkillAikoDrive,
    };

    private readonly EnvironmentsEditor _editor;

    private readonly List<EnvironmentRow> _environmentRows = [];

    /// The width of one column of switches. The Russian word for the persona in the head of its
    /// column is the widest thing in it.
    private const double SwitchColumn = 84;

    private PersonaSettings _persona = SettingsStore.LoadPersona();

    /// Setting the controls raises their own events, and those events save.
    private bool _filling;

    public PersonalityPage(EnvironmentsEditor editor)
    {
        InitializeComponent();
        _editor = editor;
        SampleCommitLine.Text = SampleCommit;

        _filling = true;
        FaceChibi.IsChecked = _persona.Face == FaceStyle.Chibi;
        FaceEmoji.IsChecked = _persona.Face == FaceStyle.Emoji;
        foreach (var choice in TemperamentChoice.Children.OfType<RadioButton>())
        {
            choice.IsChecked = (string)choice.Tag == _persona.Temperament.ToString();
        }
        _filling = false;

        ShowTemperament();
        FillSkills();
        FillEnvironments();
        _editor.Changed += OnEnvironmentsChanged;
    }

    /// Raised after a change to persona.json was saved. Switches per environment go through the
    /// editor, which says so itself.
    public event Action? Saved;

    public void Leave() => _editor.Changed -= OnEnvironmentsChanged;

    // ---- what is on, by environment ----

    private sealed record EnvironmentRow(
        string Name, ToggleButton Persona, ToggleButton Skills, ToggleButton Memory, RevealPanel Warning, string? OwnStyle);

    /// A table: one row per environment, one column per thing that can be on there (D-278).
    private void FillEnvironments()
    {
        EnvironmentRows.Children.Clear();
        _environmentRows.Clear();
        EnvironmentRows.Children.Add(ColumnHeads());

        _filling = true;
        foreach (var environment in SettingsPanel.Ordered(_editor.Current))
        {
            EnvironmentRows.Children.Add(EnvironmentRowFor(environment));
        }
        _filling = false;

        ShowSwitchState();
    }

    private Grid ColumnHeads()
    {
        var heads = SwitchGrid(new Thickness(12, 8, 2, 8));
        string[] names = [Strings.ColumnPersonality, Strings.ColumnSkills, Strings.ColumnMemory];
        for (var i = 0; i < names.Length; i++)
        {
            var head = new TextBlock
            {
                Text = names[i],
                Style = (Style)FindResource("SectionLabel"),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(head, i + 1);
            heads.Children.Add(head);
        }

        return heads;
    }

    /// The name takes what is left; the three switches stand in columns of one width, so they
    /// line up under their heads in every row.
    private static Grid SwitchGrid(Thickness margin)
    {
        var grid = new Grid { Margin = margin };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwitchColumn) });
        }

        return grid;
    }

    private Border EnvironmentRowFor(AikoEnvironment environment)
    {
        var folder = environment.ConfigDirectories[0];

        var title = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = environment.Name,
            Style = (Style)FindResource("RowName"),
            FontSize = Tokens.Get<double>("TextName"),
            Margin = new Thickness(0, 0, 8, 0),
        });
        var plan = ClaudeAccounts.Read(folder).PlanLabel;
        if (plan.Length > 0)
        {
            title.Children.Add(new Border
            {
                Style = (Style)FindResource("Chip"),
                Child = new TextBlock { Text = plan, Style = (Style)FindResource("ChipText") },
            });
        }

        var name = environment.Name;
        var persona = SwitchFor(name, Strings.ColumnPersonality, environment.Persona, 1, on =>
            Commit(EnvironmentEdits.SetPersona(_editor.Current, name, on), on ? "turned the persona on" : "turned the persona off"));
        var skills = SwitchFor(name, Strings.ColumnSkills, environment.Skills, 2, on =>
            Commit(EnvironmentEdits.SetSkills(_editor.Current, name, on), on ? "turned the skills on" : "turned the skills off"));
        var memory = SwitchFor(name, Strings.ColumnMemory, environment.HasMemory, 3, on =>
            Commit(EnvironmentEdits.SetMemory(_editor.Current, name, on), on ? "turned the memory on" : "turned the memory off"));

        var ownStyle = OwnOutputStyle(folder);
        var warning = new RevealPanel
        {
            Child = new TextBlock
            {
                Text = string.Format(Strings.PersonaOwnStyle, ownStyle),
                Style = (Style)FindResource("RowHint"),
                Foreground = Tokens.Brush("Caution"),
                Margin = new Thickness(0, 4, 0, 0),
            },
        };
        Grid.SetRow(warning, 1);
        Grid.SetColumnSpan(warning, 4);

        var grid = SwitchGrid(new Thickness(12, 10, 2, 10));
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(title);
        grid.Children.Add(persona);
        grid.Children.Add(skills);
        grid.Children.Add(memory);
        grid.Children.Add(warning);

        _environmentRows.Add(new EnvironmentRow(name, persona, skills, memory, warning, ownStyle));

        // Every row has a line above it: the first one stands under the heads of the columns.
        return new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
    }

    /// One switch of the table. A screen reader hears the environment and the column, because the
    /// switch itself has no word on it.
    private ToggleButton SwitchFor(string environment, string column, bool on, int at, Action<bool> changed)
    {
        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("Switch"),
            IsChecked = on,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        System.Windows.Automation.AutomationProperties.SetName(toggle, environment + " · " + column);
        toggle.Checked += (_, _) => changed(true);
        toggle.Unchecked += (_, _) => changed(false);
        Grid.SetColumn(toggle, at);
        return toggle;
    }

    private void Commit(EnvironmentSettings next, string what)
    {
        if (!_filling)
        {
            _editor.Commit(next, what);
        }
    }

    private void OnEnvironmentsChanged()
    {
        var environments = SettingsPanel.Ordered(_editor.Current).ToList();
        if (!environments.Select(e => e.Name).SequenceEqual(_environmentRows.Select(r => r.Name)))
        {
            FillEnvironments();
            return;
        }

        // The same rows: only the switches move, so the one just flipped keeps its motion.
        _filling = true;
        foreach (var (row, environment) in _environmentRows.Zip(environments))
        {
            row.Persona.IsChecked = environment.Persona;
            row.Skills.IsChecked = environment.Skills;
            row.Memory.IsChecked = environment.HasMemory;
        }
        _filling = false;

        ShowSwitchState();
    }

    /// The warning under a row, the lock on the memory switch, and whether the list of skills
    /// is about something that is on anywhere.
    private void ShowSwitchState()
    {
        foreach (var row in _environmentRows)
        {
            var persona = row.Persona.IsChecked == true;
            row.Warning.IsOpen = persona && row.OwnStyle is not null;

            // The memory works only together with the persona, so without her the switch is locked.
            row.Memory.IsEnabled = persona;
            row.Memory.Opacity = persona ? 1 : 0.45;
        }

        SkillGroups.Opacity = _editor.Current.Environments.Any(e => e.Skills) ? 1 : 0.45;
    }

    /// Read once when the page opens: the person changes it in their own settings.json, not here.
    private static string? OwnOutputStyle(string folder)
    {
        try
        {
            var path = ClaudeSettingsFile.PathIn(folder);
            return File.Exists(path) ? SettingsJsonPatch.UserOutputStyle(File.ReadAllText(path)) : null;
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ---- face and temperament ----

    private void OnFaceChanged(object sender, RoutedEventArgs e)
    {
        SavePersona(_persona with { Face = FaceEmoji.IsChecked == true ? FaceStyle.Emoji : FaceStyle.Chibi });
        ShowFaces();
    }

    /// The face next to the title and beside the sample answer, in the chosen style. A calm
    /// temperament smiles, a loud one beams, as in the mockup.
    private void ShowFaces()
    {
        var face = _persona.Temperament is Temperament.Bright or Temperament.Musou ? AikoFace.Done : AikoFace.Fresh;
        HeaderFace.Source = FaceDrawing.For(_persona.Face, face, FaceGround.Dark, HeaderFace.Width);
        ReplyFace.Source = FaceDrawing.For(_persona.Face, face, FaceGround.Dark, ReplyFace.Width);
    }

    private void OnTemperamentChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || !Enum.TryParse<Temperament>((string)((RadioButton)sender).Tag, out var temperament))
        {
            return;
        }

        SavePersona(_persona with { Temperament = temperament });
        ShowTemperament();

        // The persona text changed, so installed plugins take it for the next session.
        PluginSync.PersonaChanged();
    }

    private void ShowTemperament()
    {
        var temperament = _persona.Temperament;
        TemperamentAbout.Text = temperament switch
        {
            Temperament.Quiet => Strings.TemperamentQuietAbout,
            Temperament.Bright => Strings.TemperamentBrightAbout,
            Temperament.Musou => Strings.TemperamentMusouAbout,
            _ => Strings.TemperamentNormalAbout,
        };

        var (open, body, close) = temperament switch
        {
            Temperament.Quiet => ("", Strings.SampleBody, Strings.SampleCloseQuiet),
            Temperament.Bright => (Strings.SampleOpenBright, Strings.SampleBodyBright, Strings.SampleCloseBright),
            Temperament.Musou => (Strings.SampleOpenMusou, Strings.SampleBody + Strings.SampleBodyMusouTail, Strings.SampleCloseMusou),
            _ => (Strings.SampleOpenNormal, Strings.SampleBody, Strings.SampleCloseNormal),
        };

        ReplyOpen.Text = open;
        ReplyOpen.Visibility = open.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReplyBody.Text = body;
        ReplyClose.Text = close;
        ShowFaces();
    }

    // ---- skills ----

    /// The skills themselves. They are one plugin in Claude Code and come and go together
    /// (D-237); where they are on is a switch of each environment in the table above (D-281).
    private void FillSkills()
    {
        SkillGroups.Children.Clear();

        foreach (var group in SkillCatalog.Groups)
        {
            SkillGroups.Children.Add(DomainLabel(group));
            SkillGroups.Children.Add(DomainBox(group));
        }

        SkillsTitle.Text = string.Format(Strings.SectionSkills, SkillCatalog.All.Count);
    }

    /// The domain name and how many skills it has, above its own box.
    private TextBlock DomainLabel(SkillGroup group) => new()
    {
        Text = $"{(group.Domain == SkillDomain.Projects ? Strings.SkillDomainProjects : Strings.SkillDomainGames)} · {group.Skills.Count}",
        Style = (Style)FindResource("SectionLabel"),
        Margin = new Thickness(0, 18, 0, 8),
    };

    private Border DomainBox(SkillGroup group)
    {
        var rows = new StackPanel();
        for (var i = 0; i < group.Skills.Count; i++)
        {
            rows.Children.Add(SkillRow(group.Skills[i], topLine: i > 0));
        }

        return new Border { Style = (Style)FindResource("Rows"), Child = rows };
    }

    private Border SkillRow(string skill, bool topLine)
    {
        var rows = new StackPanel { Margin = new Thickness(12, 9, 28, 9) };
        rows.Children.Add(new TextBlock
        {
            Text = SkillCatalog.Call(skill),
            Style = (Style)FindResource("RowName"),
            FontFamily = Tokens.Get<System.Windows.Media.FontFamily>("Mono"),
        });
        rows.Children.Add(new TextBlock
        {
            Text = SkillAbout.GetValueOrDefault(skill, ""),
            Style = (Style)FindResource("RowHint"),
            Margin = new Thickness(0, 2, 0, 0),
        });

        return new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, topLine ? 1 : 0, 0, 0),
            Child = rows,
        };
    }

    private void SavePersona(PersonaSettings next)
    {
        if (_filling || next == _persona)
        {
            return;
        }

        _persona = next;
        SettingsStore.SavePersona(next);
        Saved?.Invoke();
    }
}
