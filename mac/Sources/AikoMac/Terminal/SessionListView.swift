import AikoKit
import AppKit
import SwiftUI

/// The left side of the terminal window, the twin of TerminalPanel.SessionList.cs: the sessions of
/// the chosen environment by folder. Each folder shows three quiet buttons under the mouse: Claude
/// Code and a command line in it with no question (D-350), and the folder in Finder (D-408). A row
/// says what runs in it, its name and its state, and under them what it did last or when it was used.
struct SessionListView: View {
    @ObservedObject var state: TerminalState

    /// In the card of a narrow window the list is only as tall as its rows, up to the card.
    var inCard = false

    /// Where each row stands in the list, for the drag.
    @State private var frames: [String: CGRect] = [:]

    /// The row being dragged, how far the pointer went, and where it would land.
    @State private var drag: (id: String, ids: [String], moved: CGSize, to: Int)?

    /// The copy settles into its place before the list takes the new order.
    @State private var landing = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 0) {
                    ForEach(state.groups, id: \.folder) { group in
                        GroupHeader(group: group, state: state)
                        ForEach(group.rows, id: \.id) { row in
                            SessionRowView(row: row, chosen: row.id == state.chosen && !state.settingsOpen, state: state)
                                .opacity(drag?.id == row.id ? RowDragLook.pale : 1)
                                .offset(y: shift(row.id))
                                .background(GeometryReader { place in
                                    Color.clear.preference(key: RowFrames.self, value: [row.id: place.frame(in: .named(RowDragLook.space))])
                                })
                                .gesture(dragGesture(row.id, group.rows.map(\.id)))
                        }
                    }
                }
                .padding(.horizontal, 8)
                .padding(.top, 4)
                .coordinateSpace(name: RowDragLook.space)
                .onPreferenceChange(RowFrames.self) { found in
                    if drag == nil {
                        frames = found
                        state.rowFramesForCheck = found
                    }
                }
                .overlay(alignment: .topLeading) { copy }
            }
            .fixedSize(horizontal: false, vertical: inCard)
            newSession
        }
        .frame(maxHeight: inCard ? nil : .infinity, alignment: .top)
        .background(Theme.surface)
    }

    // ---- moving a session in its folder (D-400, D-401), the twin of RowDrag.cs ----

    /// After 6 points a copy of the row lifts off and follows the pointer; the row stays as a pale
    /// place that moves through its folder, and the other rows slide out of its way.
    private func dragGesture(_ id: String, _ ids: [String]) -> some Gesture {
        DragGesture(minimumDistance: RowDragLook.start, coordinateSpace: .named(RowDragLook.space))
            .onChanged { change in
                guard !landing, drag == nil || drag?.id == id else { return }
                if drag == nil { state.dragging = id }
                let to = place(id, ids, change.translation.height)
                withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: RowDragLook.slide)) {
                    drag = (id, ids, change.translation, to)
                }
            }
            .onEnded { _ in
                guard let ended = drag, ended.id == id else { return }
                landing = true
                // The copy glides into the place, then the list takes the new order.
                withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: RowDragLook.land)) {
                    drag = (ended.id, ended.ids, CGSize(width: 0, height: shift(ended.id)), ended.to)
                }
                DispatchQueue.main.asyncAfter(deadline: .now() + (Motion.reduceMotion ? 0 : RowDragLook.land)) {
                    let others = ended.ids.filter { $0 != ended.id }
                    let before = ended.to < others.count ? others[ended.to] : nil
                    drag = nil
                    landing = false
                    state.dragging = nil
                    if ended.ids.firstIndex(of: ended.id) != ended.to {
                        state.onMove?(ended.id, before)
                    } else {
                        state.onDragEnded?()
                    }
                }
            }
    }

    /// Where the dragged row would land among the other rows of its folder: before the first one
    /// whose middle is below the middle of the copy.
    private func place(_ id: String, _ ids: [String], _ moved: CGFloat) -> Int {
        guard let own = frames[id] else { return ids.firstIndex(of: id) ?? 0 }
        let middle = own.midY + moved
        let others = ids.filter { $0 != id }
        return others.firstIndex { (frames[$0]?.midY ?? .infinity) > middle } ?? others.count
    }

    /// How far a row of the dragged folder moves from where it stands: the new order is laid from
    /// the top of the folder down, row by row.
    private func shift(_ id: String) -> CGFloat {
        guard let drag, drag.ids.contains(id), let first = drag.ids.first.flatMap({ frames[$0] }) else { return 0 }
        var order = drag.ids.filter { $0 != drag.id }
        order.insert(drag.id, at: min(drag.to, order.count))
        var top = first.minY
        for each in order {
            guard let frame = frames[each] else { return 0 }
            if each == id { return top - frame.minY }
            top += frame.height
        }
        return 0
    }

    /// The copy that follows the pointer, tilted and with the shadow of cards.
    @ViewBuilder private var copy: some View {
        if let drag, let frame = frames[drag.id],
           let row = state.groups.flatMap(\.rows).first(where: { $0.id == drag.id }) {
            SessionRowView(row: row, chosen: row.id == state.chosen, state: state)
                .frame(width: frame.width)
                .background(RoundedRectangle(cornerRadius: 10).fill(Theme.raised))
                .rotationEffect(.degrees(landing ? 0 : RowDragLook.tilt))
                .shadow(color: .black.opacity(Theme.shadowOpacity), radius: Theme.shadowBlur / 2, y: Theme.shadowOffset)
                .offset(x: frame.minX, y: frame.minY + drag.moved.height)
                .allowsHitTesting(false)
        }
    }

    /// One button under the list: the quick pick of a folder, with its key (D-353).
    private var newSession: some View {
        WindowButton(title: Strings.terminalNewSession, look: .outlined, plus: true, aside: Strings.terminalNewSessionKey) {
            state.onNewInOtherFolder?()
        }
        .padding(8)
    }
}

