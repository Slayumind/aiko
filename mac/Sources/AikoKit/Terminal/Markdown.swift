import Foundation

/// A piece of a line of an answer.
public enum MarkdownInline: Sendable, Equatable {
    case text(String)
    case code(String)
    case strong([MarkdownInline])
    case emphasis([MarkdownInline])
    case strike([MarkdownInline])

    /// A picture is a link too: Aiko does not load pictures from the internet (D-294).
    case link(target: String, inlines: [MarkdownInline])
}

public enum ColumnAlign: Sendable, Equatable {
    case none
    case left
    case center
    case right
}

/// A block of an answer.
public enum MarkdownBlock: Sendable, Equatable {
    case paragraph([MarkdownInline])
    case heading(level: Int, inlines: [MarkdownInline])
    case code(language: String, text: String)

    /// The start is the number of the first item of an ordered list.
    case list(ordered: Bool, start: Int, items: [[MarkdownBlock]])
    case quote([MarkdownBlock])
    case table(columns: [ColumnAlign], header: [[MarkdownInline]], rows: [[[MarkdownInline]]])
    case rule
}

/// Reads the markdown of an answer into blocks (D-303).
///
/// This is a part of markdown, the part Claude writes: paragraphs, headings with #, lists, fenced
/// code, tables, quotes, rules, links, bold, italic and strike. Everything else stays text as it
/// was written. Three things differ from the full rules on purpose. A line break inside a
/// paragraph is kept, the way a terminal shows it. Code is only fenced, never made by an indent.
/// A fence that was not closed runs to the end, so an answer that is still being written already
/// shows its code as code.
///
/// The text is read in UTF-16 units, the way the Windows core reads a string, so both cores cut it
/// in the same places. Every character the rules look at is ASCII; anything above ASCII counts as
/// a letter.
public enum Markdown {
    typealias Units = [UInt16]

    public static func parse(_ text: String?) -> [MarkdownBlock] {
        var units: Units = []
        let source = Array((text ?? "").utf16)
        var at = 0
        while at < source.count {
            if source[at] == Ch.carriage {
                units.append(Ch.newline)
                if at + 1 < source.count && source[at + 1] == Ch.newline {
                    at += 1
                }
            } else {
                units.append(source[at])
            }
            at += 1
        }

        let lines = units.split(separator: Ch.newline, omittingEmptySubsequences: false).map(Array.init)
        return blocks(lines)
    }

    // MARK: - Blocks

    private static func blocks(_ lines: [Units]) -> [MarkdownBlock] {
        var blocks: [MarkdownBlock] = []
        var at = 0

        while at < lines.count {
            let content = trimStart(lines[at])

            if content.isEmpty {
                at += 1
            } else if let fence = fenceIn(content) {
                at = readCode(lines, at, fence, &blocks)
            } else if let heading = headingIn(content) {
                blocks.append(heading)
                at += 1
            } else if isRule(content) {
                blocks.append(.rule)
                at += 1
            } else if content[0] == Ch.greater {
                at = readQuote(lines, at, &blocks)
            } else if let marker = markerIn(content) {
                at = readList(lines, at, marker, &blocks)
            } else if tableStartsAt(lines, at) {
                at = readTable(lines, at, &blocks)
            } else {
                at = readParagraph(lines, at, &blocks)
            }
        }

        return blocks
    }

    private struct Fence {
        let mark: UInt16
        let length: Int
        let language: String
    }

    /// Three or more backticks or tildes. The first word after them names the language.
    private static func fenceIn(_ content: Units) -> Fence? {
        let mark = content[0]
        if mark != Ch.backtick && mark != Ch.tilde {
            return nil
        }

        let length = runLength(content, 0)
        if length < 3 {
            return nil
        }

        let info = trim(sub(content, length))
        if mark == Ch.backtick && info.contains(Ch.backtick) {
            return nil
        }

        let end = info.firstIndex { isBlank($0) } ?? info.count
        return Fence(mark: mark, length: length, language: string(sub(info, 0, end)))
    }

