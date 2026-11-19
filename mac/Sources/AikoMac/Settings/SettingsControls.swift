import AikoKit
import AppKit
import SwiftUI

/// The few controls the settings need, in the new look. The twin of Theme/Controls.xaml: a
/// switch, a segmented choice, a button, a tick and a card you pick. Corners are square, every
/// line is a hairline, and the one font is Geist Mono (D-435).
///
/// They live in one file so the settings pages and the checklist page cannot drift apart, which
/// is the same reason the WPF ones share a dictionary.
enum Control {
    /// Group labels on the site are small, mono and in capitals. The capitals are typed into
    /// strings.json, so no code upper-cases anything a translator wrote.
    static func sectionLabel(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.muted)
    }

    static func rowName(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textSmall))
            .foregroundStyle(Theme.ink)
    }

    /// Wraps, as RowNote does in most places on Windows; a caller that wants one line says so.
    static func rowNote(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.muted)
    }

    static func rowHint(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textSmall))
            .foregroundStyle(Theme.muted)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    static func pageTitle(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textTitle, weight: .medium))
            .foregroundStyle(Theme.ink)
    }

    static func hairline() -> some View {
        Rule()
    }
}

/// The light layer under the mouse, which fades in and out the same everywhere.
private struct HoverFade: ViewModifier {
    let shown: Bool
    let colour: Color

    func body(content: Content) -> some View {
        content
            .background(colour.opacity(shown ? 1 : 0))
            .animation(Motion.curve(Motion.standard, Motion.hover), value: shown)
    }
}

extension View {
    func hoverLayer(_ shown: Bool, colour: Color = Theme.hoverLayer) -> some View {
        modifier(HoverFade(shown: shown, colour: colour))
    }

    /// A hairline frame on the edge of the view, square as every frame of the new look.
    func hairlineFrame(_ colour: Color = Theme.hairline) -> some View {
        overlay(Rectangle().strokeBorder(colour, lineWidth: 1))
    }
}

/// A plan name or a count: mono on a faint plate.
struct Chip: View {
    let text: String

    var body: some View {
        Text(text)
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.ink)
            .padding(.horizontal, 6)
            .padding(.vertical, 1)
            .background(Theme.hoverLayer)
    }
}

/// Both values stand in a row and the chosen one is in brackets: Off [On] (D-438). The brackets
/// of the other value keep their place, so nothing moves when it flips.
struct AikoSwitch: View {
    let isOn: Bool
    var enabled = true
    let set: (Bool) -> Void

    @State private var hovering = false

    var body: some View {
        HStack(spacing: 8) {
            value(Strings.switchOff, chosen: !isOn, tone: Theme.ink)
            value(Strings.switchOn, chosen: isOn, tone: Theme.positive)
        }
        .padding(.vertical, 3)
        .opacity(enabled ? 1 : 0.5)
        .contentShape(Rectangle())
        .onHover { hovering = $0 && enabled }
        .onTapGesture { if enabled { set(!isOn) } }
    }

    private func value(_ word: String, chosen: Bool, tone: Color) -> some View {
        let ink = chosen ? tone : (hovering ? Theme.ink : Theme.quietText)
        let brackets = chosen ? ink : Color.clear
        return (Text("[").foregroundColor(brackets) + Text(word).foregroundColor(ink) + Text("]").foregroundColor(brackets))
            .font(Theme.mono(Theme.textBody, weight: chosen ? .medium : .regular))
    }
}

/// One choice out of a few. The chosen one is lighter, not tinted: three greys of the same
/// lightness are what the site uses for hover, and a tinted "on" would blend into it.
struct SegmentBar<Value: Hashable>: View {
    let choices: [(value: Value, title: String)]
    let picked: Value
    let pick: (Value) -> Void

    var body: some View {
        HStack(spacing: 0) {
            ForEach(choices, id: \.value) { choice in
                Segment(title: choice.title, chosen: choice.value == picked) { pick(choice.value) }
            }
        }
        .padding(3)
        .hairlineFrame()
        .fixedSize()
    }

    private struct Segment: View {
        let title: String
        let chosen: Bool
        let pick: () -> Void

        @State private var hovering = false

        var body: some View {
            Text(title)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(chosen || hovering ? Theme.ink : Theme.muted)
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .hoverLayer(hovering && !chosen)
                .background(chosen ? Theme.raised : .clear)
                .contentShape(Rectangle())
                .onHover { hovering = $0 }
                .onTapGesture(perform: pick)
        }
    }
}

