import AikoKit
import AppKit
import SwiftUI

/// The left side of the terminal window, the twin of TerminalPanel.SessionList.cs: the sessions of
/// the chosen environment by folder. Each folder shows three quiet buttons under the mouse: Claude
/// Code and a command line in it with no question (D-350), and the folder in Finder (D-408). A row
/// says what runs in it, its name and its state, and under them what it did last or when it was used.
struct SessionListView: View {
    @ObservedObject var state: TerminalState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 0) {
                    ForEach(state.groups, id: \.folder) { group in
                        GroupHeader(group: group, state: state)
                        ForEach(group.rows, id: \.id) { row in
                            SessionRowView(row: row, chosen: row.id == state.chosen && !state.settingsOpen, state: state)
                        }
                    }
                }
                .padding(.horizontal, 8)
                .padding(.top, 4)
            }
            newSession
        }
        .frame(maxHeight: .infinity, alignment: .top)
        .background(Theme.surface)
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
