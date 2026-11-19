import AikoKit
import AppKit
import SwiftUI

/// The output of a command line as blocks (D-363, D-412), the twin of ShellBlocksView.cs: each
/// command with a rule on the left, grey when it passed, red when it failed, orange while it runs;
/// its command and time on the first line, its output under it, and a long output folded to its
/// first lines. Under the mouse a block offers Copy, Fold and, when it failed, Explain; the answer of
/// Claude Code stands under it. Only the rows on screen are drawn, so ten thousand lines cost what
/// thirty do. The screen is fed on the main thread, as SwiftTerm gives its output there.
@MainActor
final class ShellBlocksView: NSView {
    /// An output longer than this folds to foldShows lines and a line "… N more".
    private static let foldAbove = 40
    private static let foldShows = 20

    /// The same air on every side of the blocks: left, right and above the first one.
    private static let inset: CGFloat = 12
    private static let ruleWidth: CGFloat = 2
    private static let textGap: CGFloat = 10
    private static let blockGap: CGFloat = 10

    private static let text = rgb(0xFA, 0xFA, 0xFA)
    private static let output = rgb(0xBD, 0xBD, 0xBD)
    private static let muted = rgb(0xA1, 0xA1, 0xA1)
    private static let raised = rgb(0x4D, 0x4D, 0x4D)
    private static let destructive = rgb(0xFF, 0x64, 0x67)
    private static let caution = rgb(0xFE, 0x9A, 0x00)
    private static let ground = rgb(0x0A, 0x0A, 0x0A)
    private static let hovered = rgb(0x26, 0x26, 0x26)
    private static let buttonLit = rgb(0x3A, 0x3A, 0x3A)
    private static let onMatch = rgb(0x0A, 0x0A, 0x0A)
    private static let match = rgb(0x4A, 0x4A, 0x4A)
    private static let matchNow = caution

    /// The marks of the buttons of a block, in a box of 24 as the icons of the site: Copy is a
    /// filled shape, the others are lines.
    private static let copyIcon = BarMarks.path(SiteIcons.copy)
    private static let checkIcon = BarMarks.path(SiteIcons.check)
    private static let foldIcon = BarMarks.path("M2.5,17 L12,7.5 L21.5,17")
    private static let unfoldIcon = BarMarks.path("M2.5,7 L12,16.5 L21.5,7")
    private static let explainIcon = BarMarks.path("M6.5,8 A5.5,5.5 0 1 1 15.1,12.5 C13.1,13.9 12,15 12,17 M12,21 L12,21.6")

    /// The sixteen colours of the terminal, as TerminalColors.Ansi on Windows.
    private static let ansi: [UInt32] = [
        0x0A0A0A, 0xFF6467, 0x00BC7D, 0xFE9A00, 0x11A9F6, 0xD477CD, 0x00D3F2, 0xA1A1A1,
        0x737373, 0xFF8270, 0xAFE773, 0xFE9A00, 0x11A9F6, 0xF7BCF0, 0x24DCC3, 0xFAFAFA,
    ]

    let screen: ShellScreen
    private var unfolded = Set<Int64>()
    private var folded = Set<Int64>()
    private var explanations: [Int64: String?] = [:]
    private var asking = Set<Int64>()
    private var buttons: [(area: NSRect, act: () -> Void)] = []
    private var rows: [Row] = []
    private var matches: [(line: Int, start: Int, length: Int)] = []
    private var matchAt = -1
    private var finding = ""
    private var offset: CGFloat = 0
    private var followsEnd = true
    private var hover: Int64?
    private var pointer = NSPoint.zero
    private var pointerOnButton = false
    private var copied: Int64?
    private var room: CGFloat = 0

    private var font = Theme.monoFont(Theme.textBody)
    private var bold = Theme.monoFont(Theme.textBody)
    private var zoom: CGFloat = 1
    private var charWidth: CGFloat = 7.8

    /// A failed command asks for an explanation: the block and its output as text.
    var explainAsked: ((ShellBlock, String) -> Void)?

