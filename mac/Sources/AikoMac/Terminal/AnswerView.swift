import AikoKit
import AppKit
import SwiftUI

/// An answer of Claude drawn from its markdown (D-303, D-304). The core reads the markdown and cuts
/// it into pieces; this only draws them: prose that can be selected, each block of code in its own
/// frame with a copy button, each table as a grid.
///
/// The twin of Terminal/AnswerView.cs.
struct AnswerView: View {
    let markdown: String

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ForEach(Array(AnswerSegments.of(Markdown.parse(markdown)).enumerated()), id: \.offset) { _, segment in
                switch segment {
                case .prose(let blocks):
                    ProseView(blocks: blocks)
                case .code(let language, let text):
                    CodeBlockView(language: language, text: text)
                case .table(let columns, let header, let rows):
                    TableView(columns: columns, header: header, rows: rows)
                }
            }
        }
        .padding(.top, 6)
        .padding(.bottom, 10)
        .environment(\.openURL, OpenURLAction { url in
            AnswerLinks.open(url)
            return .handled
        })
    }
}

/// Links of an answer. A web link opens the browser; "file:line" opens the file in VS Code at that
/// line. The target travels inside a URL of Aiko's own, so a path with spaces stays whole.
enum AnswerLinks {
    private static let scheme = "aiko-link"

    static func url(_ target: String) -> URL? {
        var parts = URLComponents()
        parts.scheme = scheme
        parts.host = "open"
        parts.queryItems = [URLQueryItem(name: "to", value: target)]
        return parts.url
    }

    static func open(_ url: URL) {
        guard let target = URLComponents(url: url, resolvingAgainstBaseURL: false)?
            .queryItems?.first(where: { $0.name == "to" })?.value
        else {
            return
        }

        switch LinkTarget.of(target) {
        case .web(let address):
            if let web = URL(string: address) {
                NSWorkspace.shared.open(web)
            }
        case .file(let link):
            openInEditor(link)
        case .none:
            break
        }
    }

    /// VS Code puts its command in one of these places; an app opened from the Dock does not see
    /// the PATH of a shell.
    private static let codePaths = [
        "/usr/local/bin/code",
        "/opt/homebrew/bin/code",
        "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
    ]

    private static func openInEditor(_ link: FileLink) {
        let place = link.line.map { "\(link.path):\($0)" } ?? link.path
        if let code = codePaths.first(where: { FileManager.default.isExecutableFile(atPath: $0) }) {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: code)
            process.arguments = ["-g", place]
            try? process.run()
        } else {
            NSWorkspace.shared.open(URL(fileURLWithPath: link.path))
        }
    }
}

// MARK: - Prose

private struct ProseView: View {
    let blocks: [MarkdownBlock]

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            ForEach(Array(blocks.enumerated()), id: \.offset) { _, block in
                BlockView(block: block)
            }
        }
        .padding(.bottom, 10)
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct BlockView: View {
    let block: MarkdownBlock

    var body: some View {
        switch block {
        case .paragraph(let inlines):
            Line(inlines: inlines, size: Theme.textName)
        case .heading(let level, let inlines):
            Line(inlines: inlines, size: level == 1 ? 19 : level == 2 ? 17 : Theme.textName,
                 weight: level <= 2 ? .medium : .semibold)
                .padding(.top, level == 1 ? 8 : 4)
        case .list(let ordered, let start, let items):
            VStack(alignment: .leading, spacing: 4) {
                ForEach(Array(items.enumerated()), id: \.offset) { at, item in
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        // The last number of the list sits hidden behind each marker. With digits
                        // of one width it is the widest, so all numbers end in one column and the
                        // text of all items starts in one place.
                        ZStack(alignment: .trailing) {
                            if ordered {
                                Text("\(max(1, start) + items.count - 1).").hidden()
                            }
                            Text(ordered ? "\(max(1, start) + at)." : "•")
                        }
                        .font(Theme.sans(Theme.textName))
                        .monospacedDigit()
                        .foregroundStyle(Theme.ink)
                        .frame(minWidth: 14, alignment: .trailing)
                        VStack(alignment: .leading, spacing: 4) {
                            ForEach(Array(item.enumerated()), id: \.offset) { _, inner in
                                BlockView(block: inner)
                            }
                        }
                    }
                }
            }
        case .quote(let blocks):
            HStack(alignment: .top, spacing: 12) {
                Rectangle().fill(Theme.inputLine).frame(width: 2)
                VStack(alignment: .leading, spacing: 8) {
                    ForEach(Array(blocks.enumerated()), id: \.offset) { _, inner in
                        BlockView(block: inner)
                    }
                }
                .foregroundStyle(Theme.muted)
            }
            .fixedSize(horizontal: false, vertical: true)
        case .rule:
            Rectangle().fill(Theme.hairline).frame(height: 1).padding(.vertical, 4)
        // Code and tables are pieces of their own; inside a list or a quote they stay text.
        case .code(_, let text):
            Text(text)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .textSelection(.enabled)
        case .table:
            EmptyView()
        }
    }
}

