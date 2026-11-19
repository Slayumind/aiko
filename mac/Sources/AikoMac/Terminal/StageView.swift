import AikoKit
import SwiftUI

/// What stands in place of the terminal, the twin of ShowMain in TerminalPanel.Palette.cs: nothing
/// when the terminal shows, a message when there is no session or the session has no terminal
/// (D-421), and the question before a running session is closed.
enum StageMessage: Equatable {
    case none
    case noSessions(environment: String)
    case problem(StartProblem, shell: Bool, folder: String)
    case ask(title: String, body: String, stop: String, keep: String)
}

struct StageView: View {
    @ObservedObject var state: TerminalState

    var body: some View {
        switch state.stage {
        case .none:
            Color.clear.allowsHitTesting(false)
        case .noSessions(let environment):
            message(
                Strings.format(Strings.terminalNoSessionsTitle, environment),
                Strings.terminalNoSessionsBody
            ) {
                WindowButton(title: Strings.terminalCreateSession, look: .main, plus: true) { state.onNewInOtherFolder?() }
            }
        case .problem(let problem, let shell, let folder):
            notStarted(problem, shell: shell, folder: folder)
        case .ask(let title, let body, let stop, let keep):
            ask(title, body, stop, keep)
        }
    }

    @ViewBuilder private func notStarted(_ problem: StartProblem, shell: Bool, folder: String) -> some View {
        let again = WindowButton(title: Strings.terminalStartAgain, look: .main) { state.onRetry?() }
        switch problem {
        case .folderGone:
            message(
                Strings.format(Strings.terminalFolderGoneTitle, ShellAsk.folderName(folder)),
                shell ? Strings.terminalFolderGoneShell : Strings.terminalFolderGoneBody
            ) {
                WindowButton(title: Strings.terminalRemoveFromList, look: .outlined) {
                    if let id = state.chosen { state.onClose?(id) }
                }
            }
        case .failed:
            message(shell ? Strings.terminalShellNotStartedTitle : Strings.terminalNotStartedTitle,
                    Strings.terminalNotStartedBody) { again }
        case .noClaude:
            message(Strings.terminalNotStartedTitle, Strings.terminalNotStartedBody) {
                HStack(spacing: 8) {
                    again
                    WindowButton(title: Strings.terminalHowToInstall, look: .outlined) { state.onInstallPage?() }
                }
            }
        }
    }

    /// A title, a few words and a button, at the left of the free room and in its middle.
    private func message<Button: View>(_ title: String, _ body: String, @ViewBuilder button: () -> Button) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(title)
                .font(Theme.mono(Theme.textTitle, weight: .medium))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.bottom, 8)
            Text(body)
                .font(Theme.mono(Theme.textBody))
                .foregroundStyle(Theme.muted)
                .lineSpacing(5)
                .fixedSize(horizontal: false, vertical: true)
            button()
                .padding(.top, 14)
        }
        .frame(maxWidth: 520, alignment: .leading)
        .padding(.horizontal, 40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .background(Theme.background)
    }

    /// The question before a session that still runs is closed: a card at the top over a quieter
    /// window, as Ask() on Windows.
    private func ask(_ title: String, _ body: String, _ stop: String, _ keep: String) -> some View {
        ZStack(alignment: .top) {
            Theme.scrim
            VStack(alignment: .leading, spacing: 0) {
                Text(title)
                    .font(Theme.mono(Theme.textBody, weight: .medium))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.trailing, 28)
                    .padding(.bottom, 6)
                Text(body)
                    .font(Theme.mono(Theme.textBody))
                    .foregroundStyle(Theme.muted)
                    .lineSpacing(4)
                    .fixedSize(horizontal: false, vertical: true)
                HStack(spacing: 8) {
                    WindowButton(title: stop, look: .danger) { state.onAnswer?(true) }
                    WindowButton(title: keep, look: .outlined) { state.onAnswer?(false) }
                }
                .padding(.top, 14)
            }
            .overlay(alignment: .topTrailing) {
                BarButton(tip: "", width: 28, trailing: 0, action: { state.onAnswer?(false) }) {
                    LineMark(data: "M0,0 L8,8 M8,0 L0,8")
                }
                .offset(x: 8, y: -6)
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 16)
            .frame(width: 420)
            .floatingPanel()
            .padding(.top, 28)
            .padding(.horizontal, 20)
        }
    }
}

/// One button for the whole window, the twin of Widgets.Flat: every button is outlined. The main
/// action has the brighter line and medium words, a ghost one has no line, the red one stops
/// something.
struct WindowButton: View {
    enum Look {
        case main
        case outlined
        case ghost
        case danger
    }

    let title: String
    var look = Look.outlined
    var plus = false
    var aside: String?
    let press: () -> Void

    var body: some View {
        Button(action: press) {
            // The gaps of Widgets.Flat: 8 after the plus, 12 before the key at the right end.
            HStack(spacing: 0) {
                if plus {
                    LineMark(data: "M5,0 L5,10 M0,5 L10,5", tone: ink)
                        .padding(.trailing, 8)
                }
                Text(title)
                    .font(Theme.mono(Theme.textBody, weight: look == .main ? .medium : .regular))
                    .foregroundStyle(ink)
                    .lineLimit(1)
                if let aside {
                    Spacer(minLength: 12)
                    Text(aside).font(Theme.mono(Theme.textTiny)).foregroundStyle(Theme.muted)
                }
            }
            .padding(.horizontal, 12)
            .frame(height: 32)
            .modifier(HoverLayer())
            .hairlineFrame(line)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private var ink: Color {
        switch look {
        case .danger: Theme.destructive
        case .ghost: Theme.muted
        case .main, .outlined: Theme.ink
        }
    }

    private var line: Color {
        switch look {
        case .main: Theme.muted
        case .danger: Theme.destructive
        case .outlined: Theme.hairline
        case .ghost: .clear
        }
    }
}