/// The folder of a group, and under the mouse its three buttons.
private struct GroupHeader: View {
    let group: RowGroup
    @ObservedObject var state: TerminalState

    @State private var hovered = false
    @State private var gone = false

    var body: some View {
        HStack(spacing: 0) {
            Text(group.name)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .lineLimit(1)
                .truncationMode(.tail)
            Spacer(minLength: 4)
            HStack(spacing: 0) {
                small("M5,0 L5,10 M0,5 L10,5", Strings.terminalGroupNewSession) { state.onNew?(group.folder, .claude) }
                small("M0,1 L4,5 L0,9 M6,9 L11,9", Strings.terminalGroupNewShell) { state.onNew?(group.folder, .shell) }
                small("M0.5,1.5 L4,1.5 L5.5,3 L11.5,3 L11.5,9.5 L0.5,9.5 Z",
                      gone ? Strings.terminalFolderGone : Strings.terminalGroupOpenFolder) {
                    gone = state.onOpenFolder?(group.folder) == false
                    // A folder gone from the disk opens nothing: the tip says so at once, until the
                    // pointer leaves the button.
                    if gone {
                        DispatchQueue.main.async { Tips.show(Strings.terminalFolderGone, at: NSEvent.mouseLocation, zoom: Tips.windowZoom) }
                    }
                }
            }
            .opacity(hovered ? 1 : 0)
        }
        .padding(.leading, 10)
        .padding(.trailing, 4)
        .padding(.top, 8)
        .contentShape(Rectangle())
        .tip(group.folder)
        .onHover { inside in
            withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: 0.12)) { hovered = inside }
            if !inside { gone = false }
        }
    }

    private func small(_ data: String, _ tip: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            LineMark(data: data)
                .frame(width: 22, height: 22)
                .modifier(HoverLayer(radius: 6))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .tip(tip)
    }
}

