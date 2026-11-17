import AikoKit
import AppKit

/// The list over the editor: files after an at sign and paths of Tab. The twin of
/// TerminalEditor.Choices.cs.
extension EditorView {
    /// The keys of an open list: the arrows move the light, Enter or Tab picks, Esc closes.
    func choiceKey(_ keyCode: UInt16) -> Bool {
        switch keyCode {
        case 126, 125:
            choices.move(keyCode == 125 ? 1 : -1)
        case 36, 76, 48:
            choose(choices.lit)
        case 53:
            choices.close()
        default:
            return false
        }
        return true
    }

    /// "@" in a session of Claude Code offers the files of its folder (FileMention).
    func offer() {
        if offersPaths {
            return
        }

        mention = session?.kind == .claude && (hasKeys || checkWithoutFocus)
            ? FileMention.query(text.string, caret: text.selectedRange().location)
            : nil
        guard let mention, let session else {
            choices.close()
            return
        }
        guard let files = ProjectFiles.get(session.folder, ready: { [weak self] in self?.offer() }) else {
            return
        }

        offered = FileMention.matches(files, query: mention.text)
        choices.show(offered.map { ($0, FileMention.lit($0, query: mention.text)) }, under: self, left: caretLeft())
    }

    func complete() {
        guard let session,
              let done = PathCompletion.complete(
                text.string, caret: text.selectedRange().location, folder: session.folder, separator: "/",
                list: Self.list)
        else { return }

        setText(done.text, caret: done.caret)
        offered = done.choices
        offersPaths = !done.choices.isEmpty
        if offersPaths {
            choices.show(done.choices.map { ($0, (start: 0, length: 0)) }, under: self, left: caretLeft())
        }
    }

    func choose(_ index: Int) {
        guard index >= 0, index < offered.count else { return }

        let choice = offered[index]
        choices.close()
        if offersPaths {
            // The choice takes the place of the word before the caret, then Tab goes on from it.
            offersPaths = false
            let units = Array(text.string.utf16)
            let caret = min(text.selectedRange().location, units.count)
            var start = caret
            while start > 0, !Self.endsWord(units[start - 1]) {
                start -= 1
            }
            let before = String(decoding: units[..<start], as: UTF16.self)
            let after = String(decoding: units[caret...], as: UTF16.self)
            setText(before + choice + after, caret: start + choice.utf16.count)
        } else if let mention {
            let chosen = FileMention.chosen(text.string, query: mention, caret: text.selectedRange().location, path: choice)
            setText(chosen.text, caret: chosen.caret)
        }
        focusText()
    }

    /// A space, "/", "\" or a quote stands before the word Tab completes.
    private static func endsWord(_ unit: UTF16.CodeUnit) -> Bool {
        if unit == 0x2F || unit == 0x5C || unit == 0x22 {
            return true
        }
        return Unicode.Scalar(unit)?.properties.isWhitespace ?? false
    }

    /// Where the caret stands, from the left of the field, in points of the text view.
    private func caretLeft() -> CGFloat {
        guard let window = text.window else { return 0 }
        let caret = NSRange(location: text.selectedRange().location, length: 0)
        let onScreen = text.firstRect(forCharacterRange: caret, actualRange: nil)
        let inWindow = window.convertFromScreen(onScreen)
        return max(0, text.convert(inWindow.origin, from: nil).x)
    }

    private static func list(_ folder: String) -> [FolderEntry] {
        let path = folder.hasPrefix("~") ? Store.home + folder.dropFirst() : folder
        guard let names = try? FileManager.default.contentsOfDirectory(atPath: path) else { return [] }
        return names.prefix(2000).map { name in
            var isFolder: ObjCBool = false
            FileManager.default.fileExists(atPath: (path as NSString).appendingPathComponent(name), isDirectory: &isFolder)
            return FolderEntry(name, isFolder.boolValue)
        }
    }
}