    private static func readCode(_ lines: [Units], _ start: Int, _ fence: Fence, _ blocks: inout [MarkdownBlock]) -> Int {
        let indent = indentOf(lines[start])
        var body: [Units] = []
        var at = start + 1

        while at < lines.count {
            let content = trimStart(lines[at])
            if content.count >= fence.length
                && content[0] == fence.mark
                && runLength(content, 0) >= fence.length
                && trim(sub(content, runLength(content, 0))).isEmpty {
                at += 1
                break
            }

            body.append(withoutIndent(lines[at], indent))
            at += 1
        }

        blocks.append(.code(language: fence.language, text: body.map(string).joined(separator: "\n")))
        return at
    }

    /// One to six # and a space. A # without the space is a word, not a heading.
    private static func headingIn(_ content: Units) -> MarkdownBlock? {
        let level = content[0] == Ch.hash ? runLength(content, 0) : 0
        if level < 1 || level > 6 || (content.count > level && !isBlank(content[level])) {
            return nil
        }

        var title = trim(sub(content, level))
        var closing = title.count
        while closing > 0 && title[closing - 1] == Ch.hash {
            closing -= 1
        }
        if closing < title.count && (closing == 0 || title[closing - 1] == Ch.space) {
            title = trimEnd(sub(title, 0, closing))
        }

        return .heading(level: level, inlines: inlines(title))
    }

    /// Three or more of - * or _ and nothing else but spaces.
    private static func isRule(_ content: Units) -> Bool {
        let mark = content[0]
        if mark != Ch.dash && mark != Ch.star && mark != Ch.underscore {
            return false
        }

        var count = 0
        for unit in content {
            if unit == mark {
                count += 1
            } else if !isBlank(unit) {
                return false
            }
        }
        return count >= 3
    }

    private static func readQuote(_ lines: [Units], _ start: Int, _ blocks: inout [MarkdownBlock]) -> Int {
        var inner: [Units] = []
        var at = start
        while at < lines.count {
            let content = trimStart(lines[at])
            if content.isEmpty || content[0] != Ch.greater {
                break
            }

            inner.append(content.count > 1 && content[1] == Ch.space ? sub(content, 2) : sub(content, 1))
            at += 1
        }

        blocks.append(.quote(self.blocks(inner)))
        return at
    }

    private struct Marker {
        let ordered: Bool
        let start: Int
        let width: Int
    }

    /// A bullet (- * +) or a number with a dot or a bracket, then a space.
    private static func markerIn(_ content: Units) -> Marker? {
        if content[0] == Ch.dash || content[0] == Ch.star || content[0] == Ch.plus {
            return content.count > 1 && content[1] == Ch.space ? Marker(ordered: false, start: 1, width: 2) : nil
        }

        var digits = 0
        while digits < content.count && isDigit(content[digits]) {
            digits += 1
        }

        if digits < 1 || digits > 9
            || digits + 1 >= content.count
            || (content[digits] != Ch.dot && content[digits] != Ch.closeParen)
            || content[digits + 1] != Ch.space {
            return nil
        }

        return Marker(ordered: true, start: Int(string(sub(content, 0, digits))) ?? 1, width: digits + 2)
    }

    private static func readList(
        _ lines: [Units], _ start: Int, _ first: Marker, _ blocks: inout [MarkdownBlock]
    ) -> Int {
        let listIndent = indentOf(lines[start])
        var items: [[MarkdownBlock]] = []
        var item: [Units]?
        var contentIndent = 0
        var at = start

        while at < lines.count {
            let line = lines[at]
            let content = trimStart(line)
            let indent = indentOf(line)

            if content.isEmpty {
                // An empty line stays in the item only when the item goes on after it.
                let next = nextWithText(lines, at)
                if next < 0
                    || !(indentOf(lines[next]) >= contentIndent
                        || isSibling(lines[next], listIndent, contentIndent, first)) {
                    break
                }
                item?.append([])
                at += 1
            } else if isSibling(line, listIndent, contentIndent, first) || item == nil {
                if let done = item {
                    items.append(self.blocks(done))
                }
                guard let marker = markerIn(content) else { break }
                contentIndent = indent + marker.width
                item = [sub(content, marker.width)]
                at += 1
            } else if indent >= contentIndent {
                item?.append(withoutIndent(line, contentIndent))
                at += 1
            } else {
                break
            }
        }

        if let done = item {
            items.append(self.blocks(done))
        }

        blocks.append(.list(ordered: first.ordered, start: first.start, items: items))
        return at
    }

