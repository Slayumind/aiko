import AppKit

/// A short list over the terminal, right above the editor: the files of "@" and the paths of Tab
/// (D-369). The twin of ChoicePopup.cs. On the Mac a view drawn after the terminal covers it, so the
/// list is a view of the window, not a window of its own. The keys stay with the editor, which moves
/// the light and picks.
@MainActor
final class ChoiceList: NSView {
    private static let mostRows = 8

    private let rows = NSStackView()
    private var lines: [ChoiceRow] = []
    private var leftConstraint: NSLayoutConstraint?
    private(set) var lit = 0

    var zoom: CGFloat = 1

    /// A choice was made with the mouse: its index.
    var clicked: ((Int) -> Void)?

    var isOpen: Bool { !isHidden && superview != nil }

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        // A floating panel, as every list over the window: the dark ground, one line, one shadow.
        layer?.backgroundColor = Theme.nsBackground.cgColor
        layer?.borderColor = Theme.nsHairline.cgColor
        layer?.borderWidth = 1
        layer?.masksToBounds = false
        shadow = NSShadow()
        layer?.shadowColor = NSColor.black.cgColor
        layer?.shadowOpacity = Float(Theme.shadowOpacity)
        layer?.shadowRadius = Theme.shadowBlur / 2
        layer?.shadowOffset = CGSize(width: 0, height: -Theme.shadowOffset)
        isHidden = true

        rows.orientation = .vertical
        rows.alignment = .leading
        rows.spacing = 0
        rows.translatesAutoresizingMaskIntoConstraints = false
        addSubview(rows)
        NSLayoutConstraint.activate([
            rows.leadingAnchor.constraint(equalTo: leadingAnchor),
            rows.trailingAnchor.constraint(equalTo: trailingAnchor),
            rows.topAnchor.constraint(equalTo: topAnchor),
            rows.bottomAnchor.constraint(equalTo: bottomAnchor),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    /// The choices, each with the part to mark: start and length, or a length of 0 for none. The list
    /// stands over the editor, its left edge this far from the left of the editor's field.
    func show(_ choices: [(String, (start: Int, length: Int))], under editor: NSView, left: CGFloat) {
        place(over: editor)
        rows.edgeInsets = NSEdgeInsets(top: 4 * zoom, left: 4 * zoom, bottom: 4 * zoom, right: 4 * zoom)
        rows.setViews([], in: .top)
        lines = choices.prefix(Self.mostRows).enumerated().map { index, choice in
            let row = ChoiceRow(choice.0, mark: choice.1, zoom: zoom)
            row.clicked = { [weak self] in self?.clicked?(index) }
            return row
        }
        for line in lines {
            rows.addView(line, in: .top)
            // Each line is as wide as the list without its sides, so its light fills the row.
            line.widthAnchor.constraint(equalTo: rows.widthAnchor, constant: -8 * zoom).isActive = true
        }
        leftConstraint?.constant = (12 * zoom) + left
        light(0)
        isHidden = lines.isEmpty
    }

    func close() {
        isHidden = true
    }

    func move(_ step: Int) {
        guard !lines.isEmpty else { return }
        light((lit + step + lines.count) % lines.count)
    }

    private func light(_ index: Int) {
        lit = index
        for (at, line) in lines.enumerated() {
            line.lit = at == index
        }
    }

    /// The list joins the window above the editor once, in front of the terminal.
    private func place(over editor: NSView) {
        guard let holder = editor.superview else { return }
        if superview !== holder {
            removeFromSuperview()
            translatesAutoresizingMaskIntoConstraints = false
            holder.addSubview(self, positioned: .above, relativeTo: nil)
            let left = leadingAnchor.constraint(equalTo: editor.leadingAnchor)
            leftConstraint = left
            let widest = widthAnchor.constraint(lessThanOrEqualToConstant: 520)
            NSLayoutConstraint.activate([
                left,
                bottomAnchor.constraint(equalTo: editor.topAnchor, constant: -4),
                widthAnchor.constraint(greaterThanOrEqualToConstant: 240),
                widest,
                trailingAnchor.constraint(lessThanOrEqualTo: holder.trailingAnchor, constant: -12),
            ])
        } else {
            holder.addSubview(self, positioned: .above, relativeTo: nil)
        }
    }
}

/// One line of the list: grey, with the typed part white, on the raised plate while it is lit.
final class ChoiceRow: NSView {
    var clicked: (() -> Void)?

    var lit = false {
        didSet { layer?.backgroundColor = lit ? Theme.nsRaised.cgColor : NSColor.clear.cgColor }
    }

    init(_ text: String, mark: (start: Int, length: Int), zoom: CGFloat) {
        super.init(frame: .zero)
        wantsLayer = true

        let font = Theme.monoFont(Theme.textBody * zoom)
        let shown = NSMutableAttributedString(string: text, attributes: [.font: font, .foregroundColor: Theme.nsMuted])
        let length = (text as NSString).length
        let start = min(max(mark.start, 0), length)
        shown.addAttribute(.foregroundColor, value: Theme.nsInk,
                           range: NSRange(location: start, length: min(max(mark.length, 0), length - start)))
        let label = NSTextField(labelWithAttributedString: shown)
        label.lineBreakMode = .byTruncatingTail
        label.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        label.translatesAutoresizingMaskIntoConstraints = false
        addSubview(label)
        NSLayoutConstraint.activate([
            label.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 8 * zoom),
            label.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -8 * zoom),
            label.topAnchor.constraint(equalTo: topAnchor, constant: 5 * zoom),
            label.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -5 * zoom),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .pointingHand)
    }

    override func mouseDown(with event: NSEvent) {}

    override func mouseUp(with event: NSEvent) {
        clicked?()
    }
}
