import AikoKit
import AppKit
import SwiftTerm

/// The keys of the window, the twin of TerminalHost.Keys.cs: the core's table says what a key does
/// (TerminalKeys), this part does it. On the Mac the key of Aiko is Cmd where Windows has Ctrl
/// (D-425); copy and paste stay with the terminal and the Edit menu, as in every Mac app.
extension TerminalHost {
    /// Listens to the keys while the window is the key window. A key the table does not take goes on
    /// to the terminal or the field that has it.
    func watchKeys() {
        guard keyWatch == nil else { return }
        keyWatch = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            let taken = MainActor.assumeIsolated { () -> Bool in
                guard let self, event.window === self.window else { return false }
                return self.keyPressed(event)
            }
            return taken ? nil : event
        }
    }

    /// A key pressed with Cmd (and Shift), as if the person did it: for --try-terminal.
    func pressForCheck(_ keyCode: UInt16, shift: Bool) -> Bool {
        var flags: NSEvent.ModifierFlags = [.command]
        if shift { flags.insert(.shift) }
        guard let event = NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: flags, timestamp: 0,
            windowNumber: window?.windowNumber ?? 0, context: nil, characters: "", charactersIgnoringModifiers: "",
            isARepeat: false, keyCode: keyCode) else { return false }
        return keyPressed(event)
    }

    /// True when the key was taken.
    private func keyPressed(_ event: NSEvent) -> Bool {
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        let shift = flags.contains(.shift)

        // Cmd+Tab belongs to macOS, so the next session is Ctrl+Tab, as the tabs of Safari are.
        if flags.contains(.control), !flags.contains(.command), event.keyCode == 48 {
            return run(TerminalKeys.action("Tab", shift: shift, keyState()))
        }

        guard flags.contains(.command), !flags.contains(.control), !flags.contains(.option),
              let name = Self.keyName(event) else { return false }

        let action = TerminalKeys.action(name, shift: shift, keyState())
        return run(action)
    }

    private func keyState() -> TerminalKeyState {
        let inTerminal = window?.firstResponder is TerminalView
        return TerminalKeyState(
            settingsOpen: settings != nil,
            claudeSession: model.session(sessions)?.kind == .claude,
            inTerminal: inTerminal,
            hasSelection: false)
    }

    /// Does what the table said. Copy and paste are left to the terminal; the editor, the voice and
    /// the search in the output come to the Mac with the parts they belong to.
    private func run(_ action: TerminalKeyAction) -> Bool {
        switch action {
        case .toggleSettings:
            toggleSettings()
        case .zoomIn:
            zoom(WindowZoom.larger)
        case .zoomOut:
            zoom(WindowZoom.smaller)
        case .zoomReset:
            zoom { _ in WindowZoom.defaultZoom }
        case .newSessionQuickly:
            pickFolderQuickly()
        case .palette:
            openSessionPalette()
        case .toggleList:
            toggleList()
        case .nextSession:
            goRound(1)
        case .previousSession:
            goRound(-1)
        case .none, .copy, .paste, .compose, .find, .voice:
            return false
        }
        return true
    }

    /// The name of a key in the table of the core: a letter, a digit, Comma, Plus, Minus or Space.
    /// The place of the key decides, not the letter it types: with a Russian layout Cmd+Shift+T
    /// types a Cyrillic letter, and the key must still open a new session, as on Windows.
    private static func keyName(_ event: NSEvent) -> String? {
        keyNames[event.keyCode]
    }

    /// The key codes of the ANSI layout that the table names.
    private static let keyNames: [UInt16: String] = [
        0: "A", 11: "B", 8: "C", 2: "D", 14: "E", 3: "F", 5: "G", 4: "H", 34: "I", 38: "J", 40: "K",
        37: "L", 46: "M", 45: "N", 31: "O", 35: "P", 12: "Q", 15: "R", 1: "S", 17: "T", 32: "U",
        9: "V", 13: "W", 7: "X", 16: "Y", 6: "Z",
        29: "0", 18: "1", 19: "2", 20: "3", 21: "4", 23: "5", 22: "6", 26: "7", 28: "8", 25: "9",
        43: "Comma", 24: "Plus", 69: "Plus", 27: "Minus", 78: "Minus", 82: "0", 49: "Space",
    ]

    private func goRound(_ step: Int) {
        let next = model.toNeighbour(step, sessions)
        guard next.sessionId != nil, next != model else { return }
        model = next
        draw()
    }

    /// One step of the zoom, saved at once: the window and the terminal follow it.
    private func zoom(_ step: (Int) -> Int) {
        var settings = Store.settings()
        let next = step(WindowZoom.read(settings.windowZoom))
        guard next != settings.windowZoom else { return }
        settings.windowZoom = next
        Store.saveSettings(settings)
        applyZoom()
    }

    /// The font of every terminal at the zoom of the settings: as large as the text of the window,
    /// the twin of WindowZoom.TerminalPoints on Windows, which counts in the points of Windows.
    func applyZoom() {
        let font = Self.terminalFont()
        for terminal in terminals.values where terminal.font != font {
            terminal.font = font
        }
    }

    static func terminalFont() -> NSFont {
        Theme.monoFont(Theme.textNumber * CGFloat(WindowZoom.scale(Store.settings().windowZoom)))
    }
}