    /// The next item of the same list: a marker of the same kind that is not deep enough to
    /// belong to the item above it.
    private static func isSibling(_ line: Units, _ listIndent: Int, _ contentIndent: Int, _ first: Marker) -> Bool {
        let indent = indentOf(line)
        let content = trimStart(line)
        guard indent >= listIndent, indent < contentIndent, !content.isEmpty, !isRule(content),
              let marker = markerIn(content) else {
            return false
        }
        return marker.ordered == first.ordered
    }

    private static func nextWithText(_ lines: [Units], _ at: Int) -> Int {
        var next = at + 1
        while next < lines.count {
            if !trim(lines[next]).isEmpty {
                return next
            }
            next += 1
        }
        return -1
    }

    /// A row with a bar in it, and under it a row of dashes with the same number of cells.
    private static func tableStartsAt(_ lines: [Units], _ at: Int) -> Bool {
        if at + 1 >= lines.count || !lines[at].contains(Ch.bar) {
            return false
        }

        guard let columns = columnsIn(lines[at + 1]) else { return false }
        return columns.count == cells(lines[at]).count
    }

    private static func columnsIn(_ line: Units) -> [ColumnAlign]? {
        if !line.contains(Ch.dash) {
            return nil
        }

        var columns: [ColumnAlign] = []
        for cell in cells(line) {
            let left = cell.first == Ch.colon
            let right = cell.count > 1 && cell.last == Ch.colon
            let dashes = sub(cell, left ? 1 : 0, cell.count - (right ? 1 : 0))
            if dashes.isEmpty || dashes.contains(where: { $0 != Ch.dash }) {
                return nil
            }

            columns.append(left && right ? .center : left ? .left : right ? .right : .none)
        }
        return columns
    }

    /// The cells of a row. A bar at either end is the frame, and \| is a bar inside a cell.
    private static func cells(_ line: Units) -> [Units] {
        var row = trim(line)
        if row.first == Ch.bar {
            row = sub(row, 1)
        }
        if row.last == Ch.bar && !(row.count >= 2 && row[row.count - 2] == Ch.backslash) {
            row = sub(row, 0, row.count - 1)
        }

        var cells: [Units] = []
        var cell: Units = []
        var at = 0
        while at < row.count {
            if row[at] == Ch.backslash && at + 1 < row.count && row[at + 1] == Ch.bar {
                cell.append(Ch.bar)
                at += 1
            } else if row[at] == Ch.bar {
                cells.append(trim(cell))
                cell = []
            } else {
                cell.append(row[at])
            }
            at += 1
        }
        cells.append(trim(cell))
        return cells
    }

    private static func readTable(_ lines: [Units], _ start: Int, _ blocks: inout [MarkdownBlock]) -> Int {
        let columns = columnsIn(lines[start + 1]) ?? []
        let header = cells(lines[start]).map(inlines)
        var rows: [[[MarkdownInline]]] = []
        var at = start + 2

        while at < lines.count && lines[at].contains(Ch.bar) {
            let cells = self.cells(lines[at])
            var row: [[MarkdownInline]] = []
            for column in 0..<columns.count {
                row.append(inlines(column < cells.count ? cells[column] : []))
            }
            rows.append(row)
            at += 1
        }

        blocks.append(.table(columns: columns, header: header, rows: rows))
        return at
    }

    private static func readParagraph(_ lines: [Units], _ start: Int, _ blocks: inout [MarkdownBlock]) -> Int {
        var text: [Units] = []
        var at = start
        while at < lines.count {
            let content = trim(lines[at])
            if content.isEmpty || (!text.isEmpty && startsAnotherBlock(lines, at, content)) {
                break
            }
            text.append(content)
            at += 1
        }

        blocks.append(.paragraph(inlines(Array(text.joined(separator: [Ch.newline])))))
        return at
    }

    /// What ends a paragraph without an empty line. A numbered list does it only when it starts
    /// at one: "2024. was a good year" in the middle of a paragraph is a sentence.
    private static func startsAnotherBlock(_ lines: [Units], _ at: Int, _ content: Units) -> Bool {
        if fenceIn(content) != nil || headingIn(content) != nil || isRule(content) || content[0] == Ch.greater {
            return true
        }
        if let marker = markerIn(content), !marker.ordered || marker.start == 1 {
            return true
        }
        return tableStartsAt(lines, at)
    }

