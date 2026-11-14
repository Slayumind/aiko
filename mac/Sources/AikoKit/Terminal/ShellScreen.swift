import Foundation

/// How a cell is drawn. A colour is -1 for the default, 0-255 for the palette, or
/// trueColor | 0xRRGGBB. The twin of CellStyle in ShellScreen.cs.
public struct CellStyle: Sendable, Hashable {
    public static let defaultColor = -1
    public static let trueColor = 0x1000000
    public static let plain = CellStyle()

    public var foreground = CellStyle.defaultColor
    public var background = CellStyle.defaultColor
    public var bold = false
    public var dim = false
    public var italic = false
    public var underline = false
    public var inverse = false

    public init() {}
}

/// A piece of a line with one style.
public struct ShellRun: Sendable, Equatable {
    public let text: String
    public let style: CellStyle
}

/// A mark of shell integration (OSC 133): A prompt, B command, C output, D end with its code. Row and
/// column are where the cursor stood; at is the time the mark came, in milliseconds.
public struct ShellMark: Sendable, Equatable {
    public let kind: Character
    public let row: Int
    public let column: Int
    public let code: Int?
    public let at: Int64
}

/// The screen of a command line as a terminal keeps it (D-412): every line since the start, the
/// cursor, the styles, and the marks the shell left. A full-screen program is not kept: the real
/// terminal shows it. Rows: spec/cases/shell-screen. The twin of ShellScreen.cs.
public final class ShellScreen {
    private static let tabWidth = 8

    /// A line longer than this many lines back is dropped from the start, with its marks.
    public static let mostLines = 50_000

    private var lines: [Line] = [Line()]
    private var styles: [CellStyle] = [.plain]
    private var styleIds: [CellStyle: Int] = [.plain: 0]
    private var markList: [ShellMark] = []
    private var parser: VtParser!
    private var columns: Int
    private var rows: Int
    private var top = 0
    private var row = 0
    private var column = 0
    private var pendingWrap = false
    private var style = CellStyle.plain
    private var saved = (row: 0, column: 0)
    private var beforeFullScreen = (row: 0, column: 0, top: 0)
    private var now: Int64 = 0

    /// Lines dropped from the start since the screen began: a row of a mark is counted from here.
    public private(set) var dropped = 0

    /// A program uses the second screen of the terminal (vim, less): the window shows the terminal.
    public private(set) var fullScreen = false

    /// Called when fullScreen changes.
    public var fullScreenChanged: ((Bool) -> Void)?

    public init(columns: Int, rows: Int) {
        self.columns = max(1, columns)
        self.rows = max(1, rows)
        parser = VtParser(Receiver(self))
    }

    public var lineCount: Int { lines.count }

    public var marks: [ShellMark] { markList }

    public var cursor: (row: Int, column: Int) { (row, column) }

    public func resize(columns: Int, rows: Int) {
        self.columns = max(1, columns)
        self.rows = max(1, rows)
        top = min(max(lines.count - self.rows, 0), row)
        column = min(column, self.columns - 1)
    }

    /// What the program wrote; now is the time in milliseconds, kept on the marks.
    public func feed(_ chunk: String, now: Int64) {
        self.now = now
        parser.feed(chunk)
        trim()
    }

    /// The text of a line without its trailing blanks.
    public func lineText(_ row: Int) -> String {
        row >= 0 && row < lines.count ? lines[row].text() : ""
    }

    /// Whether the line goes on in the next one: the terminal broke it by the width.
    public func wrapped(_ row: Int) -> Bool {
        row >= 0 && row < lines.count && lines[row].wrapped
    }

    /// The line as pieces of one style each, trailing blanks left out.
    public func runs(_ row: Int) -> [ShellRun] {
        var runs: [ShellRun] = []
        guard row >= 0 && row < lines.count else { return runs }

        let line = lines[row]
        var text = ""
        var current = -1
        for i in 0..<line.textEnd() {
            let cell = line.cells[i]
            if cell.codePoint == Cell.continuation { continue }

            if cell.style != current && !text.isEmpty {
                runs.append(ShellRun(text: text, style: styles[current]))
                text = ""
            }

            current = cell.style
            text += Self.character(cell.codePoint)
        }

        if !text.isEmpty {
            runs.append(ShellRun(text: text, style: styles[current]))
        }

        return runs
    }