/// One line of inline markdown as one selectable text.
private struct Line: View {
    let inlines: [MarkdownInline]
    let size: CGFloat
    var weight: Font.Weight = .regular

    var body: some View {
        Text(InlineText.of(inlines, size: size, weight: weight))
            .foregroundStyle(Theme.ink)
            .lineSpacing(4)
            .textSelection(.enabled)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

enum InlineText {
    static func of(_ inlines: [MarkdownInline], size: CGFloat, weight: Font.Weight = .regular) -> AttributedString {
        var text = AttributedString()
        for inline in inlines {
            text += piece(inline, size: size, weight: weight, italic: false, strike: false)
        }
        return text
    }

    private static func piece(
        _ inline: MarkdownInline, size: CGFloat, weight: Font.Weight, italic: Bool, strike: Bool
    ) -> AttributedString {
        switch inline {
        case .text(let value):
            var run = AttributedString(value)
            run.swiftUI.font = italic ? Theme.sans(size, weight: weight).italic() : Theme.sans(size, weight: weight)
            if strike { run.swiftUI.strikethroughStyle = .single }
            return run
        case .code(let value):
            var run = AttributedString(value)
            run.swiftUI.font = Theme.mono(size - 1.5)
            run.swiftUI.backgroundColor = Theme.raised
            return run
        case .strong(let inner):
            return inner.reduce(into: AttributedString()) {
                $0 += piece($1, size: size, weight: .semibold, italic: italic, strike: strike)
            }
        case .emphasis(let inner):
            return inner.reduce(into: AttributedString()) {
                $0 += piece($1, size: size, weight: weight, italic: true, strike: strike)
            }
        case .strike(let inner):
            return inner.reduce(into: AttributedString()) {
                $0 += piece($1, size: size, weight: weight, italic: italic, strike: true)
            }
        case .link(let target, let inner):
            var run = inner.reduce(into: AttributedString()) {
                $0 += piece($1, size: size, weight: weight, italic: italic, strike: strike)
            }
            if case .none = LinkTarget.of(target) {
                return run
            }
            run.link = AnswerLinks.url(target)
            run.swiftUI.underlineStyle = .single
            run.swiftUI.foregroundColor = Theme.ink
            return run
        }
    }
}

// MARK: - Code

struct CodeBlockView: View {
    let language: String
    let text: String

    @State private var copied = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text(language.isEmpty ? " " : language)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(Theme.muted)
                Spacer()
                FlatButton(title: copied ? Strings.copied : Strings.copy, look: .ghost) {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(text, forType: .string)
                    copied = true
                }
            }
            .padding(.leading, 12)
            .padding(.trailing, 4)
            .padding(.top, 4)

            // Long lines wrap, as on Windows.
            Text(text)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, 12)
                .padding(.top, 6)
                .padding(.bottom, 12)
        }
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .padding(.top, 2)
        .padding(.bottom, 12)
    }
}

// MARK: - Tables

private struct TableView: View {
    let columns: [ColumnAlign]
    let header: [[MarkdownInline]]
    let rows: [[[MarkdownInline]]]

    var body: some View {
        Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 0) {
            row(header, bold: true)
            ForEach(Array(rows.enumerated()), id: \.offset) { _, cells in
                row(cells, bold: false)
            }
        }
        .fixedSize(horizontal: true, vertical: false)
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.top, 2)
        .padding(.bottom, 12)
    }

    @ViewBuilder
    private func row(_ cells: [[MarkdownInline]], bold: Bool) -> some View {
        GridRow {
            ForEach(Array(columns.enumerated()), id: \.offset) { at, align in
                Text(InlineText.of(at < cells.count ? cells[at] : [], size: Theme.textNumber, weight: bold ? .medium : .regular))
                    .foregroundStyle(Theme.ink)
                    .textSelection(.enabled)
                    .frame(maxWidth: 360, alignment: align == .right ? .trailing : align == .center ? .center : .leading)
                    .fixedSize(horizontal: false, vertical: true)
                    .gridColumnAlignment(align == .right ? .trailing : align == .center ? .center : .leading)
                    .padding(.vertical, 6)
            }
        }
        GridRow {
            // The line takes the width of the columns and gives the grid none of its own.
            Rectangle().fill(Theme.hairline).frame(height: 1)
                .gridCellColumns(max(1, columns.count))
                .gridCellUnsizedAxes(.horizontal)
        }
    }
}
