import AikoKit
import AppKit
import SwiftUI

/// The settings inside the terminal window (D-374, D-391): the menu on the place of the list of
/// sessions, as wide as it, and the chosen page in a readable column on the left of the rest. The
/// twin of SettingsPanel.xaml(.cs). There is no Save button: every change applies at once with a
/// short "Saved" mark (D-099, D-179). The way back is in the title bar: a pill, the gear or Esc.
struct SettingsPanelView: View {
    @ObservedObject var state: SettingsState

    /// The width of the menu: the list of sessions, so the edge between them does not move.
    let menuWidth: CGFloat

    var body: some View {
        if state.firstRun {
            firstRun
        } else {
            panel
        }
    }

    /// The checklist alone, one step at a time in a column in the middle, with no menu (D-394).
    private var firstRun: some View {
        ScrollView(.vertical) {
            Group {
                if let checklist = state.checklist {
                    ChecklistPageView(state: state, model: checklist, steps: true)
                }
            }
            .frame(width: 520)
            .padding(.top, 48)
            .padding(.bottom, 32)
            .frame(maxWidth: .infinity)
        }
        .background(Theme.surface)
    }

    private var panel: some View {
        HStack(spacing: 0) {
            SettingsNavView(state: state)
                .frame(width: menuWidth)
                .overlay(alignment: .trailing) {
                    Rectangle().fill(Theme.hairline).frame(width: 1)
                }
            page
        }
        .background(Theme.surface)
        .onExitCommand { state.onClose() }
    }

    /// The pages were made for a column this wide: a wider one makes lines too long to read (D-391).
    static let pageWidth: CGFloat = 640

    private static let pageEnd = "page end"

    private var page: some View {
        ZStack(alignment: .topTrailing) {
            ScrollViewReader { scroller in
                ScrollView(.vertical) {
                    SettingsPageView(state: state)
                        .padding(.horizontal, 20)
                        .padding(.top, 18)
                        .padding(.bottom, 20)
                        .frame(maxWidth: Self.pageWidth, alignment: .leading)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    Color.clear.frame(height: 1).id(Self.pageEnd)
                }
                .scrollIndicators(.automatic)
                .onAppear {
                    // AIKO_TRY_SCROLL=end shows the bottom of a long page, for a snapshot.
                    if ProcessInfo.processInfo.environment["AIKO_TRY_SCROLL"] == "end" {
                        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { scroller.scrollTo(Self.pageEnd, anchor: .bottom) }
                    }
                }
            }

            SavedMark(shown: state.saved)
                .padding(.top, 23)
                .padding(.trailing, 24)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

/// The page the menu points at. Every page is built when it is shown and dropped when it is left,
/// as the WPF panel does with its ContentControl.
struct SettingsPageView: View {
    @ObservedObject var state: SettingsState

    var body: some View {
        switch state.page {
        case .environment(let folder):
            if state.environments.environments.contains(where: { $0.holds(folder) }) {
                EnvironmentPageView(state: state, folder: folder).id(folder)
            } else {
                GeneralPageView(state: state)
            }
        case .checklist:
            if let checklist = state.checklist {
                ChecklistPageView(state: state, model: checklist)
            } else {
                GeneralPageView(state: state)
            }
        case .terminal:
            TerminalPageView(state: state)
        case .personality:
            if state.environments.hasEnvironments {
                PersonalityPageView(state: state)
            } else {
                GeneralPageView(state: state)
            }
        case .privacy:
            PrivacyPageView(state: state)
        case .general:
            GeneralPageView(state: state)
        }
    }
}

/// The menu: General, the environments with the checklist row or the dashed slot for a second
/// one, then Window, Personality and Privacy; the groups apart by room alone (D-404), and the
/// version at the bottom.
struct SettingsNavView: View {
    @ObservedObject var state: SettingsState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ForEach(Array(state.navRows.enumerated()), id: \.offset) { _, row in
                entry(row)
            }

            Spacer(minLength: 8)

            Text("Aiko \(AppVersion.withCommit())")
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .lineLimit(1)
                .padding(.leading, 10)
                .padding(.top, 8)
        }
        .padding(.horizontal, 8)
        .padding(.top, 12)
        .padding(.bottom, 10)
        .frame(maxHeight: .infinity, alignment: .top)
    }

    @ViewBuilder
    private func entry(_ row: SettingsNavEntry) -> some View {
        switch row {
        case .item(let item):
            NavItemView(item: item, chosen: item.page == state.page) { state.show(item.page) }
                .padding(.bottom, 2)

        case .addSecondEnvironment:
            AddSecondSlot { state.openChecklist(at: .secondEnvironment) }
                .padding(.top, 4)

        case .gap:
            Color.clear.frame(height: 20)
        }
    }
}

/// One item of the side menu: the chosen one sits on a lighter plate.
private struct NavItemView: View {
    let item: SettingsNavItem
    let chosen: Bool
    let pick: () -> Void

    @State private var hovering = false

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(item.title)
                .font(Theme.sans(Theme.textName))
                .foregroundStyle(chosen || hovering ? Theme.ink : Theme.muted)
                .lineLimit(1)
                .truncationMode(.tail)

            if let note = item.note {
                Text(note)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(Theme.muted)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 7)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(chosen ? Theme.chosenLayer : (hovering ? Theme.hoverLayer : .clear))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .contentShape(Rectangle())
        .onHover { hovering = $0 }
        .onTapGesture(perform: pick)
    }
}

/// The empty place of environment 2, drawn with a dashed edge.
private struct AddSecondSlot: View {
    let press: () -> Void

    @State private var hovering = false

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: "plus")
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(Theme.muted)
            Text(Strings.addSecondEnvironment)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(hovering ? Theme.ink : Theme.muted)
                .lineLimit(1)
                .truncationMode(.tail)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(hovering ? Theme.hoverLayer : .clear)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .strokeBorder(Theme.inputLine, style: StrokeStyle(lineWidth: 1, dash: [4, 3])))
        .contentShape(Rectangle())
        .onHover { hovering = $0 }
        .onTapGesture(perform: press)
    }
}