    /// The folder of the session, for a file named in the output without its folder.
    var folder = ""

    init(screen: ShellScreen) {
        self.screen = screen
        super.init(frame: .zero)
        wantsLayer = true
        layer?.masksToBounds = true
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    override var isFlipped: Bool { true }

    /// What the search found now: "3 of 12", "Nothing found", or empty without a search.
    var findStatus: String {
        finding.isEmpty ? ""
            : matches.isEmpty ? Strings.shellFindNone
            : Strings.format(Strings.shellFindCount, matchAt + 1, matches.count)
    }

    /// The font of the terminal and the zoom of the window for the buttons.
    func useFont(_ font: NSFont, zoom: CGFloat) {
        self.font = font
        bold = NSFontManager.shared.convert(font, toHaveTrait: .boldFontMask)
        self.zoom = zoom
        charWidth = ("0" as NSString).size(withAttributes: [.font: font]).width
        refresh()
    }

    private var lineHeight: CGFloat { ceil(font.pointSize * 1.35) }
    private var textLeft: CGFloat { Self.inset + Self.ruleWidth + Self.textGap }

    /// Where the first row starts: the inset, and the height of the search while it is open, so the
    /// search covers no block.
    private var top: CGFloat { Self.inset + room }

    /// Room above the blocks for the search; zero when it is closed.
    func makeRoom(_ height: CGFloat) {
        room = height
        refresh()
    }

    /// New output: the rows are counted again and drawn.
    func refresh() {
        refreshesForCheck += 1
        rows = lay(ShellBlocks.of(screen))
        if followsEnd {
            offset = maxOffset()
        }
        offset = min(max(offset, 0), maxOffset())
        needsDisplay = true
    }

    /// The answer of Claude Code for a block, or nil when none came.
    func showExplanation(_ block: Int64, _ answer: String?) {
        asking.remove(block)
        explanations[block] = .some(answer)
        refresh()
    }

    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        refresh()
    }

    // ---- search ----

    /// Every place of the text in the output of the blocks, without case; the last one is shown.
    func find(_ text: String) {
        finding = text
        matches = []
        matchAt = -1
        if !text.isEmpty {
            for block in ShellBlocks.of(screen) {
                for line in block.firstLine..<max(block.firstLine, block.endLine) {
                    let words = screen.lineText(line) as NSString
                    var from = 0
                    while from < words.length {
                        let found = words.range(of: text, options: .caseInsensitive, range: NSRange(location: from, length: words.length - from))
                        guard found.location != NSNotFound else { break }
                        matches.append((line, found.location, found.length))
                        from = found.location + max(1, found.length)
                    }
                }
            }
            matchAt = matches.isEmpty ? -1 : matches.count - 1
        }
        reveal()
    }

    /// The next place (step 1) or the one before (step -1), going round.
    func findNext(_ step: Int) {
        guard !matches.isEmpty else { return }
        matchAt = (matchAt + step + matches.count) % matches.count
        reveal()
    }

    /// Opens the block of the place shown now and scrolls to it.
    private func reveal() {
        guard matchAt >= 0 else {
            refresh()
            return
        }
        let line = matches[matchAt].line
        if let owner = ShellBlocks.of(screen).first(where: { line >= $0.firstLine && line < $0.endLine }) {
            folded.remove(owner.startedAt)
            unfolded.insert(owner.startedAt)
        }
        followsEnd = false
        refresh()
        if let index = rows.firstIndex(where: { $0.kind == .output && $0.line == line }) {
            offset = min(max(top + CGFloat(index) * lineHeight - bounds.height / 3, 0), maxOffset())
            followsEnd = offset >= maxOffset() - 1
        }
        needsDisplay = true
    }

    // ---- the mouse ----

