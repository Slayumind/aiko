import AikoKit
import AppKit

/// What the editor needs to know of the open session.
struct EditorSession: Equatable {
    let id: String
    let kind: SessionKind
    let folder: String
    let picturesFolder: String
}

/// The editor under the terminal (D-354, D-362, D-369), the twin of TerminalEditor.cs: the text is
/// typed and fixed here, with the mouse and Cmd+Z, and goes to the program as one message. An empty
/// editor lets the keys that answer a program through; what a key does is the core's (EditorKeys).
/// Pictures come from the pasteboard or a drop and wait above the text.
///
/// One editor serves every session: the text, the pictures and the history of each are kept, so a
/// switch to another session and back finds them as they were.
///
/// It is AppKit, not SwiftUI: the keys must be seen before the text view takes them, and the list of
/// "@" stands at the caret. So it takes the zoom of the window itself (D-384), in its fonts and room.
@MainActor
final class EditorView: NSView, NSTextViewDelegate {
    /// The field grows with its text up to this height, then scrolls.
    static let mostHeight: CGFloat = 180

    final class Draft {
        var text = ""
        var caret = 0
        var pictures: [String] = []
        var history = CommandHistory.empty
        var passing = false
    }

    var drafts: [String: Draft] = [:]
    var draft = Draft()
    var session: EditorSession?

    /// A session of Claude Code: the editor opened with Cmd+G (D-370).
    var composing = false

    /// What the list over the editor offers: files of "@" or paths of Tab.
    var mention: MentionQuery?
    var offered: [String] = []
    var offersPaths = false

    /// A command Claude Code proposed for the words in a command line (D-358), and the words it
    /// answers; nil while there is none. The timer waits for a pause in the typing and stops.
    var suggestion: String?
    var suggestFor: String?
    private var suggestPause: Timer?

    /// Words in a command line that ask for a command, after a pause in the typing.
    var suggestionAsked: ((String) -> Void)?

    /// The check door runs in a window that may not have the keyboard.
    var checkWithoutFocus = false

    // Voice input (EditorView+Voice.swift): a recording shows its words in the field, read only.
    var dictating = false
    var strip = false
    var before = ""
    var after = ""
    /// The field as it was when the recording began: Esc puts it back exactly.
    var kept = (text: "", caret: 0)
    var voiceActive = false
    var shownNotice = VoiceNotice.none
    var voicePressed: (() -> Void)?
    var voiceCancelled: (() -> Void)?
    var voiceSettingsAsked: (() -> Void)?
    let mic = MicButton()
    let wave = WaveView()
    let voiceLine = NSStackView()
    let voiceWord = NSTextField(labelWithString: "")
    let voiceWhere = NSTextField(labelWithString: "")
    let voiceCancel = NSTextField(labelWithString: "")
    let noticeBar = NoticeBar()
    private var micSize: NSLayoutConstraint?
    private var micGap: NSLayoutConstraint?
    private var micTop: NSLayoutConstraint?

    let text = EditorTextView(usingTextLayoutManager: false)
    let scroll = NSScrollView()
    let choices = ChoiceList()
    let pictures = FlowRow()
    private let top = NSView()
    private let sign = NSTextField(labelWithString: "›")
    private let field = NSView()
    let hint = NSTextField(wrappingLabelWithString: "")
    private let stack = NSStackView()
    /// Each line of the stack is as wide as the editor without its sides.
    private var lineWidths: [NSLayoutConstraint] = []
    private var textHeight: NSLayoutConstraint?
    private var signGap: NSLayoutConstraint?

    // ---- what the host listens to ----

    /// Keys and text for the program as they are.
    var passed: ((String) -> Void)?

    /// A message: the writes in their order (EditorSend).
    var sent: (([String]) -> Void)?

    /// Text and pictures for the line of Claude Code, without Enter (D-370).
    var inserted: (([String]) -> Void)?

    /// The editor came on screen or left it: a command line always has it, a session of Claude Code
    /// only while it is opened with Cmd+G.
    var wantedChanged: (() -> Void)?

    var wanted: Bool { session?.kind == .shell || composing || strip }

    var zoom: CGFloat = 1 {
        didSet {
            if zoom != oldValue { applyZoom() }
        }
    }

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer?.backgroundColor = Theme.nsSurface.cgColor
        layer?.masksToBounds = true