    /// Spaces in front of a line. A tab counts as four.
    private static func indentOf(_ line: Units) -> Int {
        var indent = 0
        for unit in line {
            if unit == Ch.space {
                indent += 1
            } else if unit == Ch.tab {
                indent += 4
            } else {
                break
            }
        }
        return indent
    }

    /// The line without its first spaces, up to the given number of them.
    private static func withoutIndent(_ line: Units, _ indent: Int) -> Units {
        var at = 0
        var removed = 0
        while at < line.count && removed < indent && isBlank(line[at]) {
            removed += line[at] == Ch.tab ? 4 : 1
            at += 1
        }
        return sub(line, at)
    }

    private static func runLength(_ text: Units, _ at: Int) -> Int {
        var end = at
        while end < text.count && text[end] == text[at] {
            end += 1
        }
        return end - at
    }

    // MARK: - Inlines

    private static func inlines(_ text: Units) -> [MarkdownInline] {
        var inlines: [MarkdownInline] = []
        var plain: Units = []
        var at = 0

        func add(_ found: Found) {
            if !plain.isEmpty {
                inlines.append(.text(string(plain)))
                plain = []
            }
            inlines.append(found.inline)
            at = found.next
        }

        while at < text.count {
            let unit = text[at]

            if unit == Ch.backslash && at + 1 < text.count && isPunctuation(text[at + 1]) {
                plain.append(text[at + 1])
                at += 2
            } else if unit == Ch.backtick, let span = codeSpanAt(text, at) {
                add(Found(inline: .code(string(span.text)), next: span.next))
            } else if unit == Ch.openBracket || (unit == Ch.bang && at + 1 < text.count && text[at + 1] == Ch.openBracket),
                      let link = linkAt(text, at) {
                add(link)
            } else if unit == Ch.less, let angle = angleLinkAt(text, at) {
                add(angle)
            } else if unit == Ch.letterH, let bare = bareLinkAt(text, at) {
                add(bare)
            } else if unit == Ch.star || unit == Ch.underscore || unit == Ch.tilde, let emphasis = emphasisAt(text, at) {
                add(emphasis)
            } else if unit == Ch.backtick || unit == Ch.star || unit == Ch.underscore || unit == Ch.tilde {
                let run = runLength(text, at)
                plain.append(contentsOf: text[at..<(at + run)])
                at += run
            } else {
                plain.append(unit)
                at += 1
            }
        }

        if !plain.isEmpty {
            inlines.append(.text(string(plain)))
        }
        return inlines
    }

    private struct Found {
        let inline: MarkdownInline
        let next: Int
    }

    private struct Span {
        let text: Units
        let next: Int
    }

    /// Code between two runs of backticks of the same length.
    private static func codeSpanAt(_ text: Units, _ at: Int) -> Span? {
        let run = runLength(text, at)
        let from = at + run

        var search = from
        while search < text.count {
            if text[search] != Ch.backtick {
                search += 1
                continue
            }

            let closing = runLength(text, search)
            if closing == run {
                var code = sub(text, from, search).map { $0 == Ch.newline ? Ch.space : $0 }
                if code.count >= 2 && code.first == Ch.space && code.last == Ch.space
                    && code.contains(where: { $0 != Ch.space }) {
                    code = sub(code, 1, code.count - 1)
                }
                return Span(text: code, next: search + closing)
            }
            search += closing
        }
        return nil
    }

    /// [words](target) and ![words](target). A title after the target is read and dropped.
    private static func linkAt(_ text: Units, _ at: Int) -> Found? {
        let open = text[at] == Ch.bang ? at + 1 : at
        let close = closingBracket(text, open)
        if close < 0 || close + 1 >= text.count || text[close + 1] != Ch.openParen {
            return nil
        }

        var cursor = close + 2
        while cursor < text.count && isSpace(text[cursor]) {
            cursor += 1
        }

        var target: Units
        if cursor < text.count && text[cursor] == Ch.less {
            guard let end = index(of: Ch.greater, in: text, from: cursor) else { return nil }
            target = sub(text, cursor + 1, end)
            cursor = end + 1
        } else {
            let start = cursor
            var depth = 0
            while cursor < text.count && !isSpace(text[cursor]) {
                if text[cursor] == Ch.openParen {
                    depth += 1
                } else if text[cursor] == Ch.closeParen {
                    if depth == 0 {
                        break
                    }
                    depth -= 1
                }
                cursor += 1
            }
            target = sub(text, start, cursor)
        }

        while cursor < text.count && isSpace(text[cursor]) {
            cursor += 1
        }

        if cursor < text.count && (text[cursor] == Ch.quote || text[cursor] == Ch.apostrophe) {
            guard let end = index(of: text[cursor], in: text, from: cursor + 1) else { return nil }
            cursor = end + 1
            while cursor < text.count && isSpace(text[cursor]) {
                cursor += 1
            }
        }

        if cursor >= text.count || text[cursor] != Ch.closeParen || target.isEmpty {
            return nil
        }

        var words = inlines(sub(text, open + 1, close))
        if words.isEmpty {
            words.append(.text(string(target)))
        }
        return Found(inline: .link(target: string(target), inlines: words), next: cursor + 1)
    }

