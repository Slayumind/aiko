using System.Diagnostics;
using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The palette of sessions (D-347, D-353) and the quick pick of a folder for a new session (D-350).
sealed partial class TerminalHost
{
    /// The quick pick of a folder for a new session (D-350): the folders of the environment, the
    /// latest used first, and the folder dialog as the last line. Enter starts Claude Code there,
    /// Ctrl+Enter a command line.
    private void PickFolderQuickly()
    {
        var environment = _model.Environment;
        var palette = new PaletteView(Strings.TerminalPickFolderHint, Strings.TerminalPickFolderKeys, query =>
        [
            new PaletteGroup("", [.. FolderPick.Find(_sessions, environment, query)
                .Select(hit => new PaletteRow(hit.Folder, hit.Name, hit.Parent, []))]),
            new PaletteGroup("", [new PaletteRow(OtherFolder, [new TextPart(Strings.TerminalPickOtherFolder, false)], "", [], Quiet: true)]),
        ]);
        palette.Closed += _panel.ClosePalette;
        palette.Picked += (folder, control) =>
        {
            _panel.ClosePalette();
            var kind = control ? SessionKind.Shell : SessionKind.Claude;
            if (folder != OtherFolder)
            {
                StartIn(folder, kind);
            }
            else if (PickFolder(_model.Session(_sessions)?.Folder) is { } picked)
            {
                StartIn(picked, kind);
            }
        };
        _panel.ShowPalette(palette);
    }

    /// The palette of sessions (D-347, D-353): the sessions of Aiko at once, and the conversations
    /// Claude Code keeps in the folders of the environments as soon as they are read.
    private int _palettesOpened;

    internal (bool Open, int Opened) PaletteForCheck => (_panel.PaletteOpen, _palettesOpened);

    internal void ClosePaletteForCheck() => _panel.ClosePalette();

    public async void OpenSessionPalette()
    {
        _palettesOpened++;
        if (!_window.IsVisible)
        {
            Show();
        }

        var palette = new PaletteView(Strings.TerminalSearchHint, Strings.TerminalSearchKeys, query =>
        {
            var found = SessionSearch.Find(_sessions, _conversations, query);
            var now = DateTimeOffset.Now;
            return
            [
                new PaletteGroup(Strings.TerminalSearchOpen, [.. found.Open.Select(hit => SearchRow(hit, now, quiet: false))]),
                new PaletteGroup(Strings.TerminalSearchClosed, [.. found.Closed.Select(hit => SearchRow(hit, now, quiet: true))]),
                new PaletteGroup(Strings.TerminalSearchOutside, [.. found.Outside.Select(hit => SearchRow(hit, now, quiet: true))]),
            ];
        });
        palette.Closed += _panel.ClosePalette;
        palette.Picked += (id, _) =>
        {
            _panel.ClosePalette();
            OpenFound(id);
        };
        // The conversations are read in the background. Most of the time they come within a
        // moment, and the palette opens with its rows; rows that changed during the pop-in made it
        // jump. A slow disk does not keep the palette waiting: it opens and fills in later.
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var folders = _settings.Environments
            .Select(environment => (environment.Name, environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home)))
            .ToList();
        var loading = _shelf.LoadAsync(folders);
        if (await Task.WhenAny(loading, Task.Delay(PaletteWait)) == loading)
        {
            _conversations = await loading;
            palette.Refresh();
            _panel.ShowPalette(palette);
            return;
        }

        _panel.ShowPalette(palette);
        _conversations = await loading;
        if (_panel.PaletteOpen)
        {
            palette.Refresh();
        }
    }

    /// How long the palette waits for the conversations before it opens without them.
    private static readonly TimeSpan PaletteWait = TimeSpan.FromMilliseconds(150);

    private static PaletteRow SearchRow(SearchHit hit, DateTimeOffset now, bool quiet)
    {
        var when = TerminalPanel.UsedWords(hit.LastUsed, now);
        // "Yesterday, 16:02" stands in the middle of the line here, so it starts small.
        if (when.Length > 0)
        {
            when = char.ToLower(when[0], System.Globalization.CultureInfo.CurrentCulture) + when[1..];
        }

        var where = hit.Source switch
        {
            null => hit.Environment,
            ConversationSource.Terminal => Strings.TerminalSourceTerminal,
            ConversationSource.VsCode => "VS Code",
            ConversationSource.Desktop => "Claude Desktop",
            _ => Strings.TerminalSourceOther,
        };
        var meta = string.Join(" · ", new[] { where, hit.Folder, when }.Where(part => part.Length > 0));
        return new PaletteRow(hit.Id, hit.Name, meta, hit.Snippet, quiet);
    }

    /// A choice of the palette: a session of the list opens, a closed one comes back, and a
    /// conversation started outside Aiko joins the list and continues with --resume.
    private void OpenFound(string id)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // A conversation a session went on in opens that session, never a second one beside it.
        id = _sessions.OwnerOfConversation(id)?.Id ?? id;
        if (_sessions.Find(id) is not null)
        {
            _sessions = _sessions.Reopen(id, now);
        }
        else if (_conversations.FirstOrDefault(conversation => conversation.Id == id) is { } conversation)
        {
            _sessions = _sessions.Found(conversation, now);
        }

        if (_sessions.Find(id) is not { } session)
        {
            return;
        }

        Save();
        Change(_model.ToEnvironment(session.Environment, _sessions).ToSession(id, _sessions));
    }
}
