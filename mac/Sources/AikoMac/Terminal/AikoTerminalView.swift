import AikoKit
import AppKit
import SwiftTerm

/// The terminal of a session with what SwiftTerm does not do on its own: files dropped on it, and a
/// paste that can carry a picture or a file (D-370). The host decides where they go: into the editor
/// when it is on screen, or straight into the line of the program.
final class AikoTerminalView: LocalProcessTerminalView {
    /// Cmd+V or Edit > Paste while the terminal has the keys.
    var pasted: (() -> Void)?

    /// Files dropped on the terminal, as paths.
    var dropped: (([String]) -> Void)?

    /// Output of the program, as it came: a command line cuts it into blocks.
    var received: ((ArraySlice<UInt8>) -> Void)?

    /// Whether the program has drawn a letter yet (D-368): until then the window shows the loader.
    /// A letter is anything but the sequences that clear, move the cursor, colour or name the window.
    private(set) var drawn = false

    /// The first letter came.
    var drewFirst: (() -> Void)?

    override func dataReceived(slice: ArraySlice<UInt8>) {
        super.dataReceived(slice: slice)
        received?(slice)
        if !drawn, TerminalScreen.hasText(String(decoding: slice, as: UTF8.self)) {
            drawn = true
            drewFirst?()
        }
    }

    /// A click ended in the terminal: a selection may have been made there.
    var clicked: (() -> Void)?

    /// Input went to the program that the person typed, here or through the editor: a question the
    /// program asked is answered (TerminalPulse.isTyped tells it from what the terminal sends itself).
    var typedByPerson: (() -> Void)?

    override init(frame: CGRect) {
        super.init(frame: frame)
        registerForDraggedTypes([.fileURL])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    override func paste(_ sender: Any) {
        if let pasted {
            pasted()
        } else {
            super.paste(sender)
        }
    }

    /// Cmd+C or Edit > Copy: the rows the program wrapped by the width are joined again, so a
    /// paragraph comes out whole (D-371), as Ctrl+C does on Windows.
    override func copy(_ sender: Any) {
        guard let text = copiedText() else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    /// The selection ready for the clipboard, or nil when nothing is selected.
    func copiedText() -> String? {
        guard let text = getSelection(), !text.isEmpty else { return nil }
        return TerminalCopy.unwrap(text, columns: getTerminal().cols)
    }

    override func send(source: TerminalView, data: ArraySlice<UInt8>) {
        if let typedByPerson, TerminalPulse.isTyped(String(decoding: data, as: UTF8.self)) {
            typedByPerson()
        }
        super.send(source: source, data: data)
    }

    override func mouseUp(with event: NSEvent) {
        super.mouseUp(with: event)
        clicked?()
    }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        Self.files(sender).isEmpty ? [] : .copy
    }

    override func draggingUpdated(_ sender: NSDraggingInfo) -> NSDragOperation {
        Self.files(sender).isEmpty ? [] : .copy
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        let files = Self.files(sender)
        guard !files.isEmpty, let dropped else { return false }
        dropped(files)
        return true
    }

    private static func files(_ drag: NSDraggingInfo) -> [String] {
        let urls = drag.draggingPasteboard.readObjects(
            forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL]
        return urls?.map(\.path) ?? []
    }
}