        top.wantsLayer = true
        top.layer?.backgroundColor = Theme.nsHairline.cgColor

        text.owner = self
        text.delegate = self
        text.isRichText = false
        text.importsGraphics = false
        text.allowsUndo = true
        text.drawsBackground = false
        text.textColor = Theme.nsInk
        text.insertionPointColor = Theme.nsInk
        text.textContainerInset = .zero
        // The text box of Windows keeps 2 units before its text; the hint stands where the text will.
        text.textContainer?.lineFragmentPadding = 2
        text.textContainer?.widthTracksTextView = true
        text.isVerticallyResizable = true
        text.isHorizontallyResizable = false
        text.autoresizingMask = [.width]
        // Commands and paths are typed here: nothing may change the letters on its own.
        text.isAutomaticQuoteSubstitutionEnabled = false
        text.isAutomaticDashSubstitutionEnabled = false
        text.isAutomaticTextReplacementEnabled = false
        text.isAutomaticSpellingCorrectionEnabled = false
        text.isContinuousSpellCheckingEnabled = false
        text.isAutomaticLinkDetectionEnabled = false
        text.placeholder = Strings.terminalEditorPlaceholder

        scroll.documentView = text
        scroll.drawsBackground = false
        scroll.hasVerticalScroller = true
        scroll.autohidesScrollers = true
        scroll.scrollerStyle = .overlay
        scroll.borderType = .noBorder

        sign.textColor = Theme.nsMuted
        hint.isSelectable = false
        hint.maximumNumberOfLines = 0
        hint.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        pictures.isHidden = true

