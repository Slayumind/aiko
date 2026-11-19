import AikoKit
import AppKit

/// The bar in place of the editor when Claude Code ended by itself (D-351): the conversation is
/// saved, and Enter or the button starts it again with --resume. The twin of the stopped bar of
/// TerminalPanel.cs. It takes the keys while it is shown: the terminal of a program that ended
/// takes none.
@MainActor
final class StoppedBar: NSView {
    private let line = NSTextField(wrappingLabelWithString: "")
    private let button = MainButton()
    private var insets: [NSLayoutConstraint] = []

    /// Continue: Enter or a click.
    var resume: (() -> Void)?

    var zoom: CGFloat = 1 {
        didSet { applyZoom() }
    }

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer?.backgroundColor = Theme.nsBackground.cgColor
        layer?.masksToBounds = true
        line.textColor = Theme.nsMuted
        line.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        button.pressed = { [weak self] in self?.resume?() }

        for view in [line, button] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            addSubview(view)
        }
        let left = line.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 16)
        let gap = button.leadingAnchor.constraint(greaterThanOrEqualTo: line.trailingAnchor, constant: 12)
        let right = button.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -16)
        let top = line.topAnchor.constraint(greaterThanOrEqualTo: topAnchor, constant: 10)
        let bottom = line.bottomAnchor.constraint(lessThanOrEqualTo: bottomAnchor, constant: -12)
        let buttonTop = button.topAnchor.constraint(greaterThanOrEqualTo: topAnchor, constant: 10)
        let buttonBottom = button.bottomAnchor.constraint(lessThanOrEqualTo: bottomAnchor, constant: -12)
        // The bar is as tall as what it holds, and both stand in its middle.
        let snug = heightAnchor.constraint(equalToConstant: 0)
        snug.priority = .defaultLow
        insets = [left, gap, right, top, bottom, buttonTop, buttonBottom]
        // Below required: the host folds the bar to no height while it is away, and a required
        // room inside it would fight that fold on every pass of the layout.
        for vertical in [top, bottom, buttonTop, buttonBottom] {
            vertical.priority = NSLayoutConstraint.Priority(999)
        }
        NSLayoutConstraint.activate(insets + [
            snug,
            line.centerYAnchor.constraint(equalTo: centerYAnchor),
            button.centerYAnchor.constraint(equalTo: centerYAnchor),
        ])
        applyZoom()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    /// A wrapping label needs its width to know its height: the width of the bar without its sides
    /// and the button, known before the layout. The frame of the label itself changes with the
    /// height it is given, and following it would lay the window out again and again.
    override func layout() {
        let width = max(0, bounds.width - (16 + 12 + 16) * zoom - button.intrinsicContentSize.width)
        if abs(line.preferredMaxLayoutWidth - width) > 0.5 {
            line.preferredMaxLayoutWidth = width
        }
        super.layout()
    }

    /// Enter presses the button.
    func takeKeys() {
        window?.makeFirstResponder(button)
    }

    var hasKeys: Bool { window?.firstResponder === button }

    private func applyZoom() {
        line.font = Theme.monoFont(Theme.textBody * zoom)
        line.stringValue = Strings.terminalStoppedLine
        button.show(Strings.terminalResume, zoom: zoom)
        insets[0].constant = 16 * zoom
        insets[1].constant = 12 * zoom
        insets[2].constant = -16 * zoom
        insets[3].constant = 10 * zoom
        insets[4].constant = -12 * zoom
        insets[5].constant = 10 * zoom
        insets[6].constant = -12 * zoom
    }
}

/// The main button of the window drawn by AppKit, the twin of WindowButton(look: .main): an outline
/// in Muted with medium words, 32 high, 12 on each side, and the light layer under the mouse. It
/// takes the keys, and Enter or Space presses it.
final class MainButton: NSView {
    private var title = NSAttributedString()
    private var zoom: CGFloat = 1
    private var hovered = false {
        didSet { if hovered != oldValue { needsDisplay = true } }
    }
    var pressed: (() -> Void)?

    override var acceptsFirstResponder: Bool { true }
    override var isFlipped: Bool { true }

    func show(_ text: String, zoom: CGFloat) {
        self.zoom = zoom
        title = NSAttributedString(string: text, attributes: [
            .font: Theme.monoFont(Theme.textBody * zoom, weight: .medium),
            .foregroundColor: Theme.nsInk,
        ])
        invalidateIntrinsicContentSize()
        needsDisplay = true
    }

    override var intrinsicContentSize: NSSize {
        NSSize(width: ceil(title.size().width) + 24 * zoom, height: 32 * zoom)
    }

    override func draw(_ dirtyRect: NSRect) {
        if hovered {
            Theme.nsHoverLayer.setFill()
            bounds.fill()
        }
        let edge = NSBezierPath(rect: bounds.insetBy(dx: 0.5, dy: 0.5))
        edge.lineWidth = 1
        Theme.nsMuted.setStroke()
        edge.stroke()
        let size = title.size()
        title.draw(at: NSPoint(x: (bounds.width - size.width) / 2, y: (bounds.height - size.height) / 2))
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .activeInKeyWindow], owner: self))
    }

    override func mouseEntered(with event: NSEvent) { hovered = true }

    override func mouseExited(with event: NSEvent) { hovered = false }

    override func keyDown(with event: NSEvent) {
        if [36, 76, 49].contains(event.keyCode) {
            pressed?()
        } else {
            super.keyDown(with: event)
        }
    }

    override func mouseDown(with event: NSEvent) {}

    override func mouseUp(with event: NSEvent) {
        if bounds.contains(convert(event.locationInWindow, from: nil)) {
            pressed?()
        }
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .pointingHand)
    }
}
