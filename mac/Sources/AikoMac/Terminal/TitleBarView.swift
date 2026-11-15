import AikoKit
import SwiftUI

/// One pill of the title bar (D-376): the ring and percentage of an environment, or the waiting
/// mark and the number of its waiting sessions. The twin of TabView in TerminalView.cs.
struct TabItem: Equatable {
    let name: String
    let selected: Bool
    let waiting: Int
    let percent: Int?
    let tone: LimitTone
}

/// The title bar of the terminal window, the twin of TerminalPanel.TitleBar.cs: the list button,
/// the chevron of the header, the pills of the environments, the search and the settings. The
/// window buttons of the Mac stand where Windows has its own three, on the other side.
struct TitleBarView: View {
    @ObservedObject var state: TerminalState

    /// The height of the bar, as TerminalPanel.BarHeight.
    static let height: CGFloat = 44

    /// Room for the three window buttons of the Mac on the left.
    static let windowButtons: CGFloat = 76

    @Namespace private var pills

    var body: some View {
        HStack(spacing: 0) {
            Color.clear.frame(width: Self.windowButtons)
            BarButton(tip: Strings.terminalSessionList, trailing: 2, action: { state.onToggleList?() }) {
                LineMark(data: BarMarks.list)
            }
            .padding(.leading, 6)
            BarButton(tip: state.headerOpen ? Strings.terminalHideLimits : Strings.terminalShowLimits,
                      width: 28, action: { state.onToggleHeader?() }) {
                LineMark(data: BarMarks.chevron)
                    .rotationEffect(.degrees(state.headerOpen ? 180 : 0))
            }
            if !state.headerOpen {
                HStack(spacing: 4) {
                    ForEach(state.tabs, id: \.name) { tab in
                        pill(tab)
                    }
                }
                .transition(.opacity)
            }
            Spacer(minLength: 8)
            BarButton(tip: Strings.terminalFindConversation, action: { state.onSearch?() }) {
                LineMark(data: BarMarks.search)
            }
            BarButton(tip: Strings.settings, lit: state.settingsOpen, action: { state.onSettings?() }) {
                FilledMark(data: BarMarks.gear, size: 14)
            }
        }
        .padding(.trailing, 8)
        .frame(height: Self.height)
        .background(Theme.background)
        .overlay(alignment: .bottom) {
            Rectangle().fill(Theme.hairline).frame(height: 1)
        }
        .animation(Motion.reduceMotion ? nil : .easeOut(duration: 0.2), value: state.headerOpen)
        .animation(Motion.reduceMotion ? nil : .easeOut(duration: 0.2), value: state.environment)
    }

    /// The name and its number are one line of text, so they stand on one baseline; one weight for
    /// every pill, so a pill keeps its width when it is chosen and the background lands on it.
    private func pill(_ tab: TabItem) -> some View {
        Button { state.onEnvironment?(tab.name) } label: {
            HStack(spacing: 6) {
                if tab.waiting > 0 {
                    LineMark(data: BarMarks.pause, tone: Theme.caution)
                        .frame(width: 14, height: 14)
                } else {
                    RingMark(percent: tab.percent, tone: tab.tone)
                }
                label(tab)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
            .padding(.leading, 10)
            .padding(.trailing, 12)
            .frame(height: 28)
            .background {
                if tab.selected {
                    Capsule().fill(Theme.raised).matchedGeometryEffect(id: "chosen", in: pills)
                }
            }
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .help(tab.waiting > 0 ? tip(tab) : "")
        .layoutPriority(tab.selected ? 1 : 0)
    }

    private func label(_ tab: TabItem) -> Text {
        let name = Text(tab.name)
            .font(Theme.sans(Theme.textNumber, weight: .medium))
            .foregroundColor(tab.selected ? Theme.ink : Theme.muted)
        if tab.waiting > 0 {
            return name + number("\(tab.waiting)", Theme.caution)
        }
        if let percent = tab.percent {
            return name + number("\(percent)%", Theme.muted)
        }
        return name
    }

    private func number(_ text: String, _ tone: Color) -> Text {
        Text(" " + text).font(Theme.mono(Theme.textTiny)).foregroundColor(tone)
    }

    private func tip(_ tab: TabItem) -> String {
        tab.percent.map { "\(tab.name) · \($0)%" } ?? tab.name
    }
}

/// A button of the bar, as BarButton on Windows: 28 points high, a soft layer under the mouse, and
/// the layer kept while what it opens is open.
struct BarButton<Mark: View>: View {
    let tip: String
    var width: CGFloat = 40
    var lit = false
    var trailing: CGFloat = 4
    let action: () -> Void
    @ViewBuilder let mark: () -> Mark

    @State private var hovered = false

    var body: some View {
        Button(action: action) {
            mark()
                .frame(width: width, height: 28)
                .background(
                    RoundedRectangle(cornerRadius: 6)
                        .fill(Theme.raised)
                        .opacity(lit || hovered ? 1 : 0))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.trailing, trailing)
        .onHover { inside in
            withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: 0.12)) { hovered = inside }
        }
        .help(tip)
    }
}
