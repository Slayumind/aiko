import AikoKit
import SwiftUI

/// One column of the open header: the active one carries the words of its block and the profile
/// line; a quiet one only shows its two numbers. The twin of HeaderColumnView in TerminalView.cs.
struct HeaderColumnItem: Equatable {
    let column: HeaderColumn
    let block: EnvironmentBlock?
    let direct: Bool
}

/// The header under the title bar (D-373, D-376), the twin of HeaderStripView.cs: the active
/// environment first in a card with every limit, the others as quiet cards that pick their
/// environment on a click. The cards are the segments of one switch: the active one lies on a soft
/// fill, a quiet one has none and lights up under the pointer (D-392, D-393).
struct HeaderStripView: View {
    @ObservedObject var state: TerminalState

    static let quietWidth: CGFloat = 168
    static let activeWidth: CGFloat = 480
    static let activeMinWidth: CGFloat = 300

    /// Under this width the active card names its limits short: "5 h" for "5 hours".
    static let compactBelow: CGFloat = 440
    static let gap: CGFloat = 12
    static let rowHeight: CGFloat = 24

    /// Room for the most limit rows an environment has, so every environment gives the header the
    /// same height: a switch never moves the terminal under it.
    static let mostRows = 3

    var body: some View {
        GeometryReader { room in
            let quiet = max(0, state.header.count - 1)
            let active = Self.activeWidthFor(room.size.width - 24, quietCards: quiet)
            HStack(alignment: .top, spacing: Self.gap) {
                ForEach(state.header, id: \.column.environment) { item in
                    HeaderCard(item: item, width: item.column.active ? active : Self.quietWidth, state: state)
                }
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 10)
            .animation(Motion.reduceMotion ? nil : .easeInOut(duration: 0.38), value: state.environment)
        }
        .frame(height: Self.height)
        .background(Theme.background)
        .overlay(alignment: .bottom) {
            Rectangle().fill(Theme.hairline).frame(height: 1)
        }
    }

    /// The height of the strip: the active card with its head, room for every row and the
    /// profile, its edge, and the padding of the strip.
    static let height: CGFloat = {
        let rows: CGFloat = rowHeight * CGFloat(mostRows)
        let card: CGFloat = 12 + 20 + 4 + rows + 8 + 16 + 12
        let edge: CGFloat = 2
        let padding: CGFloat = 20
        return card + edge + padding
    }()

    /// As wide as it wants when there is room, narrower in a narrow window, never under the minimum.
    static func activeWidthFor(_ room: CGFloat, quietCards: Int) -> CGFloat {
        min(max(room - CGFloat(quietCards) * (quietWidth + gap), activeMinWidth), activeWidth)
    }

    static func waitingWords(_ count: Int) -> String {
        Strings.format(
            count % 10 == 1 && count % 100 != 11 ? Strings.terminalTabWaitingOne : Strings.terminalTabWaitingMany,
            count)
    }
}

/// One card of the header. A quiet card is a button: a soft fill under the pointer, and a click
/// makes its environment the active one. The active card is not.
private struct HeaderCard: View {
    let item: HeaderColumnItem
    let width: CGFloat
    @ObservedObject var state: TerminalState

    @State private var hovered = false
    @State private var pressed = false

    private var active: Bool { item.column.active }

