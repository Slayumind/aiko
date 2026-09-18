import AikoKit
import SwiftUI

/// The card itself: the twin of Card/CardPanel.xaml on Windows, block for block and number for
/// number. Every word comes from CardModel, so this file only places things.
struct CardView: View {
    let model: CardModel
    let onSettings: () -> Void
    let onClose: () -> Void
    let onOpenClaude: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header

            ForEach(Array(model.blocks.enumerated()), id: \.offset) { _, block in
                EnvironmentBlockView(block: block, onOpenClaude: onOpenClaude)
                    .padding(.top, Theme.blockGap)
            }
        }
        .padding(Theme.cardPadding)
        .frame(width: Theme.cardWidth, alignment: .leading)
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .shadow(
            color: .black.opacity(Theme.shadowOpacity),
            radius: Theme.shadowBlur / 2,
            x: 0,
            y: Theme.shadowOffset)
        .padding(Theme.shadowRoom)
    }

    /// The header is the strip the card is dragged by, as on Windows.
    private var header: some View {
        HStack(spacing: 0) {
            Text(model.updated)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)

            Spacer(minLength: 8)

            IconButton(help: Strings.settings, action: onSettings) { GearShape() }
            IconButton(help: Strings.close, action: onClose) { CrossShape() }
        }
        .frame(height: 22)
        .background(WindowDragArea())
    }
}

private struct EnvironmentBlockView: View {
    let block: EnvironmentBlock
    let onOpenClaude: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if block.showsSeparator {
                Rectangle()
                    .fill(Theme.hairline)
                    .frame(height: 1)
                    .padding(.bottom, Theme.blockGap)
            }

            HStack(spacing: 0) {
                Text(block.name)
                    .font(Theme.sans(Theme.textName, weight: .medium))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)

                if block.showsPlan {
                    Text(block.plan)
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.ink)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 1)
                        .background(Color.white.opacity(0.07))
                        .clipShape(RoundedRectangle(cornerRadius: 5, style: .continuous))
                        .padding(.horizontal, 8)
                }

                Spacer(minLength: 8)

                if block.showsState {
                    stateMark
                }
            }

            ForEach(Array(block.rows.enumerated()), id: \.offset) { _, row in
                LimitRowView(row: row).padding(.top, Theme.rowGap)
            }

            if block.showsNote {
                Text(block.note)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.muted)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, Theme.rowGap)
            }

            // Both open Claude Code for this environment; signing in happens there, because
            // Anthropic does not allow another program to offer the login (D-157).
            if block.showsOpen {
                FlatButton(title: block.openLabel, look: .ghost) { onOpenClaude(block.name) }
                    .padding(.top, Theme.rowGap)
                    .padding(.leading, -10)
            }
        }
    }

    /// "working now" is the one state worth the eye: the words go bright and the dot gets a halo.
    private var stateMark: some View {
        HStack(spacing: 3) {
            ZStack {
                if block.showsHalo {
                    Circle().fill(Theme.positive).opacity(0.18).frame(width: 12, height: 12)
                }

                if block.stateDot == .filled {
                    Circle().fill(Theme.positive).frame(width: 6, height: 6)
                } else {
                    Circle().strokeBorder(Theme.muted, lineWidth: 1.5).frame(width: 6, height: 6)
                }
            }
            .frame(width: 12, height: 12)

            Text(block.state)
                .font(Theme.sans(Theme.textTiny))
                .foregroundStyle(block.stateIsBright ? Theme.ink : Theme.muted)
                .lineLimit(1)
        }
    }
}

private struct LimitRowView: View {
    let row: LimitRow

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 8) {
                Text(row.name)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.muted)
                Spacer(minLength: 8)
                Text(row.percent)
                    .font(Theme.mono(Theme.textNumber))
                    .foregroundStyle(Theme.ink)
            }

            bar.padding(.top, 4)

            // Two columns, not two blocks in one cell. The word on the right is the one that must
            // stay whole, so the countdown on the left gives way first.
            HStack(spacing: 8) {
                Text(row.resets)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(Theme.muted)
                    .lineLimit(1)
                    .truncationMode(.tail)
                Spacer(minLength: 0)
                Text(row.pace)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(row.paceTakesTone ? Theme.tone(row.tone) : Theme.muted)
                    .lineLimit(1)
                    .layoutPriority(1)
            }
            .padding(.top, Theme.underBar)
        }
    }

    private var bar: some View {
        GeometryReader { space in
            ZStack(alignment: .leading) {
                RoundedRectangle(cornerRadius: Theme.barRadius, style: .continuous)
                    .fill(Theme.raised)

                RoundedRectangle(cornerRadius: Theme.barRadius, style: .continuous)
                    .fill(Theme.tone(row.tone))
                    .frame(width: space.size.width * row.fill)
            }
        }
        .frame(height: Theme.barHeight)
    }
}

/// Header buttons carry no label, so they need a visible hover: the raised grey, the same step the
/// site uses.
private struct IconButton<Mark: View>: View {
    let help: String
    let action: () -> Void
    @ViewBuilder let mark: () -> Mark

    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            mark()
                .frame(width: 22, height: 22)
                .background(hovering ? Theme.raised : .clear)
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .help(help)
    }
}

/// The system gear, at the size the Windows gear is drawn. The Windows build carries the gear of
/// the user's own icon set; here the system symbol is the shape a Mac user already knows, and it
/// saves redrawing a 24 unit path by hand.
private struct GearShape: View {
    var body: some View {
        Image(systemName: "gearshape")
            .font(.system(size: 13, weight: .regular))
            .foregroundStyle(Theme.muted)
    }
}

/// The icon set has no cross, so it is two lines. The system symbol is the same two lines.
private struct CrossShape: View {
    var body: some View {
        Image(systemName: "xmark")
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(Theme.muted)
    }
}