    override func scrollWheel(with event: NSEvent) {
        let step = event.hasPreciseScrollingDeltas ? event.scrollingDeltaY : event.scrollingDeltaY * 3 * lineHeight
        offset = min(max(offset - step, 0), maxOffset())
        followsEnd = offset >= maxOffset() - 1
        needsDisplay = true
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: .zero, options: [.mouseMoved, .mouseEnteredAndExited, .activeInKeyWindow, .inVisibleRect], owner: self))
    }

    override func mouseMoved(with event: NSEvent) {
        let at = convert(event.locationInWindow, from: nil)
        let row = rowAt(at.y)
        let now: Int64? = row.flatMap { $0.kind == .gap ? nil : $0.block.startedAt }
        if now != hover {
            hover = now
            needsDisplay = true
        }
        let overButton = buttons.contains { $0.area.contains(at) }
        // The plate under a button follows the pointer, so the view is drawn again while it moves
        // over the buttons.
        if pointer != at && (overButton || pointerOnButton) {
            needsDisplay = true
        }
        pointer = at
        pointerOnButton = overButton
        let overLink = event.modifierFlags.contains(.command) && row.map { $0.kind == .output && linkAt($0.line, at.x) != nil } == true
        (overButton || overLink ? NSCursor.pointingHand : NSCursor.arrow).set()
    }

    override func mouseExited(with event: NSEvent) {
        hover = nil
        pointerOnButton = false
        NSCursor.arrow.set()
        needsDisplay = true
    }

    override func mouseDown(with event: NSEvent) {}

    override func mouseUp(with event: NSEvent) {
        let at = convert(event.locationInWindow, from: nil)
        if let button = buttons.first(where: { $0.area.contains(at) }) {
            button.act()
            return
        }
        guard let row = rowAt(at.y) else { return }

        if row.kind == .more {
            unfolded.insert(row.block.startedAt)
            followsEnd = false
            refresh()
        } else if row.kind == .output, event.modifierFlags.contains(.command), let link = linkAt(row.line, at.x) {
            ShellLinks.open(link, folder: folder)
        }
    }

    private func rowAt(_ y: CGFloat) -> Row? {
        let index = Int(floor((y + offset - top) / lineHeight))
        return index >= 0 && index < rows.count ? rows[index] : nil
    }

    /// The word under the mouse on a line of output, when it is an address or a file.
    private func linkAt(_ line: Int, _ x: CGFloat) -> String? {
        ShellLink.at(screen.lineText(line), column: Int((x - textLeft) / charWidth))
    }

    // ---- the buttons ----

    private func outputText(_ block: ShellBlock) -> String {
        block.lineCount > 0 ? screen.text(fromRow: block.firstLine, fromColumn: 0, toRow: block.endLine - 1, toColumn: Int.max) : ""
    }

    private func copy(_ block: ShellBlock) {
        let text = outputText(block)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text.isEmpty ? block.command : text, forType: .string)
        copied = block.startedAt
        needsDisplay = true
        let key = block.startedAt
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
            guard let self, self.copied == key else { return }
            self.copied = nil
            self.needsDisplay = true
        }
    }

    private func fold(_ block: ShellBlock) {
        if folded.remove(block.startedAt) == nil {
            folded.insert(block.startedAt)
        }
        followsEnd = false
        refresh()
    }

    private func explain(_ block: ShellBlock) {
        guard !asking.contains(block.startedAt) else { return }
        asking.insert(block.startedAt)
        explanations[block.startedAt] = nil
        refresh()
        explainAsked?(block, outputText(block))
    }

    // ---- drawing ----

    override func draw(_ dirtyRect: NSRect) {
        Self.ground.setFill()
        bounds.fill()
        buttons.removeAll()
        guard !rows.isEmpty else { return }

        let height = lineHeight
        let first = max(0, Int((offset - top) / height))
        let last = min(rows.count, first + Int(bounds.height / height) + 2)
        for index in first..<max(first, last) {
            let row = rows[index]
            let y = top + CGFloat(index) * height - offset
            if row.kind == .gap { continue }

            if hover == row.block.startedAt {
                Self.hovered.setFill()
                NSRect(x: Self.inset, y: y, width: max(0, bounds.width - 2 * Self.inset), height: height).fill()
            }
            ruleOf(row.block).setFill()
            NSRect(x: Self.inset, y: y, width: Self.ruleWidth, height: height).fill()

            switch row.kind {
            case .command:
                drawCommand(row.block, y)
            case .output:
                drawMatches(row.line, y)
                drawLine(row.line, y)
                drawChosenMatch(row.line, y)
            case .more:
                draw(more(row.line), at: NSPoint(x: textLeft, y: y + 2), font: font.withSize(font.pointSize * 0.9), colour: Self.muted)
            case .note:
                draw(row.note ?? "", at: NSPoint(x: textLeft, y: y), font: Theme.monoFont(font.pointSize),
                     colour: row.line < 0 ? Self.destructive : Self.text)
            case .gap:
                break
            }
        }
    }

    private func drawCommand(_ block: ShellBlock, _ y: CGFloat) {
        let meta = block.running ? Strings.shellRunning
            : block.failed ? [Strings.format(Strings.shellExitCode, block.exitCode ?? 0), seconds(block)].filter { !$0.isEmpty }.joined(separator: " · ")
            : seconds(block)
        let metaFont = font.withSize(font.pointSize * 0.85)
        let metaSize = (meta as NSString).size(withAttributes: [.font: metaFont])
        let right = bounds.width - Self.inset - 6
        let metaX = max(textLeft, right - metaSize.width)
        draw(meta, at: NSPoint(x: metaX, y: y + (lineHeight - metaSize.height) / 2), font: metaFont,
             colour: block.running ? Self.caution : block.failed ? Self.destructive : Self.muted)

        var commandEnd = metaX - 12
        if hover == block.startedAt {
            commandEnd = drawButtons(block, right: metaX - 12, y: y) - 12
        }
        let style = NSMutableParagraphStyle()
        style.lineBreakMode = .byTruncatingTail
        (block.command as NSString).draw(
            with: NSRect(x: textLeft, y: y, width: max(1, commandEnd - textLeft), height: lineHeight),
            options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine],
            attributes: [.font: bold, .foregroundColor: Self.text, .paragraphStyle: style])
    }

    /// The buttons of a block under the mouse, laid from the right; answers where they start. Each
    /// is an icon in a box of 26 by 20, with a plate under the pointer, as on Windows.
    private func drawButtons(_ block: ShellBlock, right: CGFloat, y: CGFloat) -> CGFloat {
        let key = block.startedAt
        var list: [(icon: Path, line: Bool, act: () -> Void)] = [
            (copied == key ? Self.checkIcon : Self.copyIcon, false, { [weak self] in self?.copy(block) }),
        ]
        if block.lineCount > 0 {
            list.append((folded.contains(key) ? Self.unfoldIcon : Self.foldIcon, true, { [weak self] in self?.fold(block) }))
        }
        if block.failed {
            list.append((Self.explainIcon, true, { [weak self] in self?.explain(block) }))
        }

        let iconSize = 14 * zoom
        let width = 26 * zoom
        let height = min(lineHeight, 20 * zoom)
        var x = right
        for button in list.reversed() {
            x -= width
            let area = NSRect(x: x, y: y + (lineHeight - height) / 2, width: width, height: height)
            if pointerOnButton && area.contains(pointer) {
                Self.buttonLit.setFill()
                area.fill()
            }
            let scale = iconSize / 24
            let place = CGAffineTransform(translationX: x + (width - iconSize) / 2, y: area.minY + (height - iconSize) / 2)
                .scaledBy(x: scale, y: scale)
            let icon = NSBezierPath(cgPath: button.icon.cgPath)
            icon.transform(using: AffineTransform(m11: place.a, m12: place.b, m21: place.c, m22: place.d, tX: place.tx, tY: place.ty))
            if button.line {
                Self.muted.setStroke()
                icon.lineWidth = 1.3
                icon.stroke()
            } else {
                Self.muted.setFill()
                icon.fill()
            }
            buttons.append((area, button.act))
            x -= 2 * zoom
        }
        return x
    }

    private func drawMatches(_ line: Int, _ y: CGFloat) {
        var index = firstMatch(on: line)
        while index < matches.count, matches[index].line == line {
            let found = matches[index]
            (index == matchAt ? Self.matchNow : Self.match).setFill()
            NSRect(x: textLeft + CGFloat(found.start) * charWidth, y: y, width: CGFloat(found.length) * charWidth, height: lineHeight).fill()
            index += 1
        }
    }

    /// The chosen match stands on solid orange, so its letters are dark. It is drawn over the line
    /// that is already there, so the light letters do not show around the dark ones.
    private func drawChosenMatch(_ line: Int, _ y: CGFloat) {
        guard matchAt >= 0, matchAt < matches.count, matches[matchAt].line == line else { return }
        let found = matches[matchAt]
        let text = screen.lineText(line) as NSString
        guard found.start < text.length else { return }
        let word = text.substring(with: NSRange(location: found.start, length: min(found.length, text.length - found.start)))
        let x = textLeft + CGFloat(found.start) * charWidth
        Self.matchNow.setFill()
        NSRect(x: x, y: y, width: CGFloat(found.length) * charWidth, height: lineHeight).fill()
        draw(word, at: NSPoint(x: x, y: y), font: font, colour: Self.onMatch)
    }

    /// The matches go in the order of the lines, so the first one on a line is found by halves: a
    /// one-letter search over a long output gives many thousands, and every frame draws them.
    private func firstMatch(on line: Int) -> Int {
        var (low, high) = (0, matches.count)
        while low < high {
            let middle = (low + high) / 2
            if matches[middle].line < line { low = middle + 1 } else { high = middle }
        }
        return low
    }

    private func drawLine(_ line: Int, _ y: CGFloat) {
        var x = textLeft
        for run in screen.runs(line) {
            let attributes: [NSAttributedString.Key: Any] = [.font: run.style.bold ? bold : font, .foregroundColor: colourOf(run.style)]
            let size = (run.text as NSString).size(withAttributes: attributes)
            (run.text as NSString).draw(at: NSPoint(x: x, y: y), withAttributes: attributes)
            x += size.width
            if x > bounds.width { break }
        }
    }

    private func draw(_ text: String, at point: NSPoint, font: NSFont, colour: NSColor) {
        (text as NSString).draw(at: point, withAttributes: [.font: font, .foregroundColor: colour])
    }

    /// Every block as rows: its command, its output (folded when long or by the person), the answer
    /// of Claude Code when there is one, and a gap after it.
    private func lay(_ blocks: [ShellBlock]) -> [Row] {
        var laid: [Row] = []
        let width = Int(max(20, (bounds.width - textLeft - Self.inset) / max(1, charWidth * 0.9)))
        for block in blocks {
            let key = block.startedAt
            laid.append(Row(kind: .command, block: block, line: 0))
            if !folded.contains(key) {
                let folds = !block.running && block.lineCount > Self.foldAbove && !unfolded.contains(key)
                let shown = folds ? Self.foldShows : block.lineCount
                for line in 0..<shown {
                    laid.append(Row(kind: .output, block: block, line: block.firstLine + line))
                }
                if folds {
                    laid.append(Row(kind: .more, block: block, line: block.lineCount - Self.foldShows))
                }
            }

            if asking.contains(key) {
                laid.append(Row(kind: .note, block: block, line: 0, note: Strings.shellExplaining))
            } else if let answer = explanations[key] {
                if let answer {
                    laid += ShellAsk.wrap(answer, width: width).map { Row(kind: .note, block: block, line: 0, note: $0) }
                } else {
                    laid.append(Row(kind: .note, block: block, line: -1, note: Strings.shellExplainFailed))
                }
            }
            laid.append(Row(kind: .gap, block: block, line: 0))
        }
        return laid
    }

    private func maxOffset() -> CGFloat {
        max(0, top + CGFloat(rows.count) * lineHeight - bounds.height + Self.blockGap)
    }

    private func ruleOf(_ block: ShellBlock) -> NSColor {
        block.running ? Self.caution : block.failed ? Self.destructive : Self.raised
    }

    /// Under a minute in seconds with a tenth ("0,2 с"); longer as minutes and seconds.
    private func seconds(_ block: ShellBlock) -> String {
        guard block.timed else { return "" }
        let milliseconds = (block.endedAt ?? block.startedAt) - block.startedAt
        guard milliseconds < 60_000 else {
            return "\(milliseconds / 60_000):" + String(format: "%02lld", milliseconds / 1000 % 60)
        }
        let number = Strings.language == .russian ? Self.russianSeconds : Self.englishSeconds
        return Strings.format(Strings.shellSeconds, number.string(from: NSNumber(value: Double(milliseconds) / 1000)) ?? "0")
    }

    /// Made once: every frame draws the time of each command, and a formatter is slow to make.
    private static let russianSeconds = secondsFormatter("ru_RU")
    private static let englishSeconds = secondsFormatter("en_US")

    private static func secondsFormatter(_ locale: String) -> NumberFormatter {
        let number = NumberFormatter()
        number.minimumFractionDigits = 1
        number.maximumFractionDigits = 1
        number.minimumIntegerDigits = 1
        number.locale = Locale(identifier: locale)
        return number
    }

    private func more(_ count: Int) -> String {
        let format = switch CountForm.of(Int64(count)) {
        case .one: Strings.shellMoreOne
        case .few: Strings.shellMoreFew
        default: Strings.shellMoreMany
        }
        return Strings.format(format, count)
    }

    private func colourOf(_ style: CellStyle) -> NSColor {
        let colour = style.foreground
        if colour == CellStyle.defaultColor {
            return style.dim ? Self.muted : Self.output
        }
        let rgb: UInt32 = colour >= CellStyle.trueColor ? UInt32(colour & 0xFFFFFF)
            : colour < 16 ? Self.ansi[colour]
            : Self.cube(colour)
        if let known = colours[rgb] { return known }
        let made = Self.rgb(UInt8(rgb >> 16 & 0xFF), UInt8(rgb >> 8 & 0xFF), UInt8(rgb & 0xFF))
        colours[rgb] = made
        return made
    }

    /// The colours already made, as on Windows: every frame draws each run with one.
    private var colours: [UInt32: NSColor] = [:]

    /// The 216 colours of the cube and the 24 greys of the xterm palette.
    private static func cube(_ index: Int) -> UInt32 {
        if index >= 232 {
            let grey = UInt32(8 + (index - 232) * 10)
            return (grey << 16) | (grey << 8) | grey
        }
        let n = index - 16
        func level(_ value: Int) -> UInt32 { value == 0 ? 0 : UInt32(55 + value * 40) }
        return (level(n / 36) << 16) | (level(n / 6 % 6) << 8) | level(n % 6)
    }

    private static func rgb(_ r: UInt8, _ g: UInt8, _ b: UInt8) -> NSColor {
        Theme.nsColour(Rgba(Int(r), Int(g), Int(b)))
    }

    // ---- doors for --try-shell ----

    var blockCountForCheck: Int { rows.filter { $0.kind == .command }.count }
    var rowsForCheck: Int { rows.count }

    /// How many times the rows were counted again since the view was made.
    private(set) var refreshesForCheck = 0
    var matchesForCheck: Int { matches.count }

    func hoverForCheck(_ index: Int) {
        hover = ShellBlocks.of(screen)[index].startedAt
        needsDisplay = true
    }

    func pressForCheck(_ index: Int, _ label: String) {
        let chosen = ShellBlocks.of(screen)[index]
        if label == Strings.shellCopy { copy(chosen) }
        else if label == Strings.shellFold { fold(chosen) }
        else if label == Strings.shellExplain { explain(chosen) }
    }

    private enum RowKind {
        case command, output, more, note, gap
    }

    /// One row on screen. Line is a row of the screen for output, the count left for more, and -1
    /// for a note that tells of a failure.
    private struct Row {
        let kind: RowKind
        let block: ShellBlock
        let line: Int
        var note: String?
    }
}
