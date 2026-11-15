import AikoKit
import AppKit
import SwiftTerm

/// The editor under the terminal (D-354, D-369, D-370) and what is pasted or dropped on the terminal.
/// The twin of the editor part of TerminalHost.cs and of TerminalPanel.cs, where the editor and the
/// line of keys stand under the terminal.
extension TerminalHost {
    /// From the bottom of the stage up: the line of keys, the editor, then the terminal.
    func layStage() {
        keysLine.textColor = Theme.nsMuted
        keysLine.lineBreakMode = .byTruncatingTail
        keysLine.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        for view in [editor, keysLine] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            stage.addSubview(view)
        }

        let collapsed = editor.heightAnchor.constraint(equalToConstant: 0)
        editorCollapsed = collapsed
        let keysLeft = keysLine.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 16)
        let keysRight = keysLine.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -16)
        let keysTop = keysLine.topAnchor.constraint(equalTo: editor.bottomAnchor, constant: 4)
        let keysBottom = keysLine.bottomAnchor.constraint(equalTo: stage.bottomAnchor, constant: -6)
        NSLayoutConstraint.activate([
            editor.leadingAnchor.constraint(equalTo: stage.leadingAnchor),
            editor.trailingAnchor.constraint(equalTo: stage.trailingAnchor),
            keysLeft, keysRight, keysTop, keysBottom,
            collapsed,
        ])

        zoomFollowers.append { [weak self] zoom in
            guard let self else { return false }
            self.editor.zoom = zoom
            self.keysLine.font = Theme.monoFont(Theme.textTiny * zoom)
            keysLeft.constant = 16 * zoom
            keysRight.constant = -16 * zoom
            keysTop.constant = 4 * zoom
            keysBottom.constant = -6 * zoom
            return true
        }
        wireEditor()
    }

    private func wireEditor() {
        editor.passed = { [weak self] sequence in self?.openTerminal()?.send(txt: sequence) }
        editor.inserted = { [weak self] writes in self?.writeInTurn(writes) }
        editor.sent = { [weak self] writes in self?.writeInTurn(writes) }
        editor.wantedChanged = { [weak self] in
            guard let self else { return }
            self.placeEditor()
            DispatchQueue.main.async { self.focusOpenTerminal() }
        }
    }

    /// The editor of the open session, or none: a program that ended takes no text.
    func showEditor(for session: TerminalSession?, terminal: LocalProcessTerminalView?) {
        if let session {
            editor.show(EditorSession(
                id: session.id, kind: session.kind, folder: session.folder,
                picturesFolder: Store.folders.picturesFolder(session.id)))
            keysLine.stringValue = session.kind == .shell ? Strings.terminalKeysShell : Strings.terminalKeysClaude
        }
        editorRoom = terminal != nil && session.map { !exited.contains($0.id) } == true
        placeEditor()
    }

    /// The editor stands under the terminal while it is wanted and there is room for it.
    func placeEditor() {
        let shown = editorRoom && editor.wanted
        editor.isHidden = !shown
        editorCollapsed?.isActive = !shown
        if !shown {
            editor.choices.close()
        }
    }

    /// The editor takes the keys again after a palette, a dialog or a click in the terminal: the
    /// keyboard is always there (D-354, D-369).
    func focusOpenTerminal() {
        guard settings == nil else { return }
        if editorRoom && editor.wanted {
            editor.focusText()
        } else if let terminal = openTerminal() {
            window?.makeFirstResponder(terminal)
        }
    }

    func openTerminal() -> LocalProcessTerminalView? {
        guard let id = model.session(sessions)?.id else { return nil }
        return terminals[id]
    }

    /// The writes of one message, one after another with a breath between them: Claude Code takes a
    /// paste and the Enter after it as one thing when they come together (spike 2026-10).
    func writeInTurn(_ writes: [String]) {
        guard let terminal = openTerminal(), !writes.isEmpty else { return }
        Task { @MainActor in
            for (index, write) in writes.enumerated() {
                if index > 0 {
                    try? await Task.sleep(for: .milliseconds(60))
                }
                terminal.send(txt: write)
            }
        }
    }

    /// The handlers of a new terminal: a paste, a drop, and a click that gives the keys back.
    func wire(_ terminal: AikoTerminalView, _ id: String) {
        terminal.pasted = { [weak self] in
            self?.writeInTurn(TerminalPaste.fromPasteboard(.general, picturesFolder: Store.folders.picturesFolder(id)))
        }
        terminal.dropped = { [weak self] paths in self?.dropped(paths) }
        terminal.clicked = { [weak self] in
            // In a session of Claude Code the keys stay with the terminal (D-370).
            guard let self, self.editor.wanted else { return }
            DispatchQueue.main.async { self.focusOpenTerminal() }
        }
    }

    /// Files dropped on the terminal: into the editor of a command line, or into the line of Claude Code.
    private func dropped(_ paths: [String]) {
        if editorRoom && editor.wanted {
            editor.dropFiles(paths)
        } else if let terminal = openTerminal() {
            writeInTurn(TerminalPaste.forFiles(paths))
            window?.makeFirstResponder(terminal)
        }
    }

    /// A closed session takes its draft and its pasted pictures with it.
    func forgetEditor(_ id: String) {
        editor.forget(id)
        let folder = Store.folders.picturesFolder(id)
        // Claude Code may still read one; the folder of a closed session is not used again.
        try? FileManager.default.removeItem(atPath: folder)
    }
}
