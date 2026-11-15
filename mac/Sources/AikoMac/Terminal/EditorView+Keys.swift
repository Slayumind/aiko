import AikoKit
import AppKit

/// The keys of the editor: what Enter, Esc, the arrows and Tab do. The twin of
/// TerminalEditor.Keys.cs and of Act in TerminalEditor.Checks.cs.
extension EditorView {
    func state() -> EditorState {
        let lines = caretLines()
        return EditorState(
            kind: session?.kind ?? .claude,
            empty: text.string.isEmpty && draft.pictures.isEmpty,
            firstLine: lines.first,
            lastLine: lines.last,
            passing: draft.passing,
            composing: composing)
    }

    /// A key with its modifiers; true when the editor used it up. The live keys come here, and so do
    /// the keys of the check door, which has no keyboard.
    func press(_ keyCode: UInt16, _ flags: NSEvent.ModifierFlags) -> Bool {
        let shift = flags.contains(.shift)
        let control = flags.contains(.control)
        let other = flags.contains(.option) || flags.contains(.command)

        if choices.isOpen, !control, !other, choiceKey(keyCode) {
            return true
        }

        let key: EditorKey?
        switch keyCode {
        case _ where other: key = nil
        case 8 where control && !shift: key = .interrupt
        case _ where control: key = nil
        case 126: key = .up
        case 125: key = .down
        case 123: key = .left
        case 124: key = .right
        case 36, 76: key = .enter
        case 53: key = .escape
        case 48: key = .tab
        case 51: key = .backspace
        default: key = nil
        }
        guard let key else { return false }
        return act(EditorKeys.press(state(), key, shift: shift))
    }

    /// A digit, "/" or a letter while the editor passes keys to a menu of the program. True when it
    /// went to the program; otherwise the text view takes it.
    func typed(_ character: Character) -> Bool {
        let press = EditorKeys.press(state(), .character, shift: false, character: character)
        return press.action == .pass && act(press)
    }

    /// Does what the core said. True when the key was used up.
    @discardableResult
    func act(_ press: EditorPress) -> Bool {
        draft.passing = press.passing
        showHint()
        switch press.action {
        case .pass:
            passed?(press.sequence)
        case .send:
            send()
        case .newLine:
            text.insertPlain("\n")
        case .historyBack:
            walk(draft.history.back(text.string))
        case .historyForward:
            walk(draft.history.forward())
        case .complete:
            complete()
        case .insert:
            let inserts = EditorSend.inserts(text.string, pictures: draft.pictures)
            draft.pictures.removeAll()
            setText("", caret: 0)
            drawPictures()
            hide()
            inserted?(inserts)
        case .hide:
            hide()
        case .keep:
            return false
        }
        return true
    }

    private func walk(_ step: (CommandHistory, String?)) {
        draft.history = step.0
        if let line = step.1 {
            setText(line, caret: (line as NSString).length)
        }
    }

    private func send() {
        let typed = text.string
        let writes = EditorSend.writes(typed, pictures: draft.pictures)
        guard !writes.isEmpty else { return }

        if session?.kind == .shell {
            draft.history = draft.history.sent(typed)
        }
        draft.pictures.removeAll()
        setText("", caret: 0)
        drawPictures()
        sent?(writes)
    }

    /// Whether the caret stands on the first and on the last line the field shows, wrapped lines
    /// counted, as the line index of the text box of Windows counts them.
    private func caretLines() -> (first: Bool, last: Bool) {
        let length = (text.string as NSString).length
        guard length > 0, let layout = text.layoutManager, let container = text.textContainer else {
            return (true, true)
        }
        layout.ensureLayout(for: container)
        let caret = min(text.selectedRange().location, length)
        let here = lineTop(caret, length, layout)
        return (here <= lineTop(0, length, layout) + 0.5, here >= lineTop(length, length, layout) - 0.5)
    }

    private func lineTop(_ index: Int, _ length: Int, _ layout: NSLayoutManager) -> CGFloat {
        if index >= length {
            // After a line break the caret stands on a line of its own that has no letter yet.
            if layout.extraLineFragmentTextContainer != nil {
                return layout.extraLineFragmentRect.minY
            }
            return layout.lineFragmentRect(forGlyphAt: layout.glyphIndexForCharacter(at: length - 1), effectiveRange: nil).minY
        }
        return layout.lineFragmentRect(forGlyphAt: layout.glyphIndexForCharacter(at: index), effectiveRange: nil).minY
    }
}