/// One session: the kind, the name and the state on top, the time or the deed under them. A double
/// click on the name changes it in place; the menu of the row renames or closes it.
private struct SessionRowView: View {
    let row: SessionRow
    let chosen: Bool
    @ObservedObject var state: TerminalState

    @State private var renaming = false
    @State private var newName = ""
    @FocusState private var fieldFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 0) {
                kind
                    .frame(width: 14)
                    .padding(.trailing, 8)
                if renaming {
                    TextField("", text: $newName)
                        .textFieldStyle(.plain)
                        .font(Theme.sans(Theme.textName))
                        .foregroundStyle(Theme.ink)
                        .padding(.horizontal, 4)
                        .padding(.vertical, 1)
                        .overlay(RoundedRectangle(cornerRadius: 6).stroke(Theme.inputLine, lineWidth: 1))
                        .focused($fieldFocused)
                        .onSubmit { finishRename(keep: true) }
                        .onExitCommand { finishRename(keep: false) }
                        .onChange(of: fieldFocused) { _, focused in
                            if !focused && renaming { finishRename(keep: true) }
                        }
                } else {
                    Text(row.name)
                        .font(Theme.sans(Theme.textName))
                        .foregroundStyle(Theme.ink)
                        .lineLimit(1)
                        .truncationMode(.tail)
                        .onTapGesture(count: 2) { startRename() }
                }
                Spacer(minLength: 0)
                RowMark(state: row.state)
            }
            let second = SessionListWords.secondLine(row, Date())
            if !second.isEmpty {
                Text(second)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.muted)
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .padding(.leading, 22)
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .modifier(HoverLayer(radius: 10))
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(chosen ? Theme.raised : Color.clear))
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(chosen ? Theme.inputLine : Color.clear, lineWidth: 1))
        .padding(.vertical, 1)
        .contentShape(Rectangle())
        .onTapGesture { state.onChoose?(row.id) }
        .contextMenu {
            Button(Strings.terminalRenameSession) { startRename() }
            Button(Strings.terminalCloseSession) { state.onClose?(row.id) }
        }
    }

    /// What runs in the session (D-362): the mark of Claude Code or "$".
    @ViewBuilder private var kind: some View {
        if row.kind == .shell {
            Text("$").font(Theme.mono(Theme.textTiny)).foregroundStyle(Theme.muted)
        } else {
            LineMark(data: "M6,1 L6,11 M1,6 L11,6 M2.5,2.5 L9.5,9.5 M9.5,2.5 L2.5,9.5")
        }
    }

    private func startRename() {
        newName = row.name
        renaming = true
        DispatchQueue.main.async { fieldFocused = true }
    }

    /// An empty name changes nothing (TerminalSessions.rename).
    private func finishRename(keep: Bool) {
        guard renaming else { return }
        renaming = false
        if keep && newName != row.name {
            state.onRename?(row.id, newName)
        }
    }
}

/// The state of a row on the right: a mark and a word in the mono font, as Mark() on Windows.
private struct RowMark: View {
    let state: RowState

    var body: some View {
        if let (word, tone) = words {
            HStack(spacing: 6) {
                mark
                Text(word).font(Theme.mono(Theme.textTiny)).foregroundStyle(tone)
            }
            .padding(.leading, 8)
        }
    }

    private var words: (String, Color)? {
        switch state {
        case .working: (Strings.terminalWorking, Theme.muted)
        case .waiting: (Strings.terminalWaiting, Theme.caution)
        case .done: (Strings.terminalDone, Theme.positive)
        case .asleep: (Strings.terminalAsleep, Theme.muted)
        case .stopped: (Strings.terminalStopped, Theme.destructive)
        case .read: nil
        }
    }

