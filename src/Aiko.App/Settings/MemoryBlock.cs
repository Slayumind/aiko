using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// The memory on the Personality page (D-278): four categories with their quotas, each opening
/// into its facts, and everything a person may do to a fact by hand.
///
/// What to show is decided in Aiko.Core.MemoryPage and what a click does in Aiko.Core.MemoryHand.
/// This class draws and wires. It is rebuilt whole after every change: the list is short, and a
/// rebuilt list cannot disagree with the file.
sealed class MemoryBlock : StackPanel
{
    /// A door for snapshots: AIKO_TRY_MEMORY_FILE=<path> draws the block from that file in place of
    /// the person's own memory, so a picture for the site never shows real facts. With it the block
    /// only draws: nothing is written and the real file is not opened.
    private static readonly string? SampleFile = Environment.GetEnvironmentVariable("AIKO_TRY_MEMORY_FILE");

    private readonly HashSet<MemoryCategory> _open = [];

    private MemoryFile _memory = Load();

    /// The fact shown as two fields instead of two lines.
    private string? _editing;

    /// The category of the fact being typed in, or null when the form is closed.
    private MemoryCategory? _adding;

    private bool _erasing;

    /// What went wrong with the last click, in the person's words. Shown once, above the buttons.
    private string? _problem;

    public MemoryBlock()
    {
        TryState();
        Build();

        // The model may have saved a fact since the page was built.
        Loaded += (_, _) => Reload();
    }

    /// A door for snapshots: AIKO_TRY_MEMORY=open, edit, add or erase draws the block in that state,
    /// so every state of the mockup can be looked at without clicking.
    private void TryState()
    {
        var state = Environment.GetEnvironmentVariable("AIKO_TRY_MEMORY");
        if (string.IsNullOrEmpty(state))
        {
            return;
        }

        _open.UnionWith(MemoryRules.Categories);
        _editing = state == "edit" ? _memory.In(MemoryCategory.Work).FirstOrDefault()?.Id : null;
        _adding = state == "add" ? MemoryCategory.Work : null;
        _erasing = state == "erase";
        if (state is "add" or "erase")
        {
            _open.Clear();
        }
    }

    private void Reload()
    {
        _memory = Load();
        Build();
    }

    private static MemoryFile Load() =>
        string.IsNullOrEmpty(SampleFile)
            ? MemoryStore.Load()
            : MemoryFile.FromJson(System.IO.File.ReadAllText(SampleFile)).Memory;

    private void Build()
    {
        var page = MemoryPage.From(_memory);
        Children.Clear();
        Children.Add(Head(page));
        Children.Add(Sections(page));

        if (page.TooLong > 0)
        {
            Children.Add(Warning(string.Format(Strings.MemoryTooLongNote, MemoryRules.MaxLength)));
        }

        if (page.SetAside > 0)
        {
            Children.Add(Warning(string.Format(Strings.MemoryAsideNote, page.SetAside)));
        }

        if (_problem is not null)
        {
            Children.Add(Warning(_problem));
        }

        Children.Add(_erasing ? EraseQuestion(page) : Foot(page));
    }

    // ---- the head: the count and "Add a fact" ----

