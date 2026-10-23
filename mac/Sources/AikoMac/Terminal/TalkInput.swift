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

    /// The window of the terminal and what its conversation shows now: the keys the window hears
    /// in another place than the field are answered from them (D-328).
    weak var window: NSWindow?
    var screen: TalkScreen?

    /// Asks the view to give the field the keyboard.
    var focusAsked: (() -> Void)?

    private var history: [String: [String]] = [:]
    private var session = ""
    private var back = -1
    private var draft = ""
    private var keyWatch: Any?

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

    /// The keys of the window (D-328). The field editor of AppKit takes Cmd+V first, so a paste
    /// is caught here: with a picture in the pasteboard it is attached, copied files are dropped,
    /// text pastes as it always does. From another place of the terminal window, where the
    /// keyboard went after a click on the feed or the list, Shift+Tab changes the mode, Cmd+V
    /// pastes into the field, Return and Esc answer a card, and letters go into the field.
    func watchKeys() {
        guard keyWatch == nil else { return }
        keyWatch = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            let taken = MainActor.assumeIsolated { self?.windowKey(event) ?? false }
            return taken ? nil : event
        }
    }

    func stopWatchingKeys() {
        if let keyWatch {
            NSEvent.removeMonitor(keyWatch)
        }
        keyWatch = nil
    }

    private func windowKey(_ event: NSEvent) -> Bool {
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        let paste = flags == .command && event.charactersIgnoringModifiers == "v"
        if paste && focused {
            return pasteFromBoard()
        }
        guard !focused, let window, event.window === window, !othersTyping(window) else { return false }

        if paste {
            focusAsked?()
            if pasteFromBoard() { return true }
            guard let words = NSPasteboard.general.string(forType: .string) else { return false }
            text += words
            return true
        }
        switch event.keyCode {
        case Self.tabKey where flags == .shift:
            modeAsked?()
            return true
        case Self.returnKey, Self.enterKey, Self.escapeKey:
            guard flags.isEmpty, !(window.firstResponder is NSButton) else { return false }
            return act(event.keyCode == Self.escapeKey ? .escape : .enter, outside: true)
        default:
            guard flags.isDisjoint(with: [.command, .control]), let letters = event.characters,
                  Self.typed(letters) else { return false }
            focusAsked?()
            text += letters
            return true
        }
    }

    private static let tabKey: UInt16 = 48
    private static let returnKey: UInt16 = 36
    private static let enterKey: UInt16 = 76
    private static let escapeKey: UInt16 = 53

    /// Another field one types into has the keyboard: the name of a session, a search.
    private func othersTyping(_ window: NSWindow) -> Bool {
        (window.firstResponder as? NSTextView)?.isEditable == true
    }

    /// Letters and signs, not a space, a control key or the arrows and function keys of AppKit.
    private static func typed(_ letters: String) -> Bool {
        !letters.isEmpty && letters.unicodeScalars.allSatisfy { scalar in
            !CharacterSet.whitespacesAndNewlines.contains(scalar)
                && !CharacterSet.controlCharacters.contains(scalar)
                && !(0xF700...0xF8FF).contains(scalar.value)
        }
    }

    /// Return or Esc, in the field or from another place of the window. Voice input is not on the
    /// Mac yet (D-312), so a recording never takes a key here. Whether the key was taken.
    func act(_ key: TalkKey, outside: Bool = false) -> Bool {
        guard let talk = screen else { return false }
        let card = TalkKeys.card(talk.items)
        let empty = text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && attached.isEmpty
        switch TalkKeys.action(key, TalkKeyState(cardWaits: card != nil, empty: empty, busy: talk.busy, outside: outside)) {
        case .send:
            send()
        case .allow:
            if let card { chosen?(card.requestId, .allow) }
        case .deny:
            if let card { chosen?(card.requestId, .deny("")) }
        case .stop:
            stopAsked?()
        case .swallow, .cancelVoice:
            break
        case .none:
            return false
        }
        return true
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
    @State private var textHeight: CGFloat = 20

    /// The room between the text and the frame of the field, and its height before it scrolls.
    private static let side: CGFloat = 14
    private static let fieldHeight: CGFloat = 200
    private static let fieldEnd = "field-end"

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
                    .padding(.horizontal, Self.side)
                }

                // The text does not scroll itself: this does, over the whole width of the field, so
                // its bar stands at the right edge, right of the buttons (AIKO-245).
                ZStack(alignment: .topTrailing) {
                    ScrollViewReader { reader in
                        ScrollView(.vertical) {
                            VStack(spacing: 0) {
                                TextField(Strings.terminalDescribeTask, text: $input.text, axis: .vertical)
                                    .textFieldStyle(.plain)
                                    .font(Theme.sans(Theme.textName))
                                    .foregroundStyle(Theme.ink)
                                    .focused($focused)
                                    .onKeyPress(phases: .down) { press in onKey(press) }
                                    .padding(.leading, Self.side)
                                    .padding(.trailing, Self.side + (talk.busy ? 36 : 0))
                                Color.clear.frame(height: 0).id(Self.fieldEnd)
                            }
                            .onGeometryChange(for: CGFloat.self) { $0.size.height } action: { textHeight = $0 }
                        }
                        .frame(height: min(textHeight, Self.fieldHeight))
                        // Typing at the end keeps the end in view, as the field did when it scrolled
                        // itself; the caret elsewhere is kept in view by the text view.
                        .onChange(of: input.text) { old, new in
                            if new.count > old.count && new.hasPrefix(old) {
                                reader.scrollTo(Self.fieldEnd, anchor: .bottom)
                            }
                        }
                    }

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
                        .padding(.trailing, Self.side)
                    }
                }
            }
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
            input.screen = talk
            input.focusAsked = { focused = true }
            input.watchKeys()
            focused = true
        }
        .onDisappear { input.stopWatchingKeys() }
        .onChange(of: talk.session) { _, next in
            input.show(session: next)
            focused = true
        }
        .onChange(of: talk) { _, now in input.screen = now }
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

    private func act(_ key: TalkKey) -> KeyPress.Result {
        input.act(key) ? .handled : .ignored
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