    /// The bracket that closes the one at this place. Brackets inside it are counted, code and
    /// escaped characters are stepped over.
    private static func closingBracket(_ text: Units, _ open: Int) -> Int {
        var depth = 0
        var at = open
        while at < text.count {
            if text[at] == Ch.backslash {
                at += 1
            } else if text[at] == Ch.backtick, let span = codeSpanAt(text, at) {
                at = span.next - 1
            } else if text[at] == Ch.openBracket {
                depth += 1
            } else if text[at] == Ch.closeBracket {
                depth -= 1
                if depth == 0 {
                    return at
                }
            }
            at += 1
        }
        return -1
    }

    /// <https://example.com>
    private static func angleLinkAt(_ text: Units, _ at: Int) -> Found? {
        guard let end = index(of: Ch.greater, in: text, from: at) else { return nil }

        let target = sub(text, at + 1, end)
        guard isWebAddress(target), !target.contains(where: isSpace) else { return nil }
        return Found(inline: .link(target: string(target), inlines: [.text(string(target))]), next: end + 1)
    }

    /// An address written as it is. It ends at a space; a full stop or a bracket after it is not
    /// part of it.
    private static func bareLinkAt(_ text: Units, _ at: Int) -> Found? {
        if at > 0 && isWordCharacter(text[at - 1]) {
            return nil
        }

        var end = at
        while end < text.count && !isSpace(text[end]) && text[end] != Ch.less && text[end] != Ch.backtick {
            end += 1
        }

        while end > at && isTail(text, at, end) {
            end -= 1
        }

        let target = sub(text, at, end)
        guard isWebAddress(target) else { return nil }
        return Found(inline: .link(target: string(target), inlines: [.text(string(target))]), next: end)
    }

    private static func isTail(_ text: Units, _ from: Int, _ end: Int) -> Bool {
        let last = text[end - 1]
        if last == Ch.closeParen {
            let address = text[from..<end]
            return address.filter { $0 == Ch.closeParen }.count > address.filter { $0 == Ch.openParen }.count
        }
        return Ch.tails.contains(last)
    }

    private static func isWebAddress(_ target: Units) -> Bool {
        (target.starts(with: Ch.https) && target.count > 8) || (target.starts(with: Ch.http) && target.count > 7)
    }

    /// **bold**, *italic*, ***both*** with * or _, and ~~strike~~.
    private static func emphasisAt(_ text: Units, _ at: Int) -> Found? {
        let mark = text[at]
        let run = runLength(text, at)
        let after = at + run

        if after >= text.count || isSpace(text[after]) || run > 3
            || (mark == Ch.tilde && run != 2)
            || (mark == Ch.underscore && at > 0 && isWordCharacter(text[at - 1])) {
            return nil
        }

        var size = run
        while size >= 1 {
            defer { size -= 1 }
            if mark == Ch.tilde && size != 2 {
                continue
            }

            let from = at + size
            guard let closer = closerOf(text, from, mark, size) else { continue }

            let inner = inlines(sub(text, from, closer.contentEnd))
            let inline: MarkdownInline = mark == Ch.tilde ? .strike(inner)
                : size == 1 ? .emphasis(inner)
                : size == 2 ? .strong(inner)
                : .emphasis([.strong(inner)])
            return Found(inline: inline, next: closer.next)
        }
        return nil
    }

    private struct Closer {
        let contentEnd: Int
        let next: Int
    }

