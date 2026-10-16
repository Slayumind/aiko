import AikoKit
import AppKit
import SwiftUI
import UniformTypeIdentifiers

/// The field of a conversation (D-294, D-302): the text, the pictures that go with it, and what
/// was sent in each session for the arrows. The twin of Terminal/TalkInput.cs.
@MainActor
final class TalkInputState: ObservableObject {
    /// Pictures larger than this are made smaller before they go: Claude Code takes up to about
    /// 5 MB of a picture, and a screenshot of a large screen is often more as PNG.
    private static let largestSide: CGFloat = 1800

    private static let pictureFiles: Set<String> = ["png", "jpg", "jpeg", "gif", "webp", "bmp", "heic", "tiff"]

    @Published var text = ""
    @Published private(set) var attached: [AttachedImage] = []

    var sent: ((String, [AttachedImage]) -> Void)?
    var stopAsked: (() -> Void)?
    var modeAsked: (() -> Void)?
    var modelAsked: (() -> Void)?

    /// Return in an empty field and Esc while a card waits answer it (D-323).
    var chosen: ((String, CardChoice) -> Void)?

    /// The field has the keyboard, so Cmd+V with a picture is ours.
    var focused = false

    private var history: [String: [String]] = [:]
    private var session = ""
    private var back = -1
    private var draft = ""
    private var pasteWatch: Any?

    /// A new session starts with an empty field: what was typed for another one stays out of it.
    func show(session next: String) {
        guard next != session else { return }
        session = next
        text = ""
        attached = []
        back = -1
    }

    func send() {
        let words = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !words.isEmpty || !attached.isEmpty else { return }

        if !words.isEmpty {
            history[session, default: []].append(words)
        }
        let pictures = attached
        text = ""
        attached = []
        back = -1
        sent?(words, pictures)
    }

    /// One step through what was sent: up is older, down is newer, past the newest is the draft.
    /// The arrows walk only while the field is empty or shows a line of the history, so they
    /// still move the caret in a text being written.
    func walk(_ step: Int) -> Bool {
        let sent = history[session] ?? []
        guard !sent.isEmpty else { return false }

        if back < 0 {
            guard step < 0, text.isEmpty else { return false }
            draft = text
            back = sent.count
        } else if back < sent.count, text != sent[back] {
            back = -1
            return false
        }

        let next = back + step
        guard next >= 0 else { return true }

        if next >= sent.count {
            back = -1
            text = draft
        } else {
            back = next
            text = sent[next]
        }
        return true
    }

    func remove(at index: Int) {
        guard attached.indices.contains(index) else { return }
        attached.remove(at: index)
    }

    // MARK: - Pictures and files

    /// Cmd+V with a picture in the pasteboard attaches it; copied files are dropped. Text pastes
    /// as it always does. The field editor of AppKit takes the paste first, so it is caught here.
    func watchPaste() {
        guard pasteWatch == nil else { return }
        pasteWatch = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            let paste = event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .command
                && event.charactersIgnoringModifiers == "v"
            guard paste else { return event }
            let taken = MainActor.assumeIsolated { self?.pasteIfFocused() ?? false }
            return taken ? nil : event
        }
    }

    private func pasteIfFocused() -> Bool {
        focused && pasteFromBoard()
    }

    func stopWatchingPaste() {
        if let pasteWatch {
            NSEvent.removeMonitor(pasteWatch)
        }
        pasteWatch = nil
    }

    private func pasteFromBoard() -> Bool {
        let board = NSPasteboard.general
        if let files = board.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL],
           !files.isEmpty {
            take(files)
            return true
        }
        if let image = NSImage(pasteboard: board) {
            attach(image)
            return true
        }
        return false
    }

    /// A picture file is attached; any other file or folder is its path in the text, in quotes
    /// when it has a space.
    func take(_ files: [URL]) {
        var words: [String] = []
        for file in files {
            if Self.pictureFiles.contains(file.pathExtension.lowercased()), let image = NSImage(contentsOf: file) {
                attach(image)
            } else {
                words.append(file.path.contains(" ") ? "\"\(file.path)\"" : file.path)
            }
        }

        guard !words.isEmpty else { return }
        let before = text.isEmpty || text.last?.isWhitespace == true ? "" : " "
        text += before + words.joined(separator: " ") + " "
    }

    private func attach(_ image: NSImage) {
        guard let png = Self.png(image) else { return }
        attached.append(AttachedImage(mediaType: "image/png", base64: png.base64EncodedString()))
    }

    private static func png(_ image: NSImage) -> Data? {
        guard let source = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else { return nil }
        let width = CGFloat(source.width)
        let height = CGFloat(source.height)
        let scale = min(1, largestSide / max(width, height))

        let size = NSSize(width: (width * scale).rounded(), height: (height * scale).rounded())
        guard let bitmap = NSBitmapImageRep(
            bitmapDataPlanes: nil, pixelsWide: Int(size.width), pixelsHigh: Int(size.height),
            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
            colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)
        else {
            return nil
        }

        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
        NSGraphicsContext.current?.cgContext.draw(source, in: CGRect(origin: .zero, size: size))
        NSGraphicsContext.restoreGraphicsState()
        return bitmap.representation(using: .png, properties: [:])
    }
}

