import AikoKit
import AppKit
import SwiftUI

/// What the panel shows and what the person did in it. The host sets the screen and listens to
/// the callbacks; the panel decides nothing.
@MainActor
final class TerminalPanelState: ObservableObject {
    @Published var screen: TerminalScreen?

    // What is typed into the form stays here while the screen is drawn again.
    @Published var name = ""
    @Published var message = ""

    var tabPicked: ((String) -> Void)?
    var rowPicked: ((String) -> Void)?
    var rowClosed: ((String) -> Void)?
    var createAsked: (() -> Void)?
    var folderPicked: ((Int) -> Void)?
    var otherFolderAsked: (() -> Void)?
    var modelPicked: ((Int) -> Void)?

    /// The name and the first message of the form.
    var submitted: ((String, String) -> Void)?
    var formCancelled: (() -> Void)?
    var askAnswered: ((Bool) -> Void)?
    var signInAsked: (() -> Void)?

    func show(_ next: TerminalScreen) {
        let formWasOpen = screen?.form != nil
        if next.form == nil || !formWasOpen {
            name = ""
            message = ""
        }
        screen = next
    }

    func submit() {
        submitted?(name, message)
    }
}

/// The inside of the terminal window (D-292, D-299): the title bar with the tabs of the
/// environments, the list of sessions and the place of the conversation.
///
/// The twin of Terminal/TerminalPanel.cs. The window buttons are the ones of macOS, at the left
/// of the bar, so the bar leaves room for them and has none of its own.
struct TerminalPanelView: View {
    static let barHeight: CGFloat = 44
    static let listWidth: CGFloat = 252

    /// The room the three buttons of the window take at the left of the bar.
    static let windowButtonsWidth: CGFloat = 78

    @ObservedObject var state: TerminalPanelState
    @State private var listOpen = false

    var body: some View {
        GeometryReader { size in
            let narrow = WindowPlace.isNarrow(size.size.width)
            if let screen = state.screen {
                VStack(spacing: 0) {
                    bar(screen, narrow: narrow)
                    Rectangle().fill(Theme.hairline).frame(height: 1)
                    HStack(spacing: 0) {
                        if !narrow {
                            list(screen)
                                .frame(width: Self.listWidth)
                            Rectangle().fill(Theme.hairline).frame(width: 1)
                        }
                        main(screen)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }
                }
            }
        }
        .background(Theme.background)
        .ignoresSafeArea()
    }

    // MARK: - The title bar

    private func bar(_ screen: TerminalScreen, narrow: Bool) -> some View {
        HStack(spacing: 0) {
            Color.clear.frame(width: Self.windowButtonsWidth, height: 1)

            if narrow {
                Button { listOpen.toggle() } label: {
                    HStack(spacing: 8) {
                        Text(screen.sessionName ?? Strings.terminalSessions)
                            .font(Theme.sans(Theme.textNumber))
                            .foregroundStyle(Theme.ink)
                            .lineLimit(1)
                            .frame(maxWidth: 160, alignment: .leading)
                        Image(systemName: "chevron.down")
                            .font(.system(size: 9, weight: .medium))
                            .foregroundStyle(Theme.muted)
                    }
                    .padding(.horizontal, 12)
                    .frame(height: 30)
                    .overlay(
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
                            .strokeBorder(Theme.inputLine, lineWidth: 1))
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .padding(.trailing, 10)
                .popover(isPresented: $listOpen, arrowEdge: .bottom) {
                    list(screen)
                        .frame(width: Self.listWidth + 20, height: 480)
                        .background(Theme.background)
                }
            }

            ForEach(screen.tabs, id: \.name) { tab in
                TerminalTabButton(tab: tab, narrow: narrow) { state.tabPicked?(tab.name) }
            }

            Spacer(minLength: 8)

            if let badge = screen.badge {
                BadgeMark(badge: badge)
                    .padding(.trailing, 14)
            }
        }
        .frame(height: Self.barHeight)
        .background(WindowDragArea())
    }

    // MARK: - The list of sessions

    private func list(_ screen: TerminalScreen) -> some View {
        VStack(spacing: 0) {
            ScrollView(.vertical) {
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(screen.groups, id: \.folder) { group in
                        Text(group.name)
                            .font(Theme.mono(Theme.textTiny))
                            .foregroundStyle(Theme.muted)
                            .padding(.horizontal, 10)
                            .padding(.top, 10)
                            .padding(.bottom, 4)

                        ForEach(group.rows, id: \.id) { row in
                            SessionRowView(
                                row: row,
                                selected: row.id == screen.sessionId && screen.main == .talk,
                                now: screen.now,
                                pick: {
                                    listOpen = false
                                    state.rowPicked?(row.id)
                                },
                                close: { state.rowClosed?(row.id) })
                        }
                    }
                }
                .padding(.horizontal, 8)
                .padding(.top, 4)
                .frame(maxWidth: .infinity, alignment: .leading)
            }

            FlatButton(title: Strings.terminalCreateSession, look: .ghost, symbol: "plus") {
                listOpen = false
                state.createAsked?()
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(8)
        }
    }

    // MARK: - The place of the conversation

    @ViewBuilder
    private func main(_ screen: TerminalScreen) -> some View {
        ZStack {
            switch screen.main {
            case .form:
                if let form = screen.form {
                    SessionFormView(form: form, state: state)
                }
            case .talk where screen.signedOut:
                MessageView(
                    title: Strings.format(Strings.terminalLoginTitle, screen.environment),
                    text: Strings.terminalLoginBody,
                    button: (Strings.signIn, false, { state.signInAsked?() }))
            case .talk:
                MessageView(title: nil, text: Strings.terminalEmptyTalk, button: nil)
            default:
                MessageView(
                    title: Strings.format(Strings.terminalNoSessionsTitle, screen.environment),
                    text: Strings.terminalNoSessionsBody,
                    button: (Strings.terminalCreateSession, true, { state.createAsked?() }))
            }

            if let ask = screen.ask {
                AskCover(ask: ask, answer: { state.askAnswered?($0) })
            }
        }
    }
}

// MARK: - The parts of the bar

private struct TerminalTabButton: View {
    let tab: TerminalTab
    let narrow: Bool
    let pick: () -> Void