    /// The text from one position up to another, lines broken by the width joined again.
    public func text(fromRow: Int, fromColumn: Int, toRow: Int, toColumn: Int) -> String {
        var text = ""
        var row = max(0, fromRow)
        while row <= toRow && row < lines.count {
            let line = lines[row]
            let start = row == fromRow ? fromColumn : 0
            let end = row == toRow ? min(toColumn, line.textEnd()) : line.textEnd()
            if start < end {
                for i in start..<end where line.cells[i].codePoint != Cell.continuation {
                    text += Self.character(line.cells[i].codePoint)
                }
            }

            if row < toRow && !line.wrapped {
                text += "\n"
            }
            row += 1
        }

        return text
    }

    fileprivate static func character(_ point: Int) -> String {
        point == 0 ? " " : Unicode.Scalar(point).map { String(Character($0)) } ?? " "
    }

    // ---- what the parser found ----

    fileprivate func print(_ codePoint: Int) {
        if fullScreen { return }

        let width = CellWidth.of(codePoint)
        if pendingWrap || column + width > columns {
            current().wrapped = true
            newLine()
            column = 0
        }

        let line = current()
        line.set(column, Cell(codePoint: codePoint, style: styleId()))
        if width == 2 {
            line.set(column + 1, Cell(codePoint: Cell.continuation, style: styleId()))
        }

        column += width
        if column >= columns {
            column = columns - 1
            pendingWrap = true
        }
    }

    fileprivate func control(_ control: UInt16) {
        if fullScreen { return }

        switch control {
        case 0x0D:
            column = 0
            pendingWrap = false
        case 0x0A, 0x0B, 0x0C:
            newLine()
        case 0x08:
            column = max(0, column - 1)
            pendingWrap = false
        case 0x09:
            column = min(columns - 1, ((column / Self.tabWidth) + 1) * Self.tabWidth)
        default:
            break
        }
    }

    fileprivate func escape(_ final: UInt16) {
        if fullScreen { return }

        switch Self.ascii(final) {
        case "7": saved = (row - top, column)
        case "8": moveTo(top + saved.row, saved.column)
        case "M": moveTo(max(top, row - 1), column)
        case "D": newLine()
        case "E":
            newLine()
            column = 0
        default: break
        }
    }

    fileprivate func csi(_ p: [Int], marker: UInt16, final: UInt16) {
        let end = Self.ascii(final)
        if marker == UInt16(UInt8(ascii: "?")) {
            if end == "h" || end == "l" {
                modes(p, on: end == "h")
            }
            return
        }

        if fullScreen || marker != 0 { return }

        func arg(_ index: Int, _ fallback: Int) -> Int { index < p.count && p[index] > 0 ? p[index] : fallback }

        switch end {
        case "H", "f": moveTo(top + arg(0, 1) - 1, arg(1, 1) - 1)
        case "A": moveTo(max(top, row - arg(0, 1)), column)
        case "B": moveTo(min(top + rows - 1, row + arg(0, 1)), column)
        case "C": moveTo(row, column + arg(0, 1))
        case "D": moveTo(row, column - arg(0, 1))
        case "E": moveTo(min(top + rows - 1, row + arg(0, 1)), 0)
        case "F": moveTo(max(top, row - arg(0, 1)), 0)
        case "G", "`": moveTo(row, arg(0, 1) - 1)
        case "d": moveTo(top + arg(0, 1) - 1, column)
        case "K": eraseLine(!p.isEmpty && p[0] > 0 ? p[0] : 0)
        case "J": eraseScreen(!p.isEmpty && p[0] > 0 ? p[0] : 0)
        case "X": current().clear(from: column, to: column + arg(0, 1), style: styleId())
        case "P": current().delete(at: column, count: arg(0, 1))
        case "@": current().insert(at: column, count: arg(0, 1))
        case "S": scrollUp(arg(0, 1))
        case "m": sgr(p)
        case "s": saved = (row - top, column)
        case "u": moveTo(top + saved.row, saved.column)
        default: break
        }
    }

