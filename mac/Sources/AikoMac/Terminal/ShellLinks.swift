import AikoKit
import AppKit

/// Opens what Cmd+click found in the output of a command line (D-358), the twin of ShellLinks.cs:
/// an address in the browser, a file in VS Code at its line when VS Code is there, otherwise in the
/// app macOS gives it. A file that is not on the disk opens nothing.
@MainActor
enum ShellLinks {
    static func open(_ link: String, folder: String) {
        if link.lowercased().hasPrefix("http") {
            if let url = URL(string: link) { NSWorkspace.shared.open(url) }
            return
        }
        guard let file = FileLink.parse(link) else { return }
        let path = (file.path as NSString).isAbsolutePath ? file.path : (folder as NSString).appendingPathComponent(file.path)
        guard FileManager.default.fileExists(atPath: path) else { return }

        if let code = vsCode() {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: code)
            process.arguments = ["-g", file.line.map { "\(path):\($0)" } ?? path]
            process.environment = ProcessEnvironment.variables([])
            if (try? process.run()) != nil { return }
        }
        NSWorkspace.shared.open(URL(fileURLWithPath: path))
    }

    /// The code command of VS Code on the PATH of a login shell, or inside the app where it lives.
    private static func vsCode() -> String? {
        let path = ProcessEnvironment.variables([])["PATH"] ?? ""
        let found = path.split(separator: ":").map { ($0 as NSString).appendingPathComponent("code") }
            .first { FileManager.default.isExecutableFile(atPath: $0) }
        return found ?? ["/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"]
            .first { FileManager.default.isExecutableFile(atPath: $0) }
    }
}

/// The search in the output of a command line (Cmd+Shift+F, D-358), the twin of ShellFindBar.cs: a
/// field in the top right corner over the blocks, with how many places it found. The search starts
/// from the newest output at the bottom: Enter goes to the place above, Shift+Enter to the one
/// below, Esc closes it.
@MainActor
final class ShellFindBar: NSView, NSTextFieldDelegate {
    private let view: ShellBlocksView
    private let field = NSTextField()
    private let count = NSTextField(labelWithString: "")

    /// The search closed: the keys go back to the editor.
    var closed: (() -> Void)?

    init(view: ShellBlocksView) {
        self.view = view
        super.init(frame: .zero)
        wantsLayer = true
        layer?.backgroundColor = Theme.nsSurface.cgColor
        layer?.borderColor = Theme.nsHairline.cgColor
        layer?.borderWidth = 1
        layer?.cornerRadius = 8
        isHidden = true

        field.isBordered = false
        field.drawsBackground = false
        field.focusRingType = .none
        field.textColor = Theme.nsInk
        field.font = Theme.monoFont(Theme.textNumber)
        field.toolTip = Strings.shellFind
        field.delegate = self
        count.textColor = Theme.nsMuted
        count.font = Theme.monoFont(Theme.textNumber)

        for part in [field, count] as [NSView] {
            part.translatesAutoresizingMaskIntoConstraints = false
            addSubview(part)
        }
        NSLayoutConstraint.activate([
            field.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 10),
            field.widthAnchor.constraint(greaterThanOrEqualToConstant: 200),
            field.centerYAnchor.constraint(equalTo: centerYAnchor),
            count.leadingAnchor.constraint(equalTo: field.trailingAnchor, constant: 10),
            count.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -10),
            count.centerYAnchor.constraint(equalTo: centerYAnchor),
            heightAnchor.constraint(equalTo: field.heightAnchor, constant: 10),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    /// The bar and its room above the blocks: 8 from the top and its own height.
    func open() {
        isHidden = false
        window?.makeFirstResponder(field)
        field.currentEditor()?.selectAll(nil)
        layoutSubtreeIfNeeded()
        view.makeRoom(frame.height + 8)
        search()
    }

    func close() {
        isHidden = true
        view.makeRoom(0)
        view.find("")
        count.stringValue = ""
        closed?()
    }

    func controlTextDidChange(_ notification: Notification) {
        search()
    }

    func control(_ control: NSControl, textView: NSTextView, doCommandBy selector: Selector) -> Bool {
        switch selector {
        case #selector(NSResponder.insertNewline(_:)):
            let shift = NSApp.currentEvent?.modifierFlags.contains(.shift) == true
            view.findNext(shift ? 1 : -1)
            count.stringValue = view.findStatus
            return true
        case #selector(NSResponder.cancelOperation(_:)):
            close()
            return true
        default:
            return false
        }
    }

    private func search() {
        view.find(field.stringValue)
        count.stringValue = view.findStatus
    }

    func searchForCheck(_ text: String) {
        if isHidden {
            isHidden = false
            layoutSubtreeIfNeeded()
            view.makeRoom(frame.height + 8)
        }
        field.stringValue = text
        search()
    }

    var countForCheck: String { count.stringValue }
}