    var body: some View {
        Button(action: pick) {
            HStack(spacing: 0) {
                Text(tab.name)
                    .font(Theme.sans(Theme.textNumber, weight: tab.selected ? .medium : .regular))
                    .foregroundStyle(tab.selected ? Theme.ink : Theme.muted)
                    .lineLimit(1)

                if tab.waiting > 0 {
                    PauseMark()
                        .padding(.leading, 8)
                        .padding(.trailing, 5)
                    Text(narrow ? "\(tab.waiting)" : Self.waitingWords(tab.waiting))
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.caution)
                }
            }
            .padding(.horizontal, 12)
            .frame(height: 28)
            .background(tab.selected ? Theme.raised : .clear)
            .clipShape(Capsule())
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .padding(.trailing, 4)
    }

    private static func waitingWords(_ count: Int) -> String {
        Strings.format(
            count % 10 == 1 && count % 100 != 11 ? Strings.terminalTabWaitingOne : Strings.terminalTabWaitingMany,
            count)
    }
}

/// The ring and the number of the limit that is closer to its end.
private struct BadgeMark: View {
    let badge: TerminalBadge

    var body: some View {
        HStack(spacing: 6) {
            ZStack {
                Circle().stroke(Theme.raised, lineWidth: 2)
                Circle()
                    .trim(from: 0, to: min(max(Double(badge.percent) / 100, 0), 0.999))
                    .stroke(Theme.tone(badge.tone), lineWidth: 2)
                    .rotationEffect(.degrees(-90))
            }
            .frame(width: 12, height: 12)

            Text("\(badge.percent)%")
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
        }
        .help(badge.kind == .fiveHour ? Strings.terminalLimitFiveHour : Strings.terminalLimitWeek)
    }
}

/// The sign of "waits for you": a pause (D-299).
private struct PauseMark: View {
    var body: some View {
        HStack(spacing: 3.7) {
            Capsule().fill(Theme.caution).frame(width: 1.3, height: 11)
            Capsule().fill(Theme.caution).frame(width: 1.3, height: 11)
        }
    }
}

// MARK: - A row of the list

private struct SessionRowView: View {
    let row: SessionRow
    let selected: Bool
    let now: Date
    let pick: () -> Void
    let close: () -> Void

