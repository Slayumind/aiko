import AikoKit
import AppKit
import SwiftUI

/// The few controls the settings window needs, in the shape the site uses. The twin of
/// Theme/Controls.xaml: a switch, a segmented choice, a quiet button and a card you pick.
///
/// They live in one file so the settings window and the checklist page cannot drift apart, which
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
            .font(Theme.sans(Theme.textSmall))
            .foregroundStyle(Theme.ink)
    }

    static func rowNote(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textTiny))
            .foregroundStyle(Theme.muted)
    }

    /// A sentence, not a number: mono is for figures and metadata.
    static func rowHint(_ text: String) -> some View {
        Text(text)
            .font(Theme.sans(Theme.textSmall))
            .foregroundStyle(Theme.muted)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    static func pageTitle(_ text: String) -> some View {
        Text(text)
            .font(Theme.sans(16, weight: .medium))
            .foregroundStyle(Theme.ink)
    }

    static func hairline() -> some View {
        Rectangle().fill(Theme.hairline).frame(height: 1)
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
            .background(Theme.chosenLayer)
            .clipShape(RoundedRectangle(cornerRadius: 5, style: .continuous))
    }
}

/// The 32 by 18 switch of the site. The knob is where the value says at once, so a window that
/// opens with the switch on shows it on; only a flip is animated.
struct AikoSwitch: View {
    let isOn: Bool
    var enabled = true
    let set: (Bool) -> Void

    var body: some View {
        ZStack(alignment: isOn ? .trailing : .leading) {
            Capsule().fill(isOn ? Theme.positive : Theme.inputLine)
            Circle().fill(Theme.ink).frame(width: 14, height: 14).padding(.horizontal, 2)
        }
        .frame(width: 32, height: 18)
        .opacity(enabled ? 1 : 0.5)
        .animation(Motion.curve(Motion.spring, Motion.settle), value: isOn)
        .contentShape(Rectangle())
        .onTapGesture { if enabled { set(!isOn) } }
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
        .background(Theme.raised)
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
    }

    private struct Segment: View {
        let title: String
        let chosen: Bool
        let pick: () -> Void

        @State private var hovering = false

        var body: some View {
            Text(title)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(chosen || hovering ? Theme.ink : Theme.muted)
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .background(chosen ? Theme.inputLine : (hovering ? Theme.hoverLayer : .clear))
                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
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
            .background(chosen ? Theme.raised : (hovering ? Theme.hoverLayer : .clear))
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(chosen ? Theme.inputLine : Theme.hairline, lineWidth: 1))
            .contentShape(Rectangle())
            .onHover { hovering = $0 }
            .onTapGesture(perform: pick)
    }
}

/// The small buttons of the settings window. One shape for all of them: the face and the colour of
/// the words change, and the hover layer takes the colour of the words.
struct FlatButton: View {
    enum Look {
        case plain
        case ghost
        case danger
        case ghostDanger
        case quiet
    }

    let title: String
    var look = Look.plain
    var enabled = true
    let press: () -> Void

    @State private var hovering = false

