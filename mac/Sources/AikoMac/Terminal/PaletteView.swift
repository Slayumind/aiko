import AikoKit
import SwiftUI

/// One line of a palette: a name with its match marked, where it is, and a place in a text when
/// only that matched. A quiet line is drawn dimmer, as a closed session is.
struct PaletteRow: Equatable {
    let id: String
    let name: [TextPart]
    let meta: String
    var snippet: [TextPart] = []
    var quiet = false
}

/// A group of lines under a title; an empty title draws no header.
struct PaletteGroup: Equatable {
    let title: String
    let rows: [PaletteRow]
}

/// What a palette shows and does. The core finds; the palette only draws and listens to the keys.
@MainActor
final class PaletteState: ObservableObject {
    let hint: String
    let keys: String
    private let find: (String) -> [PaletteGroup]

    @Published var query = "" {
        didSet { fill() }
    }

    @Published private(set) var groups: [PaletteGroup] = []
    @Published var lit = 0

    /// A line was chosen: its id, and whether Cmd was held (a command line instead of Claude Code).
    var onPicked: ((String, Bool) -> Void)?
    var onClosed: (() -> Void)?

    init(hint: String, keys: String, find: @escaping (String) -> [PaletteGroup]) {
        self.hint = hint
        self.keys = keys
        self.find = find
        fill()
    }

    var rows: [PaletteRow] { groups.flatMap(\.rows) }

    /// More to find came in, such as conversations read in the background: the same query again.
    func refresh() { fill() }

    func move(_ step: Int) {
        let count = rows.count
        guard count > 0 else { return }
        lit = (lit + step + count) % count
    }

    func pick(_ index: Int? = nil, command: Bool) {
        let rows = rows
        let at = index ?? lit
        guard rows.indices.contains(at) else { return }
        onPicked?(rows[at].id, command)
    }

    private func fill() {
        groups = find(query).filter { !$0.rows.isEmpty }
        lit = rows.isEmpty ? -1 : 0
    }
}

/// A palette of the terminal window (D-347, D-350), the twin of PaletteView.cs: a field, the lines
/// found in groups, and the keys at the bottom.
struct PaletteView: View {
    @ObservedObject var state: PaletteState
    @FocusState private var focused: Bool

    static let cardWidth: CGFloat = 520
    private static let mostHeight: CGFloat = 380

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            field
            Rectangle().fill(Theme.hairline).frame(height: 1)
            list
            Rectangle().fill(Theme.hairline).frame(height: 1)
            // Russian keys are longer than the card: they go on, never cut at its edge.
            Text(state.keys)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.horizontal, 14)
                .padding(.top, 8)
                .padding(.bottom, 9)
        }
        .background(RoundedRectangle(cornerRadius: 12).fill(Theme.surface))
        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Theme.hairline, lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .shadow(color: .black.opacity(Theme.shadowOpacity), radius: Theme.shadowBlur / 2, y: Theme.shadowOffset)
        .onAppear { focused = true }
    }

    private var field: some View {
        HStack(spacing: 10) {
            LineMark(data: "M8,8 m-5.5,0 a5.5,5.5 0 1,0 11,0 a5.5,5.5 0 1,0 -11,0 M12,12 L16,16")
            ZStack(alignment: .leading) {
                if state.query.isEmpty {
                    Text(state.hint)
                        .font(Theme.sans(Theme.textName))
                        .foregroundStyle(Theme.muted)
                        .padding(.leading, 2)
                        .allowsHitTesting(false)
                }
                TextField("", text: $state.query)
                    .textFieldStyle(.plain)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(Theme.ink)
                    .focused($focused)
                    .onKeyPress(.downArrow) { state.move(1); return .handled }
                    .onKeyPress(.upArrow) { state.move(-1); return .handled }
                    .onKeyPress(.escape) { state.onClosed?(); return .handled }
                    .onKeyPress(.return) {
                        state.pick(command: NSEvent.modifierFlags.contains(.command))
                        return .handled
                    }
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
    }

    private var list: some View {
        ScrollViewReader { scroller in
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    let starts = Self.starts(state.groups)
                    ForEach(Array(state.groups.enumerated()), id: \.offset) { groupIndex, group in
                        if !group.title.isEmpty {
                            Text(group.title)
                                .font(Theme.mono(Theme.textTiny))
                                .foregroundStyle(Theme.muted)
                                .padding(.horizontal, 8)
                                .padding(.top, 8)
                                .padding(.bottom, 4)
                        }
                        ForEach(Array(group.rows.enumerated()), id: \.offset) { rowIndex, row in
                            let index = starts[groupIndex] + rowIndex
                            line(row, index: index).id(index)
                        }
                    }
                }
                .padding(.horizontal, 6)
                .padding(.vertical, 4)
            }
            .frame(maxHeight: Self.mostHeight)
            .fixedSize(horizontal: false, vertical: true)
            .onChange(of: state.lit) { _, lit in
                if lit >= 0 { scroller.scrollTo(lit) }
            }
        }
    }

    private func line(_ row: PaletteRow, index: Int) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 12) {
                Self.marked(row.name)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(row.quiet ? Theme.muted : Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)
                Spacer(minLength: 0)
                if !row.meta.isEmpty {
                    Text(row.meta)
                        .font(Theme.sans(Theme.textSmall))
                        .foregroundStyle(Theme.muted)
                        .lineLimit(1)
                }
            }
            if !row.snippet.isEmpty {
                Self.marked(row.snippet)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.muted)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 7)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 8).fill(index == state.lit ? Theme.raised : .clear))
        .contentShape(Rectangle())
        // Only a pointer that moves lights a line: the list changes under a still pointer as the
        // person types.
        .onContinuousHover { phase in
            if case .active = phase, index != state.lit { state.lit = index }
        }
        .onTapGesture { state.pick(index, command: NSEvent.modifierFlags.contains(.command)) }
    }

    /// The match of the query is underlined in the colour of the text.
    private static func marked(_ parts: [TextPart]) -> Text {
        parts.reduce(Text("")) { line, part in
            line + (part.match ? Text(part.text).foregroundColor(Theme.ink).underline() : Text(part.text))
        }
    }

    /// The index of the first line of each group in the whole list.
    private static func starts(_ groups: [PaletteGroup]) -> [Int] {
        var at = 0
        return groups.map { group in
            defer { at += group.rows.count }
            return at
        }
    }
}
