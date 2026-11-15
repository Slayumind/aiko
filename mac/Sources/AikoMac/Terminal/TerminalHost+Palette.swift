import AikoKit
import AppKit
import SwiftUI

/// The palettes of the window, the twin of TerminalHost.Palette.cs: the quick pick of a folder for
/// a new session (D-353), and the search of sessions and of the conversations Claude Code keeps
/// (D-347, D-364).
extension TerminalHost {
    /// The id of the last line of the folder pick, which asks for any other folder.
    private static let otherFolder = "\u{1}other"

    /// How long the palette waits for the conversations before it opens without them.
    private static let paletteWait: UInt64 = 150_000_000

    /// "New session": the folders the sessions of this environment ran in, the latest first, and
    /// any other folder at the end. Enter starts Claude Code there, Cmd+Enter a command line.
    func pickFolderQuickly() {
        let environment = model.environment
        let palette = PaletteState(hint: Strings.terminalPickFolderHint, keys: Strings.terminalPickFolderKeys) { [weak self] query in
            guard let self else { return [] }
            let folders = FolderPick.find(self.sessions, environment: environment, query: query)
                .map { PaletteRow(id: $0.folder, name: $0.name, meta: $0.parent) }
            let other = PaletteRow(id: Self.otherFolder, name: [TextPart(Strings.terminalPickOtherFolder, false)],
                                   meta: "", quiet: true)
            return [PaletteGroup(title: "", rows: folders), PaletteGroup(title: "", rows: [other])]
        }
        palette.onPicked = { [weak self] folder, command in
            guard let self else { return }
            self.closePalette()
            let kind: SessionKind = command ? .shell : .claude
            if folder != Self.otherFolder {
                self.start(in: folder, kind: kind)
            } else if let picked = self.chooseFolder() {
                self.start(in: picked, kind: kind)
            }
        }
        showPalette(palette)
    }

    /// The search of sessions: the sessions of Aiko at once, and the conversations Claude Code keeps
    /// in the folders of the environments as soon as they are read.
    func openSessionPalette() {
        if !isShown { show() }

        let palette = PaletteState(hint: Strings.terminalSearchHint, keys: Strings.terminalSearchKeys) { [weak self] query in
            guard let self else { return [] }
            let found = SessionSearch.find(self.sessions, self.conversations, query: query)
            let now = Date()
            return [
                PaletteGroup(title: Strings.terminalSearchOpen, rows: found.open.map { Self.searchRow($0, now, quiet: false) }),
                PaletteGroup(title: Strings.terminalSearchClosed, rows: found.closed.map { Self.searchRow($0, now, quiet: true) }),
                PaletteGroup(title: Strings.terminalSearchOutside, rows: found.outside.map { Self.searchRow($0, now, quiet: true) }),
            ]
        }
        palette.onPicked = { [weak self] id, _ in
            self?.closePalette()
            self?.openFound(id)
        }

        // The conversations are read in the background. Most of the time they come within a
        // moment, and the palette opens with its rows; a slow disk does not keep it waiting.
        let folders = Store.environments().environments.map { environment in
            (environment: environment.name,
             config: environment.configDirectories.first ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home))
        }
        let shelf = self.shelf
        Task { @MainActor [weak self] in
            let loading = Task { await shelf.load(folders) }
            let early = await withTaskGroup(of: [Conversation]?.self) { group in
                group.addTask { await loading.value }
                group.addTask {
                    try? await Task.sleep(nanoseconds: Self.paletteWait)
                    return nil
                }
                let first = await group.next() ?? nil
                group.cancelAll()
                return first
            }

            guard let self else { return }
            if let early {
                self.conversations = early
                palette.refresh()
                self.showPalette(palette)
                return
            }

            self.showPalette(palette)
            self.conversations = await loading.value
            if self.paletteOpen { palette.refresh() }
        }
    }

    /// A choice of the palette: a session of the list opens, a closed one comes back, and a
    /// conversation started outside Aiko joins the list and continues with --resume.
    func openFound(_ id: String) {
        let now = Self.now()
        if sessions.find(id) != nil {
            sessions = sessions.reopen(id, now: now)
        } else if let conversation = conversations.first(where: { $0.id == id }) {
            sessions = sessions.found(conversation, now: now)
        }

        guard let session = sessions.find(id) else { return }
        save()
        model = model.toEnvironment(session.environment, sessions).toSession(id, sessions)
        draw()
    }

    private static func searchRow(_ hit: SearchHit, _ now: Date, quiet: Bool) -> PaletteRow {
        var when = SessionListWords.usedWords(hit.lastUsed, now)
        // "Yesterday, 16:02" stands in the middle of the line here, so it starts small.
        if let first = when.first {
            when = first.lowercased() + when.dropFirst()
        }

        let place: String
        switch hit.source {
        case nil: place = hit.environment
        case .terminal: place = Strings.terminalSourceTerminal
        case .vsCode: place = "VS Code"
        case .desktop: place = "Claude Desktop"
        case .other: place = Strings.terminalSourceOther
        }
        let meta = [place, hit.folder, when].filter { !$0.isEmpty }.joined(separator: " · ")
        return PaletteRow(id: hit.id, name: hit.name, meta: meta, snippet: hit.snippet, quiet: quiet)
    }

    // ---- the palette over the window ----

    /// The palette at the top of the window, in the middle, over the terminal. A click anywhere
    /// else closes it, as Esc does.
    func showPalette(_ palette: PaletteState) {
        guard let window, let root = window.contentView else { return }
        closePalette()
        palette.onClosed = { [weak self] in self?.closePalette() }

        let cover = zoomed(PaletteCover(palette: palette, close: { [weak self] in self?.closePalette() }))
        cover.translatesAutoresizingMaskIntoConstraints = false
        root.addSubview(cover)
        NSLayoutConstraint.activate([
            cover.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            cover.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            cover.topAnchor.constraint(equalTo: root.topAnchor),
            cover.bottomAnchor.constraint(equalTo: root.bottomAnchor),
        ])
        paletteCover = cover
        window.makeFirstResponder(cover)
    }

    func closePalette() {
        guard let cover = paletteCover else { return }
        cover.removeFromSuperview()
        paletteCover = nil
        draw()
    }

    var paletteOpen: Bool { paletteCover != nil }
}

/// The room of the palette: the card under the title bar, in the middle, and the rest of the window
/// takes the click that closes it.
private struct PaletteCover: View {
    @ObservedObject var palette: PaletteState
    let close: () -> Void

    var body: some View {
        GeometryReader { room in
            ZStack(alignment: .top) {
                Color.black.opacity(0.001)
                    .onTapGesture(perform: close)
                PaletteView(state: palette)
                    .frame(width: max(240, min(PaletteView.cardWidth, room.size.width - 32)))
                    .padding(.top, TitleBarView.height + 8)
                    .transition(.opacity.combined(with: .scale(scale: 0.98, anchor: .top)))
            }
        }
    }
}