    var body: some View {
        Button(action: { if enabled { press() } }) {
            Text(title)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(words)
                .padding(.horizontal, look == .quiet ? 12 : 10)
                .frame(height: look == .quiet ? 32 : 28)
                .background(face)
                .background(hovering ? hover : .clear)
                .clipShape(RoundedRectangle(cornerRadius: look == .quiet ? 10 : 8, style: .continuous))
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

    private var face: Color {
        switch look {
        case .ghost, .ghostDanger: return .clear
        case .danger: return Theme.destructive.opacity(0.16)
        default: return Theme.raised
        }
    }

    private var hover: Color {
        switch look {
        case .danger, .ghostDanger: return Theme.destructive.opacity(0.09)
        default: return Theme.hoverLayer
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
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .underline(true, color: hovering ? Theme.ink : Color.white.opacity(0.3))
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
    }
}

/// The close cross in a window header, and the small cross that unbinds a folder.
struct MarkButton<Mark: View>: View {
    let help: String
    let press: () -> Void
    @ViewBuilder let mark: () -> Mark

    @State private var hovering = false

    var body: some View {
        Button(action: press) {
            mark()
                .frame(width: 28, height: 28)
                .background(hovering ? Theme.raised : .clear)
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .help(help)
    }
}

/// A tick box with its words beside it, which wrap.
struct Tick: View {
    @Binding var isOn: Bool
    let title: String

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            ZStack {
                RoundedRectangle(cornerRadius: 4, style: .continuous)
                    .strokeBorder(Theme.inputLine, lineWidth: 1.5)
                if isOn {
                    RoundedRectangle(cornerRadius: 4, style: .continuous).fill(Theme.ink)
                    Image(systemName: "checkmark")
                        .font(.system(size: 9, weight: .bold))
                        .foregroundStyle(Theme.background)
                }
            }
            .frame(width: 16, height: 16)

            Text(title)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .contentShape(Rectangle())
        .onTapGesture { isOn.toggle() }
    }
}

/// A one line field on the raised plate, with the edge lighting up while it has the keyboard.
struct NameField: View {
    @Binding var text: String
    var mono = false
    var size: CGFloat = Theme.textSmall
    var placeholder = ""
    var onCommit: () -> Void = {}

    @FocusState private var focused: Bool

    var body: some View {
        TextField(placeholder, text: $text)
            .textFieldStyle(.plain)
            .font(mono ? Theme.mono(size) : Theme.sans(size))
            .foregroundStyle(Theme.ink)
            .focused($focused)
            .onSubmit(onCommit)
            .onChange(of: focused) { _, now in if !now { onCommit() } }
            .padding(.horizontal, 8)
            .padding(.vertical, 5)
            .background(Theme.raised)
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(focused ? Theme.inputLine : Theme.hairline, lineWidth: 1))
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
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))
    }
}

/// One choice out of a list, as a menu. The twin of Theme/DropDown.cs.
struct DropDown: View {
    let items: [String]
    let index: Int
    let pick: (Int) -> Void

    var body: some View {
        Menu {
            ForEach(Array(items.enumerated()), id: \.offset) { at, item in
                Button(item) { pick(at) }
            }
        } label: {
            HStack(spacing: 6) {
                Text(items.indices.contains(index) ? items[index] : "—")
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                Spacer(minLength: 4)
                Image(systemName: "chevron.down")
                    .font(.system(size: 9, weight: .medium))
                    .foregroundStyle(Theme.muted)
            }
            .padding(.horizontal, 8)
            .frame(height: 28)
            .background(Theme.raised)
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))
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
                .font(.system(size: 9, weight: .bold))
                .foregroundStyle(Theme.positive)
            Text(Strings.saved)
                .font(Theme.sans(Theme.textSmall))
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

/// A panel with a hairline edge, holding rows split by hairlines.
struct RowsBox<Content: View>: View {
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: 0) { content() }
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))
    }
}

/// The picture of a screen beside each answer of "where to show Aiko". The menu bar runs along the
/// top of every Mac screen, so the dot sits up there and not down by a taskbar.
struct ScreenPicture: View {
    enum Where {
        case menuBar
        case island
    }

    let show: Where
    var height: CGFloat = 46

    var body: some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: 4, style: .continuous).fill(Theme.background)

            switch show {
            case .menuBar:
                ZStack(alignment: .trailing) {
                    Rectangle().fill(Theme.raised).frame(height: 8)
                    Circle().fill(Theme.positive).frame(width: 5, height: 5).padding(.trailing, 6)
                }
                .frame(height: 8)
            case .island:
                UnevenRoundedRectangle(bottomLeadingRadius: 5, bottomTrailingRadius: 5, style: .continuous)
                    .fill(Theme.raised)
                    .frame(width: 38, height: 9)
            }
        }
        .frame(height: height)
        .overlay(
            RoundedRectangle(cornerRadius: 4, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
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