    /// The run that closes an emphasis of this size. A run of exactly this size wins; if there is
    /// none, the end of the first longer run is taken, so "**bold *both***" closes both.
    private static func closerOf(_ text: Units, _ from: Int, _ mark: UInt16, _ size: Int) -> Closer? {
        var longer: Closer?

        var at = from
        while at < text.count {
            if text[at] == Ch.backslash {
                at += 1
            } else if text[at] == Ch.backtick, let span = codeSpanAt(text, at) {
                at = span.next - 1
            } else if text[at] == mark {
                let run = runLength(text, at)
                let next = at + run
                let closes = at > from
                    && !isSpace(text[at - 1])
                    && (mark != Ch.underscore || next >= text.count || !isWordCharacter(text[next]))

                if closes && run == size {
                    return Closer(contentEnd: at, next: next)
                }
                if closes && run > size && longer == nil {
                    longer = Closer(contentEnd: next - size, next: next)
                }
                at = next - 1
            }
            at += 1
        }
        return longer
    }

    // MARK: - Characters

    private static func isBlank(_ unit: UInt16) -> Bool {
        unit == Ch.space || unit == Ch.tab
    }

    private static func isSpace(_ unit: UInt16) -> Bool {
        unit == Ch.space || unit == Ch.tab || unit == Ch.newline
    }

    private static func isDigit(_ unit: UInt16) -> Bool {
        unit >= 48 && unit <= 57
    }

    /// A letter or a digit. Everything above ASCII counts as a letter.
    private static func isWordCharacter(_ unit: UInt16) -> Bool {
        isDigit(unit) || (unit >= 65 && unit <= 90) || (unit >= 97 && unit <= 122) || unit >= 0x80
    }

    private static func isPunctuation(_ unit: UInt16) -> Bool {
        (unit >= 33 && unit <= 47) || (unit >= 58 && unit <= 64) || (unit >= 91 && unit <= 96)
            || (unit >= 123 && unit <= 126)
    }

    private static func string(_ units: Units) -> String {
        String(decoding: units, as: UTF16.self)
    }

    private static func sub(_ units: Units, _ from: Int, _ to: Int? = nil) -> Units {
        let end = to ?? units.count
        return from >= end ? [] : Array(units[from..<end])
    }

    private static func index(of unit: UInt16, in units: Units, from: Int) -> Int? {
        var at = from
        while at < units.count {
            if units[at] == unit {
                return at
            }
            at += 1
        }
        return nil
    }

    private static func trimStart(_ units: Units) -> Units {
        var from = 0
        while from < units.count && isBlank(units[from]) {
            from += 1
        }
        return sub(units, from)
    }

    private static func trimEnd(_ units: Units) -> Units {
        var end = units.count
        while end > 0 && isBlank(units[end - 1]) {
            end -= 1
        }
        return sub(units, 0, end)
    }

    private static func trim(_ units: Units) -> Units {
        trimEnd(trimStart(units))
    }

    /// The ASCII characters the rules look at, as UTF-16 units.
    private enum Ch {
        static let tab: UInt16 = 9
        static let newline: UInt16 = 10
        static let carriage: UInt16 = 13
        static let space: UInt16 = 32
        static let bang: UInt16 = 33
        static let quote: UInt16 = 34
        static let hash: UInt16 = 35
        static let apostrophe: UInt16 = 39
        static let openParen: UInt16 = 40
        static let closeParen: UInt16 = 41
        static let star: UInt16 = 42
        static let plus: UInt16 = 43
        static let dash: UInt16 = 45
        static let dot: UInt16 = 46
        static let colon: UInt16 = 58
        static let less: UInt16 = 60
        static let greater: UInt16 = 62
        static let openBracket: UInt16 = 91
        static let backslash: UInt16 = 92
        static let closeBracket: UInt16 = 93
        static let underscore: UInt16 = 95
        static let backtick: UInt16 = 96
        static let letterH: UInt16 = 104
        static let bar: UInt16 = 124
        static let tilde: UInt16 = 126

        static let https = Array("https://".utf16)
        static let http = Array("http://".utf16)

        /// What may follow an address without being part of it: . , ; : ! ? ' " * _ ~ ]
        static let tails: Set<UInt16> = [46, 44, 59, 58, 33, 63, 39, 34, 42, 95, 126, 93]
    }
}