        for view in [sign, scroll, mic] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            field.addSubview(view)
        }
        let textHeight = scroll.heightAnchor.constraint(equalToConstant: 16)
        let signGap = scroll.leadingAnchor.constraint(equalTo: sign.trailingAnchor, constant: 10)
        let micSize = mic.widthAnchor.constraint(equalToConstant: 24)
        let micGap = mic.leadingAnchor.constraint(equalTo: scroll.trailingAnchor, constant: 10)
        let micTop = mic.topAnchor.constraint(equalTo: field.topAnchor, constant: -4)
        self.textHeight = textHeight
        self.signGap = signGap
        self.micSize = micSize
        self.micGap = micGap
        self.micTop = micTop
        sign.setContentHuggingPriority(.required, for: .horizontal)
        mic.isHidden = true
        mic.pressed = { [weak self] in self?.voicePressed?() }
        NSLayoutConstraint.activate([
            sign.leadingAnchor.constraint(equalTo: field.leadingAnchor),
            sign.topAnchor.constraint(equalTo: field.topAnchor),
            signGap,
            micGap,
            mic.trailingAnchor.constraint(equalTo: field.trailingAnchor),
            micSize,
            mic.heightAnchor.constraint(equalTo: mic.widthAnchor),
            micTop,
            scroll.topAnchor.constraint(equalTo: field.topAnchor),
            scroll.bottomAnchor.constraint(equalTo: field.bottomAnchor),
            textHeight,
        ])

        for label in [voiceWord, voiceWhere, voiceCancel] {
            label.textColor = Theme.nsMuted
        }
        voiceLine.orientation = .horizontal
        voiceLine.alignment = .centerY
        voiceLine.setViews([wave, voiceWord, voiceWhere, voiceCancel], in: .leading)
        voiceLine.isHidden = true
        noticeBar.isHidden = true
        noticeBar.clicked = { [weak self] in self?.noticeLinkClicked() }

        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 0
        // The editor is as tall as what it holds; the terminal takes the rest.
        stack.setHuggingPriority(NSLayoutConstraint.Priority(999), for: .vertical)
        pictures.setContentHuggingPriority(NSLayoutConstraint.Priority(999), for: .vertical)
        hint.setContentHuggingPriority(NSLayoutConstraint.Priority(999), for: .vertical)
        stack.setViews([noticeBar, pictures, field, hint, voiceLine], in: .top)
        lineWidths = [noticeBar, pictures, field, hint].map { $0.widthAnchor.constraint(equalTo: stack.widthAnchor) }
        NSLayoutConstraint.activate(lineWidths)

        // Below required: the host folds the editor to no height while it is away.
        let stackBottom = stack.bottomAnchor.constraint(equalTo: bottomAnchor)
        stackBottom.priority = NSLayoutConstraint.Priority(999)
        for view in [top, stack] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            addSubview(view)
        }
        NSLayoutConstraint.activate([
            top.leadingAnchor.constraint(equalTo: leadingAnchor),
            top.trailingAnchor.constraint(equalTo: trailingAnchor),
            top.topAnchor.constraint(equalTo: topAnchor),
            top.heightAnchor.constraint(equalToConstant: 1),
            stack.leadingAnchor.constraint(equalTo: leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: trailingAnchor),
            stack.topAnchor.constraint(equalTo: top.bottomAnchor),
            stackBottom,
        ])

        choices.clicked = { [weak self] index in self?.choose(index) }
        applyZoom()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    /// A click anywhere on the editor puts the caret in its field.
    override func mouseDown(with event: NSEvent) {
        focusText()
    }

    override func layout() {
        // A wrapping label needs its width to know its height: the width of the editor without its
        // sides, known before the stack is laid out.
        let width = max(0, bounds.width - stack.edgeInsets.left - stack.edgeInsets.right)
        if abs(hint.preferredMaxLayoutWidth - width) > 0.5 {
            hint.preferredMaxLayoutWidth = width
        }
        refit()
        super.layout()
    }

    func focusText() {
        if window?.firstResponder !== text {
            window?.makeFirstResponder(text)
        }
    }

    var hasKeys: Bool { window?.firstResponder === text }

    /// The text view took the keys or gave them away: the line over the editor shows it.
    func focusChanged(_ focused: Bool) {
        top.layer?.backgroundColor = (focused ? Theme.nsInputLine : Theme.nsHairline).cgColor
        if !focused {
            choices.close()
        }
    }

    // ---- sessions ----

    /// The session the editor writes for. Its text and pictures come back as they were left.
    func show(_ next: EditorSession) {
        if session?.id == next.id {
            session = next
            return
        }

        if dictating {
            voiceCancelled?()
        }

        keep()
        let wasWanted = wanted
        session = next
        composing = false
        if let kept = drafts[next.id] {
            draft = kept
        } else {
            draft = Draft()
            drafts[next.id] = draft
        }
        text.string = draft.text
        text.setSelectedRange(NSRange(location: min(draft.caret, (text.string as NSString).length), length: 0))
        choices.close()
        drawPictures()
        textChanged()
        if wanted != wasWanted {
            wantedChanged?()
        }
    }

    /// A session was closed: what was typed for it goes.
    func forget(_ id: String) {
        drafts[id] = nil
    }

    /// Cmd+G in a session of Claude Code opens the editor for a long text, and closes it again.
    func compose() {
        guard session?.kind == .claude, !dictating else { return }
        composing.toggle()
        showHint()
        wantedChanged?()
        if composing {
            focusText()
        }
    }

    func hide() {
        composing = false
        choices.close()
        showHint()
        wantedChanged?()
    }

    /// The mic stands right of the field while voice input is on; away, it takes no room.
    func placeMic() {
        micSize?.constant = mic.isHidden ? 0 : 24 * zoom
        micGap?.constant = mic.isHidden ? 0 : 10 * zoom
        micTop?.constant = -4 * zoom
    }

    private func keep() {
        draft.text = text.string
        draft.caret = text.selectedRange().location
    }

    // ---- the text ----

    func textDidChange(_ notification: Notification) {
        textChanged()
    }

    func textViewDidChangeSelection(_ notification: Notification) {
        offer()
    }

    /// The text was typed or set: the draft, the hint, the list and the height follow it.
    func textChanged() {
        draft.text = text.string
        if !text.string.isEmpty {
            draft.passing = false
        }
        offersPaths = false
        text.needsDisplay = true
        waitForSuggestion()
        showHint()
        offer()
        refit()
    }

    /// Text set by the editor itself, with the caret after it.
    func setText(_ value: String, caret: Int) {
        text.string = value
        text.setSelectedRange(NSRange(location: min(max(caret, 0), (value as NSString).length), length: 0))
        textChanged()
    }

    /// The field is as tall as its text, up to mostHeight.
    func refit() {
        guard let layout = text.layoutManager, let container = text.textContainer, let font = text.font else { return }
        layout.ensureLayout(for: container)
        let line = layout.defaultLineHeight(for: font)
        let used = max(layout.usedRect(for: container).height, line)
        let height = min(used.rounded(.up), Self.mostHeight * zoom)
        if textHeight?.constant != height {
            textHeight?.constant = height
        }
    }

    // ---- the command Claude Code proposes (D-358) ----

    /// The command Claude Code proposed for these words; nil when it gave none. It shows only while
    /// the words in the field are still the same.
    func showSuggestion(_ words: String, _ command: String?) {
        guard text.string == words else { return }
        suggestFor = nil
        suggestion = command
        showHint()
    }

    /// New words in a command line drop the old proposal; words that ask for a command wait for a
    /// pause in the typing before the question goes out.
    private func waitForSuggestion() {
        suggestion = nil
        suggestFor = nil
        suggestPause?.invalidate()
        suggestPause = nil
        guard session?.kind == .shell, ShellAsk.isRequest(text.string) else { return }
        suggestPause = Timer.scheduledTimer(withTimeInterval: 0.7, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated { self?.askForSuggestion() }
        }
    }

    private func askForSuggestion() {
        suggestPause = nil
        guard session?.kind == .shell, ShellAsk.isRequest(text.string) else { return }
        suggestFor = text.string
        showHint()
        suggestionAsked?(text.string)
    }

    // ---- the line under the field ----

    func showHint() {
        let state = state()
        let shell = state.kind == .shell
        let words = shell && suggestion != nil ? Strings.format(Strings.shellSuggestion, (suggestion ?? "").replacingOccurrences(of: "**", with: "* *"))
            : shell && suggestFor != nil && suggestFor == text.string ? Strings.shellSuggesting
            : state.composing ? Strings.terminalEditorHintCompose
            : state.passing && state.empty ? Strings.terminalEditorHintPassing
            : state.empty ? (shell ? Strings.terminalEditorHintShellEmpty : Strings.terminalEditorHintEmpty)
            : shell ? Strings.terminalEditorHintShellText
            : Strings.terminalEditorHintText

        // **…** marks a key: it is written white, the rest grey.
        let font = Theme.monoFont(Theme.textTiny * zoom)
        let shown = NSMutableAttributedString()
        for (index, part) in words.components(separatedBy: "**").enumerated() {
            shown.append(NSAttributedString(string: part, attributes: [
                .font: font,
                .foregroundColor: index % 2 == 1 ? Theme.nsInk : Theme.nsMuted,
            ]))
        }
        hint.attributedStringValue = shown
    }

    /// The heights of the parts, for --try-editor.
    var layoutForCheck: String {
        let parts: [(String, NSView)] = [("editor", self), ("stack", stack), ("pictures", pictures), ("field", field),
                                         ("hint", hint), ("terminal", superview?.subviews.first { $0 is AikoTerminalView } ?? self)]
        return parts.map { "\($0.0) \(Int($0.1.frame.height))" }.joined(separator: ", ")
            + (hasAmbiguousLayout ? ", ambiguous" : "")
    }

    // ---- the zoom ----

    private func applyZoom() {
        let font = Theme.monoFont(Theme.textNumber * zoom)
        text.font = font
        text.typingAttributes = [.font: font, .foregroundColor: Theme.nsInk]
        sign.font = font
        signGap?.constant = 10 * zoom
        stack.edgeInsets = NSEdgeInsets(top: 10 * zoom, left: 14 * zoom, bottom: 8 * zoom, right: 14 * zoom)
        lineWidths.forEach { $0.constant = -28 * zoom }
        stack.setCustomSpacing(6 * zoom, after: pictures)
        stack.setCustomSpacing(6 * zoom, after: field)
        pictures.gap = 6 * zoom
        choices.zoom = zoom
        let tiny = Theme.monoFont(Theme.textTiny * zoom)
        for label in [voiceWord, voiceWhere, voiceCancel] {
            label.font = tiny
        }
        wave.zoom = zoom
        voiceLine.spacing = 10 * zoom
        voiceLine.setCustomSpacing(8 * zoom, after: wave)
        stack.setCustomSpacing(8 * zoom, after: noticeBar)
        placeMic()
        showHint()
        drawPictures()
        needsLayout = true
    }
}