    fileprivate func osc(_ text: String) {
        let units = Array(text.utf16)
        guard !fullScreen, text.hasPrefix("133;"), units.count >= 5 else { return }

        let kind = Character(Unicode.Scalar(units[4]) ?? " ")
        guard "ABCD".contains(kind) else { return }

        var code: Int?
        if kind == "D" && units.count > 6 {
            let rest = String(decoding: units[6...], as: UTF16.self)
            code = Int(rest.split(separator: ";", omittingEmptySubsequences: false).first ?? "")
        }

        markList.append(ShellMark(kind: kind, row: row + dropped, column: column, code: code, at: now))
    }

    // ---- moving and erasing ----

    private func modes(_ p: [Int], on: Bool) {
        for mode in p where [1049, 1047, 47].contains(mode) && on != fullScreen {
            if on {
                beforeFullScreen = (row, column, top)
                fullScreen = true
            } else {
                fullScreen = false
                (row, column, top) = beforeFullScreen
                pendingWrap = false
            }
            fullScreenChanged?(on)
        }
    }

    private func moveTo(_ row: Int, _ column: Int) {
        self.row = max(0, row)
        while lines.count <= self.row {
            lines.append(Line())
        }
        self.column = min(max(column, 0), columns - 1)
        pendingWrap = false
    }

    private func newLine() {
        pendingWrap = false
        row += 1
        if row >= lines.count {
            lines.append(Line())
        }
        if row > top + rows - 1 {
            top = row - rows + 1
        }
    }

    private func scrollUp(_ count: Int) {
        top += count
        row += count
        while lines.count < top + rows {
            lines.append(Line())
        }
    }

    private func eraseLine(_ how: Int) {
        let line = current()
        switch how {
        case 0:
            line.clear(from: column, to: Int.max, style: styleId())
            line.wrapped = false
        case 1:
            line.clear(from: 0, to: column + 1, style: styleId())
        default:
            line.clear(from: 0, to: Int.max, style: styleId())
            line.wrapped = false
        }
    }

    private func eraseScreen(_ how: Int) {
        let bottom = min(lines.count, top + rows)
        switch how {
        case 0:
            eraseLine(0)
            var at = row + 1
            while at < bottom {
                lines[at] = Line()
                at += 1
            }
        case 1:
            var at = top
            while at < row {
                lines[at] = Line()
                at += 1
            }
            eraseLine(1)
        default:
            var at = top
            while at < bottom {
                lines[at] = Line()
                at += 1
            }
        }
    }

    private func sgr(_ p: [Int]) {
        if p.isEmpty {
            style = .plain
            return
        }

        var i = 0
        while i < p.count {
            let code = max(0, p[i])
            switch code {
            case 0: style = .plain
            case 1: style.bold = true
            case 2: style.dim = true
            case 3: style.italic = true
            case 4: style.underline = true
            case 7: style.inverse = true
            case 22:
                style.bold = false
                style.dim = false
            case 23: style.italic = false
            case 24: style.underline = false
            case 27: style.inverse = false
            case 30...37: style.foreground = code - 30
            case 39: style.foreground = CellStyle.defaultColor
            case 40...47: style.background = code - 40
            case 49: style.background = CellStyle.defaultColor
            case 90...97: style.foreground = code - 90 + 8
            case 100...107: style.background = code - 100 + 8
            case 38, 48:
                let (color, used) = Self.extendedColor(p, i + 1)
                i += used
                if code == 38 { style.foreground = color } else { style.background = color }
            default: break
            }
            i += 1
        }
    }

    /// 5;n is the palette, 2;r;g;b a true colour. Returns the colour and how many numbers it took.
    private static func extendedColor(_ p: [Int], _ at: Int) -> (Int, Int) {
        func clamp(_ value: Int) -> Int { min(max(value, 0), 255) }
        if at < p.count && p[at] == 5 && at + 1 < p.count {
            return (clamp(p[at + 1]), 2)
        }
        if at < p.count && p[at] == 2 && at + 3 < p.count {
            let rgb = (clamp(p[at + 1]) << 16) | (clamp(p[at + 2]) << 8) | clamp(p[at + 3])
            return (CellStyle.trueColor | rgb, 4)
        }
        return (CellStyle.defaultColor, max(0, p.count - at))
    }