    var body: some View {
        content
            .padding(12)
            .frame(width: width, alignment: .topLeading)
            .background(
                RoundedRectangle(cornerRadius: 14)
                    .fill(active ? Theme.surface : (hovered ? Theme.hoverLayer : Color.clear)))
            .padding(1)
            .scaleEffect(pressed ? Motion.pressedScale : 1)
            .contentShape(RoundedRectangle(cornerRadius: 14))
            .onHover { inside in
                withAnimation(Motion.reduceMotion ? nil : .easeOut(duration: 0.12)) { hovered = inside && !active }
            }
            .onTapGesture {
                if !active { state.onEnvironment?(item.column.environment) }
            }
            .simultaneousGesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { _ in if !active && !pressed { withAnimation(.easeOut(duration: 0.08)) { pressed = true } } }
                    .onEnded { _ in withAnimation(.spring(duration: 0.25)) { pressed = false } })
            .tip(active ? "" : item.column.environment)
    }

    @ViewBuilder private var content: some View {
        if active {
            full
        } else {
            quiet
        }
    }

    // ---- the active card: the head, one line per limit, and the profile ----

    private var full: some View {
        VStack(alignment: .leading, spacing: 0) {
            head
            if let block = item.block {
                VStack(alignment: .leading, spacing: 0) {
                    if block.rows.isEmpty {
                        Text(block.note)
                            .font(Theme.sans(Theme.textSmall))
                            .foregroundStyle(Theme.muted)
                            .fixedSize(horizontal: false, vertical: true)
                            .padding(.top, 4)
                    }
                    // The names, the times and the tone words share their widths, so the bars line up.
                    Grid(alignment: .leading, horizontalSpacing: 0, verticalSpacing: 0) {
                        ForEach(Array(block.rows.enumerated()), id: \.offset) { _, row in
                            LimitLine(row: row, compact: width < HeaderStripView.compactBelow)
                        }
                    }
                }
                .frame(minHeight: HeaderStripView.rowHeight * CGFloat(HeaderStripView.mostRows), alignment: .top)
                .padding(.top, 4)
                profile(block)
            }
        }
    }

    /// Ring, name, "N waiting", and at the right end "working now" when the environment works, or
    /// "Sign in" when its account is not connected.
    private var head: some View {
        HStack(spacing: 0) {
            RingMark(percent: item.column.fiveHour, tone: active ? tone : .unknown)
                .opacity(active ? 1 : 0.6)
                .padding(.trailing, 8)
            Text(item.column.environment)
                .font(Theme.sans(Theme.textNumber, weight: active ? .medium : .regular))
                .foregroundStyle(active ? Theme.ink : Theme.muted)
                .lineLimit(1)
                .truncationMode(.tail)
            if active && item.column.waiting > 0 {
                Text(HeaderStripView.waitingWords(item.column.waiting))
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.caution)
                    .padding(.leading, 8)
            }
            Spacer(minLength: 0)
            if let block = item.block, active {
                if block.showsHalo {
                    HStack(spacing: 4) {
                        ZStack {
                            Circle().fill(Theme.positive.opacity(0.18)).frame(width: 12, height: 12)
                            Circle().fill(Theme.positive).frame(width: 6, height: 6)
                        }
                        Text(block.state)
                            .font(Theme.sans(Theme.textTiny))
                            .foregroundStyle(block.stateIsBright ? Theme.ink : Theme.muted)
                    }
                    .padding(.leading, 12)
                }
                if block.needsSignIn {
                    Text(Strings.signIn)
                        .font(Theme.sans(Theme.textTiny))
                        .foregroundStyle(Theme.ink)
                        .underline()
                        .tip(Strings.stateSignInNeeded)
                        .onTapGesture { state.onSignIn?(item.column.environment) }
                        .padding(.leading, 12)
                }
            }
        }
        .frame(height: 20)
    }

    /// "Max 5x · Aiko · direct mode off · updated 12:04": who this environment is, on one line.
    private func profile(_ block: EnvironmentBlock) -> some View {
        var parts: [String] = []
        if !block.plan.isEmpty { parts.append(block.plan) }
        parts.append(item.column.environment)
        parts.append(item.direct ? Strings.terminalDirectOn : Strings.terminalDirectOff)
        if !block.rows.isEmpty { parts.append(block.updated) }

        return Text(parts.joined(separator: "  ·  "))
            .font(Theme.sans(Theme.textSmall))
            .foregroundStyle(Theme.muted)
            .lineLimit(1)
            .truncationMode(.tail)
            .padding(.top, 8)
    }

    // ---- the quiet card: a dim head, two small bars, and "N waiting" ----

    private var quiet: some View {
        VStack(alignment: .leading, spacing: 0) {
            head
            Grid(alignment: .leading, horizontalSpacing: 0, verticalSpacing: 0) {
                MiniLine(label: Strings.cardShortSession, percent: item.column.fiveHour)
                MiniLine(label: Strings.cardShortWeek, percent: item.column.sevenDay)
            }
            .padding(.top, 8)
            if item.column.waiting > 0 {
                Text(HeaderStripView.waitingWords(item.column.waiting))
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.caution)
                    .padding(.top, 8)
            }
        }
    }

    private var tone: LimitTone {
        item.column.fiveHour.map { CardState.toneFor($0, .live) } ?? .unknown
    }
}

/// "5 hours ━━━━━━──── 42%  1h 6m  running low": one line of 24 points per limit.
private struct LimitLine: View {
    let row: LimitRow
    let compact: Bool

    @State private var grown = false

    var body: some View {
        GridRow {
            Text(compact ? row.shortName : row.name)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.muted)
                .lineLimit(1)
                .padding(.trailing, 12)
            // The bar grows from the left edge when the header opens. The height of the line lives
            // on a cell: a modifier on the row itself would make it one cell of the grid.
            Bar(fill: grown ? row.fill * 100 : 0, height: 4, tone: Theme.tone(row.tone), toneOpacity: 1)
                .frame(height: HeaderStripView.rowHeight)
                .padding(.trailing, 8)
                .onAppear {
                    guard !Motion.reduceMotion else {
                        grown = true
                        return
                    }
                    withAnimation(.easeOut(duration: 0.48).delay(0.12)) { grown = true }
                }
            Text(row.percent)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.ink)
                .frame(width: 40, alignment: .trailing)
            Text(row.countdown)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .lineLimit(1)
                .frame(minWidth: 64, alignment: .leading)
                .padding(.leading, 12)
            // Only the word of a tone ("running low"), in its colour; a guess about the pace is the
            // card's detail.
            Text(row.paceTakesTone ? row.pace : "")
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.tone(row.tone))
                .lineLimit(1)
                .frame(minWidth: 72, alignment: .leading)
                .padding(.leading, 8)
        }
    }
}

/// "5 h ━━── 82%" in a quiet card: a thin dim bar, nothing coloured.
private struct MiniLine: View {
    let label: String
    let percent: Int?

    var body: some View {
        GridRow {
            Text(label)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .padding(.trailing, 8)
            Bar(fill: Double(min(max(percent ?? 0, 0), 100)), height: 2, tone: Theme.muted, toneOpacity: 0.5)
                .frame(height: 16)
                .padding(.trailing, 4)
            Text(percent.map { "\($0)%" } ?? "—")
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .frame(width: 32, alignment: .trailing)
        }
    }
}

/// A bar: a grey track and the used part from the left, fill from 0 to 100.
private struct Bar: View {
    let fill: Double
    let height: CGFloat
    let tone: Color
    let toneOpacity: Double

    var body: some View {
        GeometryReader { box in
            ZStack(alignment: .leading) {
                Capsule().fill(Theme.raised)
                Capsule().fill(tone).opacity(toneOpacity)
                    .frame(width: box.size.width * CGFloat(fill / 100))
            }
        }
        .frame(height: height)
    }
}