/// The field, the stop button and the line "mode · model · environment" under them.
struct TalkInputView: View {
    let talk: TalkScreen
    @ObservedObject var input: TalkInputState

    @FocusState private var focused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            VStack(alignment: .leading, spacing: 8) {
                if !input.attached.isEmpty {
                    HStack(spacing: 12) {
                        ForEach(Array(input.attached.enumerated()), id: \.offset) { at, picture in
                            Picture(base64: picture.base64, side: 48)
                                .overlay(alignment: .topTrailing) {
                                    Button { input.remove(at: at) } label: {
                                        Image(systemName: "xmark")
                                            .font(.system(size: 8, weight: .bold))
                                            .foregroundStyle(Theme.ink)
                                            .frame(width: 18, height: 18)
                                            .background(Theme.raised)
                                            .clipShape(Circle())
                                    }
                                    .buttonStyle(.plain)
                                    .offset(x: 6, y: -6)
                                }
                        }
                    }
                }

                HStack(alignment: .top, spacing: 10) {
                    TextField(Strings.terminalDescribeTask, text: $input.text, axis: .vertical)
                        .textFieldStyle(.plain)
                        .font(Theme.sans(Theme.textName))
                        .foregroundStyle(Theme.ink)
                        .lineLimit(1...8)
                        .focused($focused)
                        .onKeyPress(phases: .down) { press in onKey(press) }

                    // The stop button stands where the paper clip will be (D-308).
                    if talk.busy {
                        Button { input.stopAsked?() } label: {
                            RoundedRectangle(cornerRadius: 2).fill(Theme.ink).frame(width: 8, height: 8)
                                .frame(width: 26, height: 26)
                                .background(Color.white.opacity(0.043))
                                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                                .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous)
                                    .strokeBorder(Theme.inputLine, lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                        .help(Strings.terminalStop)
                        .padding(.top, -1)
                    }
                }
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 12)
            .background(Theme.surface)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(Theme.inputLine, lineWidth: 1))
            .contentShape(Rectangle())
            .onTapGesture { focused = true }
            .onDrop(of: [.fileURL], isTargeted: nil) { providers in drop(providers) }
            .padding(.top, 10)

            // The mode changes with a click or Shift+Tab, in the order of Claude Code (D-302); a
            // click on the model takes the next one.
            HStack(spacing: 0) {
                Text(PermissionModes.name(talk.mode))
                    .onTapGesture { input.modeAsked?() }
                    .help("Shift+Tab")
                if !talk.model.text.isEmpty {
                    Text(" · ")
                    // A model Claude Code has not named yet is quieter than the rest of the line.
                    Text(talk.model.text)
                        .opacity(talk.model.waiting ? 0.6 : 1)
                        .onTapGesture { input.modelAsked?() }
                }
                Text(" · " + talk.environment)
            }
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.muted)
            .padding(.leading, 14)
            .padding(.top, 6)
            .padding(.bottom, 14)
        }
        .onAppear {
            input.show(session: talk.session)
            input.watchPaste()
            focused = true
        }
        .onDisappear { input.stopWatchingPaste() }
        .onChange(of: talk.session) { _, next in
            input.show(session: next)
            focused = true
        }
        .onChange(of: focused) { _, now in input.focused = now }
    }

    /// Return sends, Shift+Return is a new line, Esc stops the work, Shift+Tab changes the mode,
    /// the arrows walk through what was sent (D-294). While a card of permission or of a plan
    /// waits, Return in an empty field allows it and Esc denies it (D-323).
    private func onKey(_ press: KeyPress) -> KeyPress.Result {
        switch press.key {
        case .return where press.modifiers.contains(.shift):
            input.text += "\n"
            return .handled
        case .return:
            return act(.enter)
        case .escape:
            return act(.escape)
        case .tab where press.modifiers.contains(.shift):
            input.modeAsked?()
            return .handled
        case .upArrow:
            return input.walk(-1) ? .handled : .ignored
        case .downArrow:
            return input.walk(+1) ? .handled : .ignored
        default:
            return .ignored
        }
    }

    /// Voice input is not on the Mac yet (D-312), so a recording never takes a key here.
    private func act(_ key: TalkKey) -> KeyPress.Result {
        let card = TalkKeys.card(talk.items)
        let empty = input.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && input.attached.isEmpty
        switch TalkKeys.action(key, TalkKeyState(cardWaits: card != nil, empty: empty, busy: talk.busy)) {
        case .send:
            input.send()
        case .allow:
            if let card { input.chosen?(card.requestId, .allow) }
        case .deny:
            if let card { input.chosen?(card.requestId, .deny("")) }
        case .stop:
            input.stopAsked?()
        case .swallow, .cancelVoice:
            break
        case .none:
            return .ignored
        }
        return .handled
    }

    private func drop(_ providers: [NSItemProvider]) -> Bool {
        let found = providers.filter { $0.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier) }
        guard !found.isEmpty else { return false }

        for provider in found {
            _ = provider.loadObject(ofClass: URL.self) { url, _ in
                guard let url else { return }
                DispatchQueue.main.async {
                    MainActor.assumeIsolated { input.take([url]) }
                }
            }
        }
        return true
    }
}
