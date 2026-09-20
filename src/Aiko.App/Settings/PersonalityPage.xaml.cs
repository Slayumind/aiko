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
        ["calendar"] = Strings.SkillAikoCalendar,
        ["drive"] = Strings.SkillAikoDrive,
    };

    private readonly EnvironmentsEditor _editor;

    private readonly List<EnvironmentRow> _environmentRows = [];
    private readonly ToggleButton _skillsSwitch = new();

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

    // ---- where Aiko talks ----

    private sealed record EnvironmentRow(string Name, ToggleButton Switch, RevealPanel Warning, string? OwnStyle);

    private void FillEnvironments()
    {
        EnvironmentRows.Children.Clear();
        _environmentRows.Clear();

        _filling = true;
        foreach (var environment in SettingsPanel.Ordered(_editor.Current))
        {
            EnvironmentRows.Children.Add(EnvironmentRowFor(environment, first: _environmentRows.Count == 0));
        }
        _filling = false;

        ShowPersonaState();
    }

    private Border EnvironmentRowFor(AikoEnvironment environment, bool first)
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

        var toggle = new ToggleButton { Style = (Style)FindResource("Switch"), IsChecked = environment.Persona };
        toggle.Checked += (_, _) => SetPersona(environment.Name, true);
        toggle.Unchecked += (_, _) => SetPersona(environment.Name, false);
        Grid.SetColumn(toggle, 1);

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

        var grid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(title);
        grid.Children.Add(toggle);
        grid.Children.Add(warning);

        _environmentRows.Add(new EnvironmentRow(environment.Name, toggle, warning, ownStyle));

        return new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
            Child = grid,
        };
    }

    private void SetPersona(string environment, bool on)
    {
        if (_filling)
        {
            return;
        }

        _editor.Commit(EnvironmentEdits.SetPersona(_editor.Current, environment, on), on ? "turned the persona on" : "turned the persona off");
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
            row.Switch.IsChecked = environment.Persona;
        }
        _filling = false;

        ShowPersonaState();
    }

    /// The warning under a switch, and whether the skills can work anywhere at all.
    private void ShowPersonaState()
    {
        foreach (var row in _environmentRows)
        {
            row.Warning.IsOpen = row.Switch.IsChecked == true && row.OwnStyle is not null;
        }

        var anyOn = _editor.Current.Environments.Any(e => e.Persona);
        SkillsArea.Opacity = anyOn ? 1 : 0.45;
        _skillsSwitch.IsEnabled = anyOn;

        SkillsNote.Text = anyOn ? Strings.SkillsWork : Strings.SkillsNeedPersona;
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

    /// One switch for all skills, then the skills themselves: they are one plugin in Claude Code and
    /// come and go together (D-237).
    private void FillSkills()
    {
        SkillRows.Children.Clear();
        SkillGroups.Children.Clear();

        _filling = true;
        _skillsSwitch.Style = (Style)FindResource("Switch");
        _skillsSwitch.IsChecked = _persona.SkillsOn;
        _filling = false;
        _skillsSwitch.Checked += (_, _) => SetSkills(true);
        _skillsSwitch.Unchecked += (_, _) => SetSkills(false);
        Grid.SetColumn(_skillsSwitch, 1);

        var title = new TextBlock
        {
            Text = Strings.SkillsSwitch,
            Style = (Style)FindResource("RowName"),
            FontSize = Tokens.Get<double>("TextName"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var header = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(title);
        header.Children.Add(_skillsSwitch);
        SkillRows.Children.Add(header);

        foreach (var group in SkillCatalog.Groups)
        {
            SkillGroups.Children.Add(DomainLabel(group));
            SkillGroups.Children.Add(DomainBox(group));
        }

        SkillsTitle.Text = string.Format(Strings.SectionSkills, SkillCatalog.All.Count);
        ShowSkillsOn();
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

    private void SetSkills(bool on)
    {
        if (_filling)
        {
            return;
        }

        SavePersona(_persona with { SkillsOn = on });
        ShowSkillsOn();
        PluginSync.Request(on ? "skills switched on" : "skills switched off");
    }

    private void ShowSkillsOn() => SkillGroups.Opacity = _persona.SkillsOn ? 1 : 0.45;

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