    private Grid Head(MemoryPage page)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = string.Format(Strings.SectionMemory, page.Count, page.Capacity),
            Style = StyleOf("SectionLabel"),
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        });

        // While a new fact is being typed the button would open what is already open.
        if (_adding is null)
        {
            var add = TextButton(Strings.MemoryAdd, "SmallButton", () => Show(() => _adding = FirstWithRoom(page)));
            add.Content = WithPlus(Strings.MemoryAdd);
            Grid.SetColumn(add, 1);
            grid.Children.Add(add);
        }

        return grid;
    }

    /// The form opens on "How to work with me": that is what people most often tell by hand.
    private static MemoryCategory FirstWithRoom(MemoryPage page) =>
        page.Sections.Where(s => !s.Full).Select(s => (MemoryCategory?)s.Category)
            .OrderBy(c => c == MemoryCategory.Work ? 0 : 1).FirstOrDefault() ?? MemoryCategory.Work;

    // ---- the list ----

    private Border Sections(MemoryPage page)
    {
        var rows = new StackPanel();
        if (_adding is { } category)
        {
            rows.Children.Add(Lined(AddForm(page, category), first: true));
        }

        foreach (var section in page.Sections)
        {
            rows.Children.Add(Lined(QuotaRow(section), first: rows.Children.Count == 0));
            if (_open.Contains(section.Category))
            {
                foreach (var fact in section.Facts)
                {
                    rows.Children.Add(Lined(fact.Fact.Id == _editing ? EditForm(fact) : FactRow(fact), first: false, indent: 14));
                }

                if (section.Category == MemoryCategory.You)
                {
                    rows.Children.Add(Lined(Hint(Strings.MemoryYouNote, new Thickness(0, 10, 12, 10)), first: false, indent: 14));
                }
            }
        }

        return new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            Background = Tokens.Brush("Background"),
            Child = rows,
        };
    }

    private static Border Lined(UIElement child, bool first, double indent = 0) => new()
    {
        BorderBrush = Tokens.Brush("Hairline"),
        BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
        Margin = new Thickness(indent, 0, 0, 0),
        Child = child,
    };

    /// The name, a thin bar and the count. The whole row opens and closes its facts.
    private Border QuotaRow(MemoryPageSection section)
    {
        var grid = new Grid { Margin = new Thickness(14, 10, 14, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        grid.Children.Add(new TextBlock { Text = Title(section.Category), Style = StyleOf("RowName"), FontSize = Tokens.Get<double>("TextName") });

        var bar = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Min(section.Count, section.Quota), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(section.Quota - section.Count, 0), GridUnitType.Star) });
        bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Tokens.Brush("Raised") });
        Grid.SetColumnSpan(bar.Children[0], 2);
        bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Tokens.Brush("Muted") });
        Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);

        var count = new TextBlock
        {
            Text = $"{section.Count}/{section.Quota}",
            Style = StyleOf("RowHint"),
            FontFamily = Tokens.Get<FontFamily>("Mono"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(count, 2);
        grid.Children.Add(count);

        var row = new Border { Background = Brushes.Transparent, Child = grid, Focusable = true };
        var canOpen = section.Count > 0 || section.Category == MemoryCategory.You;
        if (canOpen)
        {
            row.Cursor = Cursors.Hand;
            AutomationProperties.SetName(row, $"{Title(section.Category)}, {section.Count}/{section.Quota}");
            row.MouseLeftButtonUp += (_, _) => Toggle(section.Category);
            row.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    e.Handled = true;
                    Toggle(section.Category);
                }
            };
        }

        return row;
    }

    private void Toggle(MemoryCategory category) =>
        Show(() =>
        {
            if (!_open.Remove(category))
            {
                _open.Add(category);
            }
        });

    private Grid FactRow(MemoryPageFact shown)
    {
        var fact = shown.Fact;
        var words = new StackPanel { Margin = new Thickness(0, 10, 8, 10) };
        words.Children.Add(new TextBlock { Text = fact.Text, Style = StyleOf("RowName"), FontSize = Tokens.Get<double>("TextName"), TextWrapping = TextWrapping.Wrap });
        if (fact.Meaning.Length > 0)
        {
            words.Children.Add(Hint(fact.Meaning, new Thickness(0, 2, 0, 0)));
        }

        if (fact.Keys.Count > 0)
        {
            var keys = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
            foreach (var key in fact.Keys)
            {
                keys.Children.Add(new Border
                {
                    Style = StyleOf("Chip"),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = new TextBlock { Text = key, Style = StyleOf("ChipText") },
                });
            }

            words.Children.Add(keys);
        }

        // A mark, a word and a colour: never the colour alone (DESIGN.md).
        if (shown.TooLong)
        {
            words.Children.Add(new TextBlock
            {
                Text = "● " + string.Format(Strings.MemoryTooLong, shown.Length, MemoryRules.MaxLength),
                Style = StyleOf("RowHint"),
                FontFamily = Tokens.Get<FontFamily>("Mono"),
                Foreground = Tokens.Brush("Caution"),
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 8, 0) };
        actions.Children.Add(IconButton(
            fact.Pinned ? MemoryIcons.Lock : MemoryIcons.Unlock,
            fact.Pinned ? Strings.MemoryUnpin : Strings.MemoryPin,
            fact.Pinned ? "Ink" : "Muted",
            () => Apply(memory => MemoryHand.Pin(memory, fact.Id, !fact.Pinned))));
        actions.Children.Add(IconButton(MemoryIcons.Pencil, Strings.MemoryEdit, "Muted", () => Show(() => _editing = fact.Id)));
        actions.Children.Add(IconButton(MemoryIcons.Trash, Strings.Remove, "Muted", () => Apply(memory => MemoryHand.Remove(memory, fact.Id))));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(words);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        return grid;
    }

    // ---- the two forms ----

    private StackPanel EditForm(MemoryPageFact shown)
    {
        var fact = shown.Fact;
        return Form(
            fact.Text, fact.Meaning, fact,
            (text, meaning) => Apply(memory => MemoryHand.Edit(memory, fact.Id, text, meaning), then: () => _editing = null),
            cancel: () => _editing = null,
            new Thickness(0, 10, 12, 10));
    }

    private StackPanel AddForm(MemoryPage page, MemoryCategory category)
    {
        var full = page.Sections.First(s => s.Category == category).Full;
        var form = Form(
            "", "", null,
            (text, meaning) => Apply(
                memory => MemoryHand.Add(memory, category, text, meaning, DateOnly.FromDateTime(DateTime.Now)),
                then: () =>
                {
                    _adding = null;
                    _open.Add(category);
                }),
            cancel: () => _adding = null,
            new Thickness(14, 10, 12, 10),
            canSave: !full);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var each in MemoryRules.Categories)
        {
            var tab = new RadioButton { Style = StyleOf("Segment"), GroupName = "MemoryCategory", Content = Tab(each), IsChecked = each == category };
            tab.Checked += (_, _) => Show(() => _adding = each);
            tabs.Children.Add(tab);
        }

        form.Children.Insert(0, new Border { Style = StyleOf("SegmentBar"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), Child = tabs });
        if (full)
        {
            form.Children.Add(Hint(Strings.MemoryFull, new Thickness(0, 6, 0, 0)));
        }

        return form;
    }

    /// Two fields, Save, Cancel and the count. Save is off while there is no text or the count is
    /// over the limit, so the person never types into a refusal.
    private StackPanel Form(
        string text, string meaning, MemoryFact? editing, Action<string, string> save, Action cancel, Thickness margin, bool canSave = true)
    {
        var textBox = Field(text, Strings.MemoryFactPlaceholder, out var textField);
        var meaningBox = Field(meaning, Strings.MemoryMeaningPlaceholder, out var meaningField);
        meaningBox.Margin = new Thickness(0, 6, 0, 0);

        var saveButton = TextButton(Strings.MemorySave, "SmallButton", () => save(textField.Text, meaningField.Text));
        saveButton.Background = Tokens.Brush("Ink");
        saveButton.Foreground = Tokens.Brush("Background");
        var cancelButton = TextButton(Strings.Cancel, "GhostButton", () => Show(cancel));
        cancelButton.Margin = new Thickness(4, 0, 0, 0);

        var count = new TextBlock { Style = StyleOf("RowHint"), FontFamily = Tokens.Get<FontFamily>("Mono"), VerticalAlignment = VerticalAlignment.Center };

        void Count()
        {
            var length = MemoryPage.LengthOf(textField.Text, meaningField.Text, editing);
            var fits = MemoryRules.Fits(length);
            count.Text = string.Format(Strings.MemoryCount, length, MemoryRules.MaxLength);
            count.Foreground = Tokens.Brush(fits ? "Muted" : "Caution");
            saveButton.IsEnabled = canSave && fits && !string.IsNullOrWhiteSpace(textField.Text);
            saveButton.Opacity = saveButton.IsEnabled ? 1 : 0.45;
        }

        textField.TextChanged += (_, _) => Count();
        meaningField.TextChanged += (_, _) => Count();
        Count();

        var foot = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = false };
        DockPanel.SetDock(count, Dock.Right);
        foot.Children.Add(count);
        foot.Children.Add(saveButton);
        foot.Children.Add(cancelButton);

        var form = new StackPanel { Margin = margin };
        form.Children.Add(textBox);
        form.Children.Add(meaningBox);
        form.Children.Add(foot);
        form.Loaded += (_, _) => textField.Focus();
        return form;
    }

    /// A text field with a grey line that says what goes into it while it is empty.
    private Grid Field(string text, string placeholder, out TextBox field)
    {
        var box = new TextBox
        {
            Text = text,
            Style = StyleOf("NameBox"),
            FontSize = Tokens.Get<double>("TextName"),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
        };
        AutomationProperties.SetName(box, placeholder);

        var hint = new TextBlock
        {
            Text = placeholder,
            Style = StyleOf("RowHint"),
            FontSize = Tokens.Get<double>("TextName"),
            Margin = new Thickness(10, 6, 10, 0),
            IsHitTestVisible = false,
            Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        box.TextChanged += (_, _) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var grid = new Grid();
        grid.Children.Add(box);
        grid.Children.Add(hint);
        field = box;
        return grid;
    }

    // ---- the foot ----

    private Grid Foot(MemoryPage page)
    {
        var note = page.Empty ? Strings.MemoryEmpty
            : _adding is not null ? Strings.MemoryAnyLanguageNote
            : Strings.MemoryEnglishNote;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        buttons.Children.Add(TextButton(Strings.MemoryOpenFile, "SmallButton", OpenFile));

        // Nothing to erase in an empty memory, so no button that would do nothing.
        if (!page.Empty)
        {
            var erase = TextButton(Strings.MemoryErase, "GhostDangerButton", () => Show(() => _erasing = true));
            erase.Margin = new Thickness(6, 0, 0, 0);
            buttons.Children.Add(erase);
        }

        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Hint(note, new Thickness(0, 0, 16, 0)));
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        return grid;
    }

    /// Asked in place of the buttons, not in a window of its own. It says what goes and that it
    /// does not come back, with the number, before the person presses anything red.
    private Border EraseQuestion(MemoryPage page)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(TextButton(Strings.MemoryErase, "DangerButton", () => Apply(MemoryHand.EraseAll, then: () =>
        {
            _erasing = false;
            _open.Clear();
        })));
        var cancel = TextButton(Strings.Cancel, "GhostButton", () => Show(() => _erasing = false));
        cancel.Margin = new Thickness(4, 0, 0, 0);
        buttons.Children.Add(cancel);

        var question = new TextBlock
        {
            Text = string.Format(Strings.MemoryEraseAsk, page.Count),
            Style = StyleOf("RowHint"),
            Foreground = Tokens.Brush("Ink"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(question);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        return new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 12, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Child = grid,
        };
    }

    private void OpenFile()
    {
        if (!string.IsNullOrEmpty(SampleFile))
        {
            return;
        }

        if (!MemoryStore.EnsureFile())
        {
            Show(() => _problem = Strings.MemoryWriteFailed);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(MemoryStore.Path) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Write($"memory: the file could not be opened ({e.GetType().Name})");
        }
    }

    // ---- changes ----

    /// A change of what the block shows, with nothing written.
    private void Show(Action change)
    {
        _problem = null;
        change();
        Build();
    }

    /// A change of the memory itself. The file is read again under the lock, so a fact the model
    /// saved a moment ago is not lost, and then the block is drawn from what was written.
    private void Apply(Func<MemoryFile, MemoryHandEdit> change, Action? then = null)
    {
        if (!string.IsNullOrEmpty(SampleFile))
        {
            return;
        }

        var edit = MemoryStore.Change(change);
        _problem = edit?.Outcome switch
        {
            null => Strings.MemoryWriteFailed,
            MemoryHandOutcome.Full => Strings.MemoryFull,
            _ => null,
        };

        if (edit is not null)
        {
            _memory = edit.Memory;
            if (edit.Outcome is MemoryHandOutcome.Done or MemoryHandOutcome.NothingToDo or MemoryHandOutcome.NoFact)
            {
                _editing = null;
                then?.Invoke();
            }
        }

        Build();
    }

    // ---- small parts ----

    private static string Title(MemoryCategory category) => category switch
    {
        MemoryCategory.You => Strings.MemoryCategoryYou,
        MemoryCategory.Work => Strings.MemoryCategoryWork,
        MemoryCategory.Projects => Strings.MemoryCategoryProjects,
        _ => Strings.MemoryCategoryInterests,
    };

    private static string Tab(MemoryCategory category) => category switch
    {
        MemoryCategory.You => Strings.MemoryTabYou,
        MemoryCategory.Work => Strings.MemoryTabWork,
        MemoryCategory.Projects => Strings.MemoryTabProjects,
        _ => Strings.MemoryTabInterests,
    };

    /// The block draws itself in its constructor, before it sits in a page, and FindResource
    /// finds nothing there. So it reads the styles from the dictionary itself, as Tokens does.
    private static readonly ResourceDictionary Controls = new()
    {
        Source = new Uri("pack://application:,,,/Theme/Controls.xaml", UriKind.Absolute),
    };

    private static Style StyleOf(string key) => (Style)Controls[key];

    private TextBlock Hint(string text, Thickness margin) => new() { Text = text, Style = StyleOf("RowHint"), Margin = margin };

    /// A line that asks for attention: a bar on the left, and the words in full colour.
    private Border Warning(string text) => new()
    {
        BorderBrush = Tokens.Brush("Caution"),
        BorderThickness = new Thickness(2, 0, 0, 0),
        Padding = new Thickness(10, 0, 0, 0),
        Margin = new Thickness(0, 10, 0, 0),
        Child = new TextBlock { Text = text, Style = StyleOf("RowHint"), Foreground = Tokens.Brush("Ink") },
    };

    /// A plus and the words, for the one button that makes something new.
    private static StackPanel WithPlus(string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Path
        {
            Data = Geometry.Parse("M 5,0 L 5,10 M 0,5 L 10,5"),
            Stroke = Tokens.Brush("Ink"),
            StrokeThickness = 1.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 10,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return content;
    }

    private static Button TextButton(string text, string style, Action click)
    {
        var button = new Button { Content = text, Style = StyleOf(style) };
        button.Click += (_, _) => click();
        return button;
    }

    private Button IconButton(string icon, string name, string brush, Action click)
    {
        var button = new Button
        {
            Style = StyleOf("IconButton"),
            Width = 26,
            Height = 26,
            ToolTip = name,
            Content = new Path
            {
                Data = Geometry.Parse("F1 " + icon),
                Fill = Tokens.Brush(brush),
                Stretch = Stretch.Uniform,
                Width = 13,
                Height = 13,
            },
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }
}

/// Icons of the slayumind set (D-278): a closed lock for a pinned fact, an open one for the rest.
static class MemoryIcons
{
    public const string Lock =
        "M19,8.276v-1.276c0-3.86-3.141-7-7-7S5,3.14,5,7v1.276c-1.742,.621-3,2.271-3,4.224v7c0,2.481,2.019,4.5,4.5,4.5h11c2.481,0,4.5-2.019,4.5-4.5v-7c0-1.953-1.258-3.602-3-4.224Zm-13-1.276c0-3.309,2.691-6,6-6s6,2.691,6,6v1.051c-.166-.019-.329-.051-.5-.051H6.5c-.171,0-.334,.032-.5,.051v-1.051Zm15,12.5c0,1.93-1.57,3.5-3.5,3.5H6.5c-1.93,0-3.5-1.57-3.5-3.5v-7c0-1.93,1.57-3.5,3.5-3.5h11c1.93,0,3.5,1.57,3.5,3.5v7Zm-8.5-5v3c0,.276-.224,.5-.5,.5s-.5-.224-.5-.5v-3c0-.276,.224-.5,.5-.5s.5,.224,.5,.5Z";

    public const string Unlock =
        "M17.5,8H6.5c-.171,0-.334,.032-.5,.051v-1.051c0-3.309,2.691-6,6-6,2.245,0,4.285,1.238,5.324,3.231,.127,.246,.431,.339,.675,.212,.244-.127,.34-.43,.212-.674-1.212-2.325-3.592-3.769-6.211-3.769-3.859,0-7,3.14-7,7v1.276c-1.742,.621-3,2.271-3,4.224v7c0,2.481,2.019,4.5,4.5,4.5h11c2.481,0,4.5-2.019,4.5-4.5v-7c0-2.481-2.019-4.5-4.5-4.5Zm3.5,11.5c0,1.93-1.57,3.5-3.5,3.5H6.5c-1.93,0-3.5-1.57-3.5-3.5v-7c0-1.93,1.57-3.5,3.5-3.5h11c1.93,0,3.5,1.57,3.5,3.5v7Zm-8.5-5v3c0,.276-.224,.5-.5,.5s-.5-.224-.5-.5v-3c0-.276,.224-.5,.5-.5s.5,.224,.5,.5Z";

    public const string Pencil =
        "M22.94,1.061c-1.368-1.367-3.76-1.365-5.124,0L1.611,17.265c-1.039,1.04-1.611,2.421-1.611,3.89v2.346c0,.276,.224,.5,.5,.5H2.846c1.47,0,2.851-.572,3.889-1.611L22.86,6.265c.579-.581,.953-1.262,1.08-1.972,.216-1.202-.148-2.381-1-3.232ZM6.028,21.682c-.85,.851-1.979,1.318-3.182,1.318H1v-1.846c0-1.202,.468-2.332,1.318-3.183L15.292,4.999l3.709,3.709L6.028,21.682ZM22.956,4.116c-.115,.642-.5,1.138-.803,1.441l-2.444,2.444-3.709-3.709,2.525-2.525c.986-.988,2.718-.99,3.709,0,.617,.617,.88,1.473,.723,2.349Z";

    public const string Trash =
        "M21.5,4h-3.551c-.252-2.244-2.139-4-4.449-4h-3c-2.31,0-4.197,1.756-4.449,4H2.5c-.276,0-.5,.224-.5,.5s.224,.5,.5,.5h1.5v14.5c0,2.481,2.019,4.5,4.5,4.5h7c2.481,0,4.5-2.019,4.5-4.5V5h1.5c.276,0,.5-.224,.5-.5s-.224-.5-.5-.5ZM10.5,1h3c1.758,0,3.204,1.308,3.449,3H7.051c.245-1.692,1.691-3,3.449-3Zm8.5,18.5c0,1.93-1.57,3.5-3.5,3.5h-7c-1.93,0-3.5-1.57-3.5-3.5V5h14v14.5ZM10,10.5v7c0,.276-.224,.5-.5,.5s-.5-.224-.5-.5v-7c0-.276,.224-.5,.5-.5s.5,.224,.5,.5Zm5,0v7c0,.276-.224,.5-.5,.5s-.5-.224-.5-.5v-7c0-.276,.224-.5,.5-.5s.5,.224,.5,.5Z";
}