/// A card you pick, with a small picture of a screen inside: the choice is seen and not read.
struct PickCard<Content: View>: View {
    let chosen: Bool
    let pick: () -> Void
    @ViewBuilder let content: () -> Content

    @State private var hovering = false

    var body: some View {
        content()
            .padding(11)
            .frame(maxWidth: .infinity)
            .hoverLayer(hovering)
            .background(chosen ? Theme.raised : .clear)
            .hairlineFrame()
            .contentShape(Rectangle())
            .onHover { hovering = $0 }
            .onTapGesture(perform: pick)
    }
}

/// The buttons of the window. One shape for all of them, as Widgets.Flat on Windows: every
/// button is outlined, the main action has the brighter line, a ghost one has none.
struct FlatButton: View {
    enum Look {
        case plain

        /// The one button that finishes what the person typed: the brighter line, medium words.
        case primary
        case ghost
        case danger
        case ghostDanger

        /// The taller button of a page footer, 32 high.
        case quiet
    }

    let title: String
    var look = Look.plain

    /// An SF Symbol before the words, for a button that makes something new.
    var symbol: String?
    var enabled = true
    let press: () -> Void

    @State private var hovering = false

    var body: some View {
        Button(action: { if enabled { press() } }) {
            HStack(spacing: 8) {
                if let symbol {
                    Image(systemName: symbol).font(.system(size: 10, weight: .light))
                }
                Text(title).font(Theme.mono(Theme.textSmall, weight: look == .primary ? .medium : .regular))
            }
                .foregroundStyle(words)
                .padding(.horizontal, look == .quiet ? 12 : 10)
                .frame(height: look == .quiet ? 32 : 28)
                .hoverLayer(hovering)
                .hairlineFrame(line)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .opacity(enabled ? 1 : 0.5)
        .onHover { hovering = $0 && enabled }
    }

    private var words: Color {
        switch look {
        case .danger, .ghostDanger: return Theme.destructive
        case .ghost: return hovering ? Theme.ink : Theme.muted
        default: return Theme.ink
        }
    }

    private var line: Color {
        switch look {
        case .ghost, .ghostDanger: return .clear
        case .danger: return Theme.destructive
        case .primary: return Theme.muted
        default: return Theme.hairline
        }
    }
}

/// Goes somewhere else in the same window. Underlined, so it does not read as a label.
struct LinkButton: View {
    let title: String
    let press: () -> Void

    @State private var hovering = false

    var body: some View {
        Button(action: press) {
            Text(title)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .underline(true, color: hovering ? Theme.ink : Theme.hairline)
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
    }
}

/// The close cross in a window header, and the small cross that unbinds a folder: 28 by 28, a
/// light layer on hover.
struct MarkButton<Mark: View>: View {
    let help: String
    let press: () -> Void
    @ViewBuilder let mark: () -> Mark

    @State private var hovering = false

    var body: some View {
        Button(action: press) {
            mark()
                .frame(width: 28, height: 28)
                .hoverLayer(hovering, colour: Theme.raised)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .tip(help)
    }
}

/// A tick box with its words beside it, which wrap. Square, filled with ink when it is on.
struct Tick: View {
    @Binding var isOn: Bool
    let title: String

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            ZStack {
                Rectangle().strokeBorder(Theme.hairline, lineWidth: 1.5)
                if isOn {
                    Rectangle().fill(Theme.ink)
                    Path { path in
                        path.move(to: CGPoint(x: 4, y: 8))
                        path.addLine(to: CGPoint(x: 7, y: 11))
                        path.addLine(to: CGPoint(x: 12, y: 5))
                    }
                    .stroke(Theme.background, style: StrokeStyle(lineWidth: 1.6, lineCap: .round, lineJoin: .round))
                }
            }
            .frame(width: 16, height: 16)

            Text(title)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .contentShape(Rectangle())
        .onTapGesture { isOn.toggle() }
    }
}

/// A one line field in a hairline frame; the frame turns grey while it has the keyboard.
struct NameField: View {
    @Binding var text: String
    var size: CGFloat = Theme.textSmall
    var placeholder = ""
    var onCommit: () -> Void = {}

    @FocusState private var focused: Bool

    var body: some View {
        TextField(placeholder, text: $text)
            .textFieldStyle(.plain)
            .font(Theme.mono(size))
            .foregroundStyle(Theme.ink)
            .focused($focused)
            .onSubmit(onCommit)
            .onChange(of: focused) { _, now in if !now { onCommit() } }
            .padding(.horizontal, 8)
            .padding(.vertical, 5)
            .hairlineFrame(focused ? Theme.muted : Theme.hairline)
    }
}

/// The exact text Aiko is about to write into somebody else's file. Shown, not described.
struct CodeBlock: View {
    let text: String

