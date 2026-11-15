import AikoKit
import AppKit

/// Pictures and files in the editor: pasted, dropped and sent as paths. The twin of
/// TerminalEditor.Attachments.cs.
extension EditorView {
    /// Cmd+V in the field. True when the pasteboard held files or a picture and they were taken;
    /// false leaves plain text to the text view.
    func pasteFromPasteboard(_ pasteboard: NSPasteboard) -> Bool {
        let files = TerminalPaste.files(on: pasteboard)
        if !files.isEmpty {
            take(files)
            return true
        }
        if let picture = TerminalPaste.picture(on: pasteboard) {
            attach(picture)
            return true
        }
        return false
    }

    /// Files dropped on the terminal of a command line, or while the editor is open.
    func dropFiles(_ paths: [String]) {
        take(paths)
        focusText()
    }

    /// A picture file goes as a picture; any other file or folder is its path in the text, in quotes
    /// when it has a space.
    func take(_ paths: [String]) {
        if dictating {
            return
        }
        var words: [String] = []
        for path in paths {
            if TerminalPaste.isPicture(path), let picture = TerminalPaste.load(path) {
                attach(picture)
            } else {
                words.append(path)
            }
        }

        if !words.isEmpty {
            let dropped = FileMention.dropped(text.string, caret: text.selectedRange().location, paths: words)
            setText(dropped.text, caret: dropped.caret)
        }
    }

    /// The picture is written as a PNG into the folder of the session; its path goes to Claude Code.
    private func attach(_ picture: CGImage) {
        guard let session, let path = TerminalPaste.save(picture, folder: session.picturesFolder) else { return }
        draft.pictures.append(path)
        drawPictures()
        showHint()
    }

    /// The pictures as small labels above the text (D-362): "picture 1", with a cross.
    func drawPictures() {
        pictures.subviews.forEach { $0.removeFromSuperview() }
        for (index, path) in draft.pictures.enumerated() {
            let chip = PictureChip(
                title: Strings.format(Strings.terminalEditorPicture, index + 1), path: path, zoom: zoom
            ) { [weak self] in
                guard let self, index < self.draft.pictures.count else { return }
                self.draft.pictures.remove(at: index)
                self.drawPictures()
                self.showHint()
            }
            pictures.addSubview(chip)
        }
        pictures.isHidden = draft.pictures.isEmpty
        pictures.invalidateIntrinsicContentSize()
        pictures.needsLayout = true
    }
}