    private func current() -> Line {
        while lines.count <= row {
            lines.append(Line())
        }
        return lines[row]
    }

    private func styleId() -> Int {
        if let id = styleIds[style] { return id }
        let id = styles.count
        styles.append(style)
        styleIds[style] = id
        return id
    }

    /// Keeps the screen within mostLines: the oldest lines go, and so do the marks on them.
    private func trim() {
        let extra = lines.count - Self.mostLines
        guard extra > 0 else { return }

        lines.removeFirst(extra)
        row -= extra
        top = max(0, top - extra)
        dropped += extra
        markList.removeAll { $0.row < dropped }
    }

    private static func ascii(_ unit: UInt16) -> Character {
        Unicode.Scalar(unit).map(Character.init) ?? " "
    }

    fileprivate struct Cell {
        /// The second half of a wide character.
        static let continuation = -1

        var codePoint: Int
        var style: Int

        static let blank = Cell(codePoint: 0, style: 0)
    }

    /// A line is a class so the screen changes it in place, as the Line of the C# twin.
    fileprivate final class Line {
        var cells: [Cell] = []
        var length = 0
        var wrapped = false

        func set(_ column: Int, _ cell: Cell) {
            grow(column + 1)
            cells[column] = cell
        }

        func clear(from: Int, to: Int, style: Int) {
            let end = min(to, length)
            var i = max(0, from)
            while i < end {
                cells[i] = Cell(codePoint: 0, style: style)
                i += 1
            }
        }

        func delete(at: Int, count: Int) {
            guard at < length else { return }
            let count = min(count, length - at)
            for i in at..<(length - count) {
                cells[i] = cells[i + count]
            }
            length -= count
        }

        func insert(at: Int, count: Int) {
            guard at < length else { return }
            grow(length + count)
            var i = length - 1
            while i >= at + count {
                cells[i] = cells[i - count]
                i -= 1
            }
            for j in at..<(at + count) {
                cells[j] = .blank
            }
        }

        func textEnd() -> Int {
            var end = length
            while end > 0 && (cells[end - 1].codePoint == 0 || cells[end - 1].codePoint == 0x20) {
                end -= 1
            }
            return end
        }

        func text() -> String {
            var text = ""
            for i in 0..<textEnd() where cells[i].codePoint != Cell.continuation {
                text += ShellScreen.character(cells[i].codePoint)
            }
            return text
        }

        private func grow(_ needed: Int) {
            if needed > cells.count {
                let size = max(needed, max(16, cells.count * 2))
                cells.append(contentsOf: repeatElement(.blank, count: size - cells.count))
            }
            length = max(length, needed)
        }
    }

    /// Hands what the parser found to the screen; it holds the screen weakly, so the two do not keep
    /// each other alive.
    private final class Receiver: VtReceiver {
        weak var screen: ShellScreen?

        init(_ screen: ShellScreen) { self.screen = screen }

        func print(_ codePoint: Int) { screen?.print(codePoint) }
        func control(_ control: UInt16) { screen?.control(control) }
        func csi(_ parameters: [Int], marker: UInt16, final: UInt16) { screen?.csi(parameters, marker: marker, final: final) }
        func escape(_ final: UInt16) { screen?.escape(final) }
        func osc(_ text: String) { screen?.osc(text) }
    }
}

/// How many cells a character takes: two for the wide ones of East Asia and most emoji.
public enum CellWidth {
    public static func of(_ codePoint: Int) -> Int {
        switch codePoint {
        case 0x1100...0x115F, 0x2E80...0xA4CF, 0xAC00...0xD7A3, 0xF900...0xFAFF, 0xFE30...0xFE4F,
             0xFF00...0xFF60, 0xFFE0...0xFFE6, 0x1F300...0x1FAFF, 0x20000...0x3FFFD:
            return 2
        default:
            return 1
        }
    }
}
