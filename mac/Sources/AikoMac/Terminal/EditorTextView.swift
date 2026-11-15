import AppKit

/// The field of the editor. It shows its keys to the editor before it takes them, as the preview
/// events of WPF do, and lets the editor have pasted and dropped pictures and files.
final class EditorTextView: NSTextView {
    weak var owner: EditorView?

    /// The grey words while the field is empty.
    var placeholder = ""

    override func keyDown(with event: NSEvent) {
        // While a word is still being composed (Japanese, Chinese), its keys belong to the input method.
        if !hasMarkedText(), owner?.press(event.keyCode, event.modifierFlags.intersection(.deviceIndependentFlagsMask)) == true {
            return
        }
        super.keyDown(with: event)
    }

    override func insertText(_ string: Any, replacementRange: NSRange) {
        if let typed = string as? String, typed.count == 1, let character = typed.first,
           owner?.typed(character) == true {
            return
        }
        super.insertText(string, replacementRange: replacementRange)
    }

    /// Text the editor puts in itself, as if typed, without asking the editor again.
    func insertPlain(_ value: String) {
        super.insertText(value, replacementRange: selectedRange())
    }

    override func paste(_ sender: Any?) {
        if owner?.pasteFromPasteboard(.general) == true {
            return
        }
        pasteAsPlainText(sender)
    }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        TerminalPaste.files(on: sender.draggingPasteboard).isEmpty ? super.draggingEntered(sender) : .copy
    }

    override func draggingUpdated(_ sender: NSDraggingInfo) -> NSDragOperation {
        TerminalPaste.files(on: sender.draggingPasteboard).isEmpty ? super.draggingUpdated(sender) : .copy
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        let files = TerminalPaste.files(on: sender.draggingPasteboard)
        guard !files.isEmpty, let owner else { return super.performDragOperation(sender) }
        owner.dropFiles(files)
        return true
    }

    override func becomeFirstResponder() -> Bool {
        let took = super.becomeFirstResponder()
        if took { owner?.focusChanged(true) }
        return took
    }

    override func resignFirstResponder() -> Bool {
        let gave = super.resignFirstResponder()
        if gave { owner?.focusChanged(false) }
        return gave
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        guard string.isEmpty, !placeholder.isEmpty, let font else { return }
        let left = textContainerInset.width + (textContainer?.lineFragmentPadding ?? 0)
        (placeholder as NSString).draw(
            at: NSPoint(x: left, y: textContainerInset.height),
            withAttributes: [.font: font, .foregroundColor: Theme.nsMuted])
    }
}

/// Labels in a row that goes on to the next line when it is full, as a WrapPanel does.
final class FlowRow: NSView {
    var gap: CGFloat = 6 {
        didSet { invalidateIntrinsicContentSize() }
    }

    override var isFlipped: Bool { true }

    override var intrinsicContentSize: NSSize {
        NSSize(width: NSView.noIntrinsicMetric, height: arrange(place: false))
    }

    override func layout() {
        super.layout()
        let height = arrange(place: true)
        if abs(height - frame.height) > 0.5 {
            invalidateIntrinsicContentSize()
        }
    }

    /// Places the labels from the left and answers the height they take.
    private func arrange(place: Bool) -> CGFloat {
        let width = bounds.width > 0 ? bounds.width : .greatestFiniteMagnitude
        var x: CGFloat = 0
        var y: CGFloat = 0
        var lineHeight: CGFloat = 0
        for view in subviews {
            let size = view.fittingSize
            if x > 0, x + size.width > width {
                x = 0
                y += lineHeight
                lineHeight = 0
            }
            if place {
                view.frame = NSRect(x: x, y: y, width: size.width, height: size.height)
            }
            x += size.width + gap
            lineHeight = max(lineHeight, size.height)
        }
        return subviews.isEmpty ? 0 : y + lineHeight
    }
}

/// "picture 1" with a small square and a cross, the twin of a label of DrawPictures on Windows.
final class PictureChip: NSView {
    private let removed: () -> Void

    init(title: String, path: String, zoom: CGFloat, removed: @escaping () -> Void) {
        self.removed = removed
        super.init(frame: .zero)
        wantsLayer = true
        layer?.backgroundColor = Theme.nsRaised.cgColor
        layer?.cornerRadius = 6 * zoom
        toolTip = path

        let square = NSView()
        square.wantsLayer = true
        square.layer?.backgroundColor = Theme.nsMuted.cgColor
        square.layer?.cornerRadius = 2 * zoom

        let label = NSTextField(labelWithString: title)
        label.font = Theme.monoFont(Theme.textTiny * zoom)
        label.textColor = Theme.nsInk

        let cross = CrossButton(size: 6 * zoom) { [weak self] in self?.removed() }

        let row = NSStackView(views: [square, label, cross])
        row.orientation = .horizontal
        row.alignment = .centerY
        row.spacing = 6 * zoom
        row.setCustomSpacing(8 * zoom, after: label)
        row.edgeInsets = NSEdgeInsets(top: 3 * zoom, left: 8 * zoom, bottom: 3 * zoom, right: 8 * zoom)
        row.translatesAutoresizingMaskIntoConstraints = false
        addSubview(row)
        NSLayoutConstraint.activate([
            square.widthAnchor.constraint(equalToConstant: 8 * zoom),
            square.heightAnchor.constraint(equalToConstant: 8 * zoom),
            row.leadingAnchor.constraint(equalTo: leadingAnchor),
            row.trailingAnchor.constraint(equalTo: trailingAnchor),
            row.topAnchor.constraint(equalTo: topAnchor),
            row.bottomAnchor.constraint(equalTo: bottomAnchor),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }
}

/// A thin cross that removes what it stands beside.
final class CrossButton: NSView {
    private let size: CGFloat
    private let pressed: () -> Void

    init(size: CGFloat, pressed: @escaping () -> Void) {
        self.size = size
        self.pressed = pressed
        super.init(frame: .zero)
        translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            widthAnchor.constraint(equalToConstant: size),
            heightAnchor.constraint(equalToConstant: size),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    override func draw(_ dirtyRect: NSRect) {
        let inset: CGFloat = 0.5
        let path = NSBezierPath()
        path.move(to: NSPoint(x: inset, y: inset))
        path.line(to: NSPoint(x: size - inset, y: size - inset))
        path.move(to: NSPoint(x: size - inset, y: inset))
        path.line(to: NSPoint(x: inset, y: size - inset))
        path.lineWidth = BarMarks.lineWidth
        path.lineCapStyle = .round
        Theme.nsMuted.setStroke()
        path.stroke()
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .pointingHand)
    }

    override func mouseDown(with event: NSEvent) {}

    override func mouseUp(with event: NSEvent) {
        if bounds.contains(convert(event.locationInWindow, from: nil)) {
            pressed()
        }
    }
}
