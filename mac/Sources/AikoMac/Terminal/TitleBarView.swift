import AikoKit
import SwiftUI

/// One tab of the title bar (D-376): the ring and percentage of an environment, and the sessions
/// that want attention. The twin of TabView in TerminalView.cs.
struct TabItem: Equatable {
    let name: String
    let selected: Bool
    let waiting: Int
    let percent: Int?
    let tone: LimitTone
    var done = 0
    var stopped = 0
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
                HStack(spacing: 0) {
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
            BarButton(tip: state.settingsOpen ? Strings.settingsCloseTip : Strings.settings, lit: state.settingsOpen,
                      action: { state.onSettings?() }) {
                FilledMark(data: BarMarks.gear, size: 14)
            }
            .overlay(alignment: .topTrailing) {
                // A quiet dot while a newer version is out (D-391); the General page says which.
                if state.updateDot {
                    Circle().fill(Theme.positive).frame(width: 6, height: 6)
                        .padding(.top, 6).padding(.trailing, 11)
                        .allowsHitTesting(false)
                }
            }
        }
        .padding(.trailing, 8)
        .frame(height: Self.height)
        .background(Theme.background)
        .overlay(alignment: .bottom) { Rule() }
        .animation(Motion.reduceMotion ? nil : .easeOut(duration: 0.2), value: state.headerOpen)
        .animation(Motion.reduceMotion ? nil : .easeOut(duration: 0.2), value: state.environment)
    }

    /// The name and its number are one line of text, so they stand on one baseline; one weight for
    /// every tab, so a tab keeps its width when it is chosen and the plate lands on it. The plate
    /// reaches from the top of the window to the line under the bar, as on Windows. In a narrow
    /// window a tab that is not chosen keeps only its ring and its marks; the name and the limit
    /// move to the tip.
    private func pill(_ tab: TabItem) -> some View {
        let small = state.narrow && !tab.selected
        return Button { state.onEnvironment?(tab.name) } label: {
            HStack(spacing: 0) {
                RingMark(percent: tab.percent, tone: tab.tone)
                if !small {
                    label(tab)
                        .lineLimit(1)
                        .truncationMode(.tail)
                        .padding(.leading, 6)
                }
                TabMark(look: .waiting, count: tab.waiting)
                TabMark(look: .done, count: tab.done)
                TabMark(look: .stopped, count: tab.stopped)
            }
            .padding(.leading, small ? 8 : 10)
            .padding(.trailing, small ? 8 : 12)
            .frame(maxHeight: .infinity)
            .background {
                if tab.selected {
                    Rectangle().fill(Theme.raised).matchedGeometryEffect(id: "chosen", in: pills)
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.trailing, 4)
        .padding(.bottom, 1)
        .tip(small ? tip(tab) : "")
        .layoutPriority(tab.selected ? 1 : 0)
        .animation(Motion.curve(Motion.standard, Motion.expand), value: tab)
    }

    private func label(_ tab: TabItem) -> Text {
        let name = Text(tab.name)
            .font(Theme.mono(Theme.textBody, weight: .medium))
            .foregroundColor(tab.selected ? Theme.ink : Theme.muted)
        if let percent = tab.percent {
            return name + Text(" \(percent)%").font(Theme.mono(Theme.textTiny)).foregroundColor(Theme.muted)
        }
        return name
    }

    private func tip(_ tab: TabItem) -> String {
        tab.percent.map { "\(tab.name) · \($0)%" } ?? tab.name
    }
}

/// One mark of the sessions that want attention on a tab, as TabMarks.cs: a sign and its number.
/// It unfolds when the first such session comes and folds when the last one goes.
private struct TabMark: View {
    let look: StatusLook
    let count: Int

    var body: some View {
        if count > 0 {
            HStack(spacing: 4) {
                StatusSign(look: look).frame(width: 12, height: 12)
                Text("\(count)")
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(Theme.ink)
                    .frame(minWidth: 7, alignment: .leading)
            }
            .padding(.leading, 8)
            .transition(.opacity.combined(with: .scale(scale: 0.6, anchor: .leading)))
        }
    }
}

/// A button of the bar, as BarButton on Windows: the plate under the mouse reaches from the top of
/// the window to the line under the bar, as on the tabs, and stays while what it opens is open.
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
                .frame(width: width)
                .frame(maxHeight: .infinity)
                .background(Theme.raised.opacity(lit || hovered ? 1 : 0))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.trailing, trailing)
        .padding(.bottom, 1)
        .onHover { inside in
            withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: 0.12)) { hovered = inside }
        }
        .tip(tip)
    }
}