    @ViewBuilder private var mark: some View {
        switch state {
        case .working: Circle().fill(Theme.muted).frame(width: 6, height: 6)
        case .asleep: Circle().stroke(Theme.muted, lineWidth: 1).frame(width: 6, height: 6)
        case .waiting: LineMark(data: BarMarks.pause, tone: Theme.caution)
        case .done: FilledMark(data: BarMarks.check, size: 10, tone: Theme.positive)
        case .stopped: LineMark(data: "M0,0 L8,8 M8,0 L0,8", tone: Theme.destructive)
        case .read: EmptyView()
        }
    }
}

/// A soft layer under the mouse, as the hover borders of Windows.
struct HoverLayer: ViewModifier {
    let radius: CGFloat
    @State private var hovered = false

    func body(content: Content) -> some View {
        content
            .background(RoundedRectangle(cornerRadius: radius).fill(Theme.hoverLayer).opacity(hovered ? 1 : 0))
            .onHover { inside in
                withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: 0.12)) { hovered = inside }
            }
    }
}

/// The words of the second line, the twin of SecondLine and UsedWords in TerminalPanel.SessionList.cs.
enum SessionListWords {
    static func secondLine(_ row: SessionRow, _ now: Date) -> String {
        row.line.isEmpty ? usedWords(row.lastUsed, now) : row.line
    }

    static func usedWords(_ lastUsed: Int64, _ now: Date) -> String {
        guard lastUsed > 0 else { return "" }
        let used = Date(timeIntervalSince1970: TimeInterval(lastUsed))
        let offset = TimeZone.current.secondsFromGMT(for: now) / 60
        let locale = Locale(identifier: Strings.language == .russian ? "ru_RU" : "en_US")
        let time = used.formatted(Date.FormatStyle(date: .omitted, time: .shortened).locale(locale))
        switch SessionRows.dayOf(lastUsed, Int64(now.timeIntervalSince1970), offsetMinutes: offset) {
        case .today:
            return time
        case .yesterday:
            return Strings.format(Strings.terminalYesterday, time)
        case .earlier:
            let format = Date.FormatStyle().day().month(.wide).locale(locale)
            return used.formatted(format)
        }
    }
}

/// The list of a narrow window, open under its button over the terminal (D-376), the twin of the
/// pop-in of TerminalPanel.SessionList.cs: the same list in a card, the left edges together as in
/// every drop-down of Aiko. A click outside closes it.
struct ListPopView: View {
    @ObservedObject var state: TerminalState

    /// Where the list button stands in the bar: after the window buttons, as in TitleBarView.
    static let left: CGFloat = TitleBarView.windowButtons + 6

    /// The bottom of the list button: the bar is 44 high and the button 28, in its middle.
    static let top: CGFloat = (TitleBarView.height + 28) / 2

    var body: some View {
        ZStack(alignment: .topLeading) {
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture { state.listPopOpen = false }
            SessionListView(state: state, inCard: true)
                .frame(width: TerminalHost.listWidth + 20)
                .frame(maxHeight: 480)
                .fixedSize(horizontal: false, vertical: true)
                .clipShape(RoundedRectangle(cornerRadius: 12))
                .overlay(RoundedRectangle(cornerRadius: 12).strokeBorder(Theme.inputLine, lineWidth: 1))
                .shadow(color: .black.opacity(Theme.shadowOpacity), radius: Theme.shadowBlur / 2, y: Theme.shadowOffset)
                .padding(.top, Self.top + 4)
                .padding(.leading, Self.left)
                .padding(.bottom, 12)
        }
    }
}

/// The numbers of the Office (office-dnd.ts, desktop-board.tsx), as in RowDrag.cs (D-401).
enum RowDragLook {
    static let start: CGFloat = 6
    static let tilt: Double = -2
    static let pale: Double = 0.3
    static let slide: Double = 0.25
    static let land: Double = 0.18
    static let space = "session rows"
}

/// Where each row stands, by its id.
struct RowFrames: PreferenceKey {
    static let defaultValue: [String: CGRect] = [:]

    static func reduce(value: inout [String: CGRect], nextValue: () -> [String: CGRect]) {
        value.merge(nextValue()) { _, new in new }
    }
}
