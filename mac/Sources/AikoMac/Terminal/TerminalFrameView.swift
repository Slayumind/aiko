import AikoKit
import AppKit
import SwiftUI

/// The thin frame around the terminal, the twin of _terminalFrame in TerminalPanel.Signal.cs: the
/// name of the session in a gap of the top line, and at its right end the state of the session, or
/// the limit when it is near its end (D-452). The terminal itself is AppKit and stands in the room
/// the host leaves inside this frame.
struct TerminalFrameView: View {
    @ObservedObject var state: TerminalState

    /// The padding inside the frame above the terminal, as Padding.Top of the Windows frame.
    private static let paddingTop: CGFloat = 4

    /// How far the terminal stands below the top of the frame: the height of the labels in the line
    /// and the padding under them. The labels are one line of TextBody.
    static func top(zoom: CGFloat) -> CGFloat {
        let font = Theme.monoFont(Theme.textBody * zoom)
        return (font.ascender - font.descender + font.leading).rounded(.up) + paddingTop * zoom
    }

    var body: some View {
        if shown, let row {
            TitledFrame(padding: EdgeInsets()) {
                Text(row.name)
                    .font(Theme.mono(Theme.textBody))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .frame(maxWidth: 360, alignment: .leading)
            } aside: {
                aside(row)
            } content: {
                Color.clear
            }
            .allowsHitTesting(false)
        }
    }

    /// The frame stands only around a terminal: a message in place of it has none.
    private var shown: Bool { state.stage == .none }

    private var row: SessionRow? {
        state.groups.lazy.flatMap(\.rows).first { $0.id == state.chosen }
    }

    @ViewBuilder private func aside(_ row: SessionRow) -> some View {
        let tab = state.tabs.first(where: \.selected)
        let signal = WindowSignals.of(state.groups.flatMap { $0.rows.map(\.state) }, limit: tab?.tone ?? .unknown)
        if signal == .limitNearEnd && row.state != .waiting {
            StatusMark(look: .limitNearEnd, word: Strings.format(Strings.terminalLimitMark, tab?.percent ?? 0))
        } else if let look = StatusLook(row.state) {
            StatusMark(look: look)
        }
    }
}