    @State private var hovering = false

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 8) {
                Text(row.name)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)
                Spacer(minLength: 0)
                StateMark(state: row.state)
            }

            let second = Self.secondLine(row, now)
            if !second.isEmpty {
                Text(second)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.muted)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(selected ? Theme.surface : (hovering ? Theme.hoverLayer : .clear))
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(selected ? Theme.inputLine : .clear, lineWidth: 1))
        .contentShape(Rectangle())
        .padding(.vertical, 1)
        .onHover { hovering = $0 }
        .onTapGesture(perform: pick)
        .contextMenu {
            Button(Strings.terminalCloseSession, action: close)
        }
    }

    private static func secondLine(_ row: SessionRow, _ now: Date) -> String {
        guard let glance = row.glance else {
            return usedWords(row.lastUsed, now)
        }

        switch glance.line {
        case .request:
            return Strings.format(Strings.terminalLineRequest, stepWord(glance), glance.text)
                .trimmingCharacters(in: .whitespaces)
        case .question:
            return Strings.format(Strings.terminalLineQuestion, glance.text)
        case .plan:
            return Strings.terminalLinePlan
        case .step:
            let format = switch glance.kind {
            case .read: Strings.terminalLineReading
            case .search: Strings.terminalLineSearching
            case .edit: Strings.terminalLineEditing
            case .run: Strings.terminalLineRunning
            case .agent: Strings.terminalLineAgent
            case .web: Strings.terminalLineOpening
            case .other: Strings.terminalLineCalling
            }
            return Strings.format(format, glance.kind == .other ? glance.label : glance.text)
        case .answer:
            return glance.text
        case .stoppedAt:
            return Strings.format(Strings.terminalLineStoppedAt, stepWord(glance))
        case .signIn:
            return Strings.terminalLineSignIn
        case .none:
            return ""
        }
    }

    /// The word of a step, the one the feed uses for it.
    private static func stepWord(_ glance: TalkGlance) -> String {
        switch glance.kind {
        case .read: Strings.terminalStepRead
        case .search: Strings.terminalStepSearch
        case .edit: Strings.terminalStepEdit
        case .run: Strings.terminalStepRun
        case .agent: Strings.terminalStepAgent
        case .web: Strings.terminalStepWeb
        case .other: glance.label
        }
    }

    private static func usedWords(_ lastUsed: Int64, _ now: Date) -> String {
        guard lastUsed > 0 else { return "" }

        let used = Date(timeIntervalSince1970: TimeInterval(lastUsed))
        let offset = TimeZone.current.secondsFromGMT(for: now) / 60
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: Strings.language == .russian ? "ru_RU" : "en_US")

        switch SessionRows.dayOf(lastUsed, Int64(now.timeIntervalSince1970), offsetMinutes: offset) {
        case .today:
            formatter.dateFormat = "HH:mm"
            return formatter.string(from: used)
        case .yesterday:
            formatter.dateFormat = "HH:mm"
            return Strings.format(Strings.terminalYesterday, formatter.string(from: used))
        case .earlier:
            formatter.dateFormat = "d MMMM"
            return formatter.string(from: used)
        }
    }
}

private struct StateMark: View {
    let state: RowState

    var body: some View {
        if let mark = Self.word(state) {
            HStack(spacing: 6) {
                icon
                Text(mark.word)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(mark.tone)
            }
        }
    }

    @ViewBuilder
    private var icon: some View {
        switch state {
        case .working:
            Circle().fill(Theme.muted).frame(width: 6, height: 6)
        case .waiting:
            PauseMark()
        case .done:
            Image(systemName: "checkmark")
                .font(.system(size: 9, weight: .semibold))
                .foregroundStyle(Theme.positive)
        case .asleep:
            Circle().strokeBorder(Theme.muted, lineWidth: 1).frame(width: 6, height: 6)
        case .stopped:
            Image(systemName: "xmark")
                .font(.system(size: 8, weight: .semibold))
                .foregroundStyle(Theme.destructive)
        default:
            EmptyView()
        }
    }

    private static func word(_ state: RowState) -> (word: String, tone: Color)? {
        switch state {
        case .working: (Strings.terminalWorking, Theme.muted)
        case .waiting: (Strings.terminalWaiting, Theme.caution)
        case .done: (Strings.terminalDone, Theme.positive)
        case .asleep: (Strings.terminalAsleep, Theme.muted)
        case .stopped: (Strings.terminalStopped, Theme.destructive)
        default: nil
        }
    }
}

// MARK: - The place of the conversation

private struct MessageView: View {
    let title: String?
    let text: String

    /// The words of the button, whether it makes something new, and what it does.
    let button: (words: String, plus: Bool, press: () -> Void)?

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if let title {
                Text(title)
                    .font(Theme.sans(16, weight: .medium))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.bottom, 8)
            }

            Text(text)
                .font(Theme.sans(Theme.textName))
                .foregroundStyle(Theme.muted)
                .lineSpacing(4)
                .fixedSize(horizontal: false, vertical: true)

            if let button {
                FlatButton(title: button.words, look: .primary, symbol: button.plus ? "plus" : nil, press: button.press)
                    .padding(.top, 14)
            }
        }
        .frame(maxWidth: 520, alignment: .leading)
        .padding(.horizontal, 40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }
}

private struct SessionFormView: View {
    let form: SessionForm
    @ObservedObject var state: TerminalPanelState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(Strings.format(Strings.terminalFormTitle, form.environment))
                .font(Theme.sans(16, weight: .medium))
                .foregroundStyle(Theme.ink)
                .padding(.bottom, 18)

            field(Strings.terminalFolder) {
                DropDown(
                    items: form.folders.map { "\($0.name)   \($0.path)" } + [Strings.terminalChooseFolder],
                    index: form.folder,
                    headers: Dictionary(uniqueKeysWithValues: NewSessionChoices.headers(form.folders).map {
                        ($0.before, $0.group == .recent ? Strings.terminalFoldersRecent : Strings.terminalFoldersBound)
                    })
                ) { index in
                    if index < form.folders.count {
                        state.folderPicked?(index)
                    } else {
                        state.otherFolderAsked?()
                    }
                }
            }

