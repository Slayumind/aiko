import AikoKit
import AppKit
import SwiftUI

/// The settings window wears the same material as the card: its own frame, so every surface of
/// Aiko looks like the same thing. The twin of SettingsWindow.xaml(.cs).
///
/// One window for everything (D-177), and no Save button: every change applies at once with a
/// short "Saved" mark (D-099, D-179).
@MainActor
final class SettingsWindow {
    private let window: EscapeWindow
    let state: SettingsState

    /// "Quit Aiko" closes the whole app, not this window, so the shell decides what to do.
    var onQuit: (() -> Void)?

    /// Raised after every saved change, so Aiko can follow it while the window is still open.
    var onChanged: (() -> Void)?

    var onClosed: (() -> Void)?

    init(page: SettingsPage?) {
        state = SettingsState(page: page)

        let body = SettingsLayout.bodyHeight(
            screenHeight: Double(NSScreen.main?.visibleFrame.height ?? 900))
        let size = NSSize(
            width: SettingsLayout.width + (Theme.shadowRoom * 2),
            height: SettingsLayout.headerHeight + body + (Theme.shadowRoom * 2))

        window = EscapeWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false)

        window.backgroundColor = .clear
        window.isOpaque = false
        window.hasShadow = false
        window.isMovableByWindowBackground = false
        window.isReleasedWhenClosed = false
        window.level = .normal
        window.collectionBehavior = [.fullScreenAuxiliary]
        window.animationBehavior = .none
        window.title = Strings.settingsWindowTitle

        state.onClose = { [weak self] in self?.close() }
        state.onQuit = { [weak self] in self?.onQuit?() }
        state.onChanged = { [weak self] in self?.onChanged?() }
        state.onReopen = { [weak self] in self?.reopenInTheNewLanguage() }

        // Esc closes from anywhere in the window, a text field included: what was typed is applied
        // on the way out, the same as leaving the field.
        window.onEscape = { [weak self] in self?.close() }
        window.onClosed = { [weak self] in
            self?.state.leave()
            self?.onClosed?()
        }

        window.contentView = NSHostingView(
            rootView: SettingsPanelView(state: state, bodyHeight: body))
        window.setContentSize(size)
    }

    var frame: NSRect { window.frame }

    /// For a picture of a page taller than the window: the window grows to the page instead of
    /// scrolling, so one screenshot holds the whole of it. The twin of SettingsPanel.GrowToPage.
    /// A page taller than the screen cannot be shown whole, so the caller says which end to keep.
    func growToPage(showingTheEnd: Bool = false) {
        let hosting = NSHostingView(rootView: SettingsPanelView(state: state, bodyHeight: nil))
        window.contentView = hosting
        hosting.layoutSubtreeIfNeeded()
        window.setContentSize(hosting.fittingSize)

        guard let room = NSScreen.main?.visibleFrame else { return }
        let bottom = showingTheEnd
            ? room.minY
            : max(room.minY, room.maxY - window.frame.height)
        window.setFrameOrigin(NSPoint(x: room.midX - (window.frame.width / 2), y: bottom))
    }

    /// The body of the panel inside the window, which is what a check measures.
    var panelFrame: NSRect {
        window.frame.insetBy(dx: Theme.shadowRoom, dy: Theme.shadowRoom)
    }

    func show() {
        window.center()
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func close() {
        window.close()
    }

    func openChecklist(at item: ChecklistItem? = nil) {
        state.openChecklist(at: item)
    }

    func startUpdateCheck() {
        state.show(.general)
        state.askForUpdate = true
    }

    /// Every word on screen was picked when its view was made, so the window is built again. Aiko
    /// makes windows on demand anyway, which is what lets a language change take effect at once
    /// instead of "after a restart".
    private func reopenInTheNewLanguage() {
        let page = state.page
        close()
        DispatchQueue.main.async { [weak self] in
            self?.onReopen?(page)
        }
    }

    var onReopen: ((SettingsPage) -> Void)?
}

/// A borderless window still has to take the keyboard, and Esc still has to close it.
final class EscapeWindow: NSWindow {
    var onEscape: (() -> Void)?
    var onClosed: (() -> Void)?

    override var canBecomeKey: Bool { true }

    override var canBecomeMain: Bool { true }

    override func cancelOperation(_ sender: Any?) {
        onEscape?()
    }

    override func close() {
        onClosed?()
        onClosed = nil
        super.close()
    }
}

/// The window's one view: the menu on the left, the chosen page on the right. The window keeps its
/// size while the pages change, so nothing under the mouse moves.
struct SettingsPanelView: View {
    @ObservedObject var state: SettingsState

    /// Nil lets the window grow to the page, which only a self test asks for.
    let bodyHeight: Double?

    var body: some View {
        VStack(spacing: 0) {
            header
            Control.hairline()

            HStack(spacing: 0) {
                SettingsNavView(state: state)
                    .frame(width: SettingsLayout.navWidth)
                Rectangle().fill(Theme.hairline).frame(width: 1)
                page
            }
            .frame(height: bodyHeight.map { CGFloat($0) })
        }
        .frame(width: SettingsLayout.width)
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: SettingsLayout.radius, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: SettingsLayout.radius, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .shadow(
            color: .black.opacity(Theme.shadowOpacity),
            radius: Theme.shadowBlur / 2,
            x: 0,
            y: Theme.shadowOffset)
        .padding(Theme.shadowRoom)
    }

    private var header: some View {
        HStack(spacing: 0) {
            Text(Strings.settings)
                .font(Theme.sans(Theme.textName, weight: .medium))
                .foregroundStyle(Theme.ink)
                .padding(.leading, 16)

            Spacer(minLength: 8)

            MarkButton(help: Strings.close, press: state.onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 10, weight: .medium))
                    .foregroundStyle(Theme.muted)
            }
            .padding(.trailing, 10)
        }
        .frame(height: SettingsLayout.headerHeight)
        .background(WindowDragArea())
    }

    private var page: some View {
        ZStack(alignment: .topTrailing) {
            ScrollView(.vertical) {
                SettingsPageView(state: state)
                    .padding(.horizontal, 20)
                    .padding(.top, 18)
                    .padding(.bottom, 20)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .scrollIndicators(.automatic)

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
        case .folders:
            FoldersPageView(state: state)
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

/// The menu: the ENVIRONMENTS label, a row per environment, the checklist row or the dashed slot
/// for a second environment, a hairline, then the rest, and the version at the bottom.
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
        case .label(let text):
            Control.sectionLabel(text)
                .padding(.leading, 10)
                .padding(.top, 4)
                .padding(.bottom, 6)

        case .item(let item):
            NavItemView(item: item, chosen: item.page == state.page) { state.show(item.page) }
                .padding(.bottom, 2)

        case .addSecondEnvironment:
            AddSecondSlot { state.openChecklist(at: .secondEnvironment) }
                .padding(.top, 4)

        case .separator:
            Rectangle().fill(Theme.hairline)
                .frame(height: 1)
                .padding(.horizontal, 10)
                .padding(.vertical, 8)
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