    var body: some View {
        Text(text)
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.ink)
            .textSelection(.enabled)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .background(Theme.background)
            .hairlineFrame()
    }
}

/// One choice out of a list, as a menu. The twin of Theme/DropDown.cs.
struct DropDown: View {
    let items: [String]
    let index: Int

    /// Group titles, each above the item with its index. A title cannot be picked.
    var headers: [Int: String] = [:]
    let pick: (Int) -> Void

    var body: some View {
        Menu {
            ForEach(Array(items.enumerated()), id: \.offset) { at, item in
                if let header = headers[at] {
                    if at > 0 { Divider() }
                    Text(header)
                }
                Button(item) { pick(at) }
            }
        } label: {
            HStack(spacing: 6) {
                Text(items.indices.contains(index) ? items[index] : "—")
                    .font(Theme.mono(Theme.textSmall))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                Spacer(minLength: 4)
                Image(systemName: "chevron.down")
                    .font(.system(size: 9, weight: .light))
                    .foregroundStyle(Theme.muted)
            }
            .padding(.horizontal, 8)
            .frame(height: 28)
            .hairlineFrame()
        }
        // A borderless menu draws an arrow of its own before the label and drops the plate the
        // label asked for, so the button style is the plain one and the whole face is ours.
        .menuStyle(.button)
        .buttonStyle(.plain)
        .menuIndicator(.hidden)
        .fixedSize(horizontal: false, vertical: true)
    }
}

/// "Saved", for a moment, after a change. With no Save button this is how the person knows the
/// change took (D-179). The twin of SavedMark.cs.
struct SavedMark: View {
    let shown: Bool

    var body: some View {
        HStack(spacing: 5) {
            Image(systemName: "checkmark")
                .font(.system(size: 9, weight: .regular))
                .foregroundStyle(Theme.positive)
            Text(Strings.saved)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.muted)
        }
        .opacity(shown ? 1 : 0)
        .animation(Motion.curve(Motion.standard, shown ? Motion.hover : Motion.settle), value: shown)
        .allowsHitTesting(false)
    }
}

/// A block that unfolds under something. The twin of Theme/RevealPanel.cs.
struct Reveal<Content: View>: View {
    let isOpen: Bool
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if isOpen {
                content()
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .animation(Motion.curve(Motion.standard, Motion.expand), value: isOpen)
    }
}

/// Rows of settings in one hairline frame, split by hairlines.
struct RowsBox<Content: View>: View {
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: 0) { content() }
            .frame(maxWidth: .infinity, alignment: .leading)
            .hairlineFrame()
    }
}

/// The box around a step that cannot be undone, and around a warning (DangerBox and CautionBox).
struct ZoneBox<Content: View>: View {
    enum Kind {
        case danger
        case caution
    }

    let kind: Kind
    @ViewBuilder let content: () -> Content

    var body: some View {
        content()
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(kind == .danger ? Theme.dangerZone : Theme.cautionZone)
            .hairlineFrame(kind == .danger ? Theme.dangerZoneLine : Theme.cautionZoneLine)
    }
}

/// The picture of a screen beside each answer of "where to show Aiko". The menu bar runs along the
/// top of every Mac screen, so the mark sits up there and not down by a taskbar.
struct ScreenPicture: View {
    enum Where {
        case menuBar
        case island
    }

    let show: Where
    var height: CGFloat = 46

    var body: some View {
        ZStack(alignment: .top) {
            Rectangle().fill(Theme.background)

            switch show {
            case .menuBar:
                ZStack(alignment: .trailing) {
                    Rectangle().fill(Theme.raised).frame(height: 8)
                    Rectangle().fill(Theme.ink).frame(width: 5, height: 5).padding(.trailing, 6)
                }
                .frame(height: 8)
            case .island:
                Rectangle()
                    .fill(Theme.raised)
                    .frame(width: 38, height: 9)
            }
        }
        .frame(height: height)
        .hairlineFrame()
    }
}

/// Picking a folder, the one dialog the settings window opens.
enum FolderPicker {
    @MainActor
    static func pick(title: String) -> String? {
        let panel = NSOpenPanel()
        panel.message = title
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        panel.prompt = title

        return panel.runModal() == .OK ? panel.url?.path : nil
    }
}