            if let other = form.boundElsewhere {
                hint(Strings.format(Strings.terminalElsewhere, other, form.environment))
            }

            field(Strings.terminalModel) {
                DropDown(items: NewSessionChoices.models.map(Self.modelWord), index: form.model) {
                    state.modelPicked?($0)
                }
                .frame(width: 180)
            }

            field(Strings.terminalName) {
                NameField(text: $state.name, size: Theme.textName, placeholder: form.defaultName, onCommit: {})
                    .onSubmit { state.submit() }
            }
            hint(Strings.terminalNameHint)

            MessageField(text: $state.message, placeholder: Strings.terminalDescribeTask) { state.submit() }
                .frame(minHeight: 64)

            FlatButton(title: Strings.terminalCreateSession, look: .primary) { state.submit() }
                .padding(.top, 12)
        }
        .frame(maxWidth: 620, alignment: .leading)
        .padding(.horizontal, 40)
        .padding(.top, 36)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .onExitCommand { state.formCancelled?() }
    }

    private func field<Content: View>(_ label: String, @ViewBuilder content: () -> Content) -> some View {
        HStack(alignment: .top, spacing: 0) {
            Text(label)
                .font(Theme.sans(Theme.textNumber))
                .foregroundStyle(Theme.muted)
                .frame(width: 96, alignment: .leading)
                .padding(.top, 6)
            content()
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.bottom, 12)
    }

    private func hint(_ text: String) -> some View {
        Text(text)
            .font(Theme.sans(Theme.textSmall))
            .foregroundStyle(Theme.muted)
            .fixedSize(horizontal: false, vertical: true)
            .padding(.leading, 96)
            .padding(.top, -6)
            .padding(.bottom, 12)
    }

    private static func modelWord(_ model: String) -> String {
        model.prefix(1).uppercased() + model.dropFirst()
    }
}

/// The first message of a new session. Return starts the session, Shift+Return is a new line
/// (D-294).
private struct MessageField: View {
    @Binding var text: String
    let placeholder: String
    let submit: () -> Void

    @FocusState private var focused: Bool

    var body: some View {
        TextField(placeholder, text: $text, axis: .vertical)
            .textFieldStyle(.plain)
            .font(Theme.sans(Theme.textName))
            .foregroundStyle(Theme.ink)
            .lineLimit(3...10)
            .focused($focused)
            .onKeyPress(.return, phases: .down) { press in
                if press.modifiers.contains(.shift) {
                    text += "\n"
                    return .handled
                }
                submit()
                return .handled
            }
            .padding(.horizontal, 9)
            .padding(.vertical, 7)
            .frame(maxWidth: .infinity, alignment: .topLeading)
            .background(Theme.surface)
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(Theme.inputLine, lineWidth: 1))
    }
}

/// Close something that still works? What is under it stays, only quieter.
private struct AskCover: View {
    let ask: CloseAsk
    let answer: (Bool) -> Void

    var body: some View {
        ZStack(alignment: .top) {
            Color(.sRGB, red: 0x0A / 255, green: 0x0A / 255, blue: 0x0A / 255, opacity: 0.6)
                .contentShape(Rectangle())
                .onTapGesture {}

            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .top) {
                    Text(ask.title)
                        .font(Theme.sans(Theme.textName, weight: .medium))
                        .foregroundStyle(Theme.ink)
                        .fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: 8)
                    MarkButton(help: Strings.terminalKeepOpen, press: { answer(false) }) {
                        Image(systemName: "xmark")
                            .font(.system(size: 10, weight: .medium))
                            .foregroundStyle(Theme.muted)
                    }
                    .padding(.top, -6)
                    .padding(.trailing, -8)
                }
                .padding(.bottom, 6)

                Text(ask.body)
                    .font(Theme.sans(Theme.textNumber))
                    .foregroundStyle(Theme.muted)
                    .lineSpacing(3)
                    .fixedSize(horizontal: false, vertical: true)

                HStack(spacing: 8) {
                    FlatButton(title: Strings.terminalStopAndClose, look: .danger) { answer(true) }
                    FlatButton(title: Strings.terminalKeepOpen, look: .plain) { answer(false) }
                }
                .padding(.top, 14)
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 16)
            .frame(width: 420)
            .background(Theme.surface)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .strokeBorder(Theme.inputLine, lineWidth: 1))
            .shadow(color: .black.opacity(Theme.shadowOpacity), radius: Theme.shadowBlur / 2, y: Theme.shadowOffset)
            .padding(.top, 28)
        }
    }
}
