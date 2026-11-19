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
        for view in [stoppedBar, editor, keysLine] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            stage.addSubview(view)
        }

        let frame = zoomed(TerminalFrameView(state: state))
        frame.translatesAutoresizingMaskIntoConstraints = false
        stage.addSubview(frame, positioned: .below, relativeTo: nil)
        stage.addLayoutGuide(terminalRoom)

        let collapsed = editor.heightAnchor.constraint(equalToConstant: 0)
        editorCollapsed = collapsed
        let stoppedAway = stoppedBar.heightAnchor.constraint(equalToConstant: 0)
        stoppedCollapsed = stoppedAway
        stoppedBar.isHidden = true
        stoppedBar.resume = { [weak self] in self?.resume() }
        // The margins of the two frames and the padding inside the terminal frame, as
        // _terminalFrame and _editorFrame in TerminalPanel.Signal.cs.
        let frameLeft = frame.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 12)
        let frameRight = frame.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -12)
        let frameTop = frame.topAnchor.constraint(equalTo: stage.topAnchor, constant: 8)
        let roomLeft = terminalRoom.leadingAnchor.constraint(equalTo: frame.leadingAnchor, constant: 9)
        let roomRight = terminalRoom.trailingAnchor.constraint(equalTo: frame.trailingAnchor, constant: -9)
        let roomTop = terminalRoom.topAnchor.constraint(equalTo: frame.topAnchor, constant: TerminalFrameView.top(zoom: 1))
        let roomBottom = terminalRoom.bottomAnchor.constraint(equalTo: frame.bottomAnchor, constant: -7)
        let editorLeft = editor.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 12)
        let editorRight = editor.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -12)
        let gap = stoppedBar.bottomAnchor.constraint(equalTo: editor.topAnchor)
        editorGap = gap
        let keysLeft = keysLine.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 16)
        let keysRight = keysLine.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -16)
        let keysTop = keysLine.topAnchor.constraint(equalTo: editor.bottomAnchor, constant: 4)
        let keysBottom = keysLine.bottomAnchor.constraint(equalTo: stage.bottomAnchor, constant: -6)
        NSLayoutConstraint.activate([
            frameLeft, frameRight, frameTop,
            frame.bottomAnchor.constraint(equalTo: stoppedBar.topAnchor),
            roomLeft, roomRight, roomTop, roomBottom,
            editorLeft, editorRight,
            keysLeft, keysRight, keysTop, keysBottom,
            collapsed,
            stoppedBar.leadingAnchor.constraint(equalTo: stage.leadingAnchor),
            stoppedBar.trailingAnchor.constraint(equalTo: stage.trailingAnchor),
            gap,
            stoppedAway,
        ])

        zoomFollowers.append { [weak self] zoom in
            guard let self else { return false }
            self.editor.zoom = zoom
            self.stoppedBar.zoom = zoom
            self.keysLine.font = Theme.monoFont(Theme.textTiny * zoom)
            frameLeft.constant = 12 * zoom
            frameRight.constant = -12 * zoom
            frameTop.constant = 8 * zoom
            roomLeft.constant = 9 * zoom
            roomRight.constant = -9 * zoom
            roomTop.constant = TerminalFrameView.top(zoom: zoom)
            roomBottom.constant = -7 * zoom
            editorLeft.constant = 12 * zoom
            editorRight.constant = -12 * zoom
            self.placeEditor()
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
        editor.suggestionAsked = { [weak self] words in self?.suggest(words) }
        editor.inserted = { [weak self] writes in self?.writeInTurn(writes) }
        editor.sent = { [weak self] writes in self?.writeInTurn(writes) }
        editor.wantedChanged = { [weak self] in
            guard let self else { return }
            self.placeEditor()
            DispatchQueue.main.async { self.focusOpenTerminal() }
        }

        let voice = VoiceDictation(field: editor) { [weak self] in self?.voiceContext() }
        voice.settingsAsked = { [weak self] in self?.openSettings(at: .terminal) }
        voice.use(Store.settings())
        self.voice = voice
    }

    /// What the hint of a recording is made of: the folder of the open session (D-313, D-424).
    private func voiceContext() -> VoiceContext? {
        model.session(sessions).map { VoiceContext(folder: $0.folder, files: []) }
    }

    /// The editor of the open session, or none: a program that ended takes no text.
    func showEditor(for session: TerminalSession?, terminal: LocalProcessTerminalView?) {
        if let session {
            editor.show(EditorSession(
                id: session.id, kind: session.kind, folder: session.folder,
                picturesFolder: Store.folders.picturesFolder(session.id)))
            keysLine.stringValue = session.kind == .shell ? Strings.terminalKeysShell : Strings.terminalKeysClaude
        }
        editorRoom = terminal != nil && session.map { exitedAt[$0.id] == nil } == true
        // A program that ended takes no text: the bar to continue it stands in place of the editor.
        let stopped = terminal != nil && session.map { exitedAt[$0.id] != nil } == true
        stoppedBar.isHidden = !stopped
        stoppedCollapsed?.isActive = !stopped
        placeEditor()
    }

    /// The editor stands under the terminal while it is wanted and there is room for it.
    func placeEditor() {
        let shown = editorRoom && editor.wanted
        editor.isHidden = !shown
        editorCollapsed?.isActive = !shown
        editorGap?.constant = shown ? -10 * zoom : 0
        if !shown {
            editor.choices.close()
        }
    }

    /// The editor takes the keys again after a palette, a dialog or a click in the terminal: the
    /// keyboard is always there (D-354, D-369).
    func focusOpenTerminal() {
        guard settings == nil else { return }
        if !stoppedBar.isHidden {
            // Enter presses the button: the terminal of a program that ended takes no keys.
            stoppedBar.takeKeys()
        } else if editorRoom && editor.wanted {
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
        terminal.typedByPerson = { [weak self] in self?.typed(id) }
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
