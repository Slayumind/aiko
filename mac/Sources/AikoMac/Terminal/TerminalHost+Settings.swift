import AikoKit
import AppKit
import SwiftUI

/// The settings inside the terminal window (D-374): they come on the place of the list and the
/// terminal, at a page when one is asked for, and go back to the same session as it was. The
/// sessions run on meanwhile. The twin of TerminalHost.Settings.cs.
extension TerminalHost {
    /// Opens the settings, or moves the open ones to the page and the item asked for.
    @discardableResult
    func openSettings(at page: SettingsPage?, item: ChecklistItem? = nil) -> SettingsState {
        if let open = settings {
            if let page { open.show(page) }
            if let item { open.openChecklist(at: item) }
            show()
            return open
        }

        let state = SettingsState(page: page)
        // AIKO_TRY_FIRST_RUN=1 shows the first run on a Mac that is set up, for --try-wizard.
        state.firstRun = !Store.environments().hasEnvironments
            || ProcessInfo.processInfo.environment["AIKO_TRY_FIRST_RUN"] == "1"
        state.onFirstRunFinished = { [weak self] in self?.finishFirstRun() }
        state.onClose = { [weak self] in self?.closeSettings() }
        state.onQuit = { [weak self] in self?.onQuitAsked?() }
        state.onChanged = { [weak self] in
            self?.onSettingsChanged?()
            self?.applyZoom()
            self?.draw()
        }
        // Every word on screen was picked when its view was made, so the panel is built again.
        state.onReopen = { [weak self] in self?.buildSettingsPanel() }
        if let item { state.openChecklist(at: item) }
        settings = state

        show()
        buildSettingsPanel()
        return state
    }

    /// Back to the same session, as it was. What was typed on the page is applied on the way out.
    func closeSettings() {
        guard let state = settings else { return }
        state.leave()
        settings = nil
        settingsCover?.removeFromSuperview()
        settingsCover = nil
        self.state.settingsOpen = false
        draw()
    }

    /// The first run wrote the environments: the terminal takes the window, and it counts as the
    /// one time the window opened by itself (D-350).
    func finishFirstRun() {
        closeSettings()
        var app = Store.settings()
        app.terminalShown = true
        Store.saveSettings(app)
        model = TerminalWindowModel.opened(sessions, Store.environments().environments.map(\.name))
        onSettingsChanged?()
        draw()
    }

    /// The gear opens the settings and closes them again.
    func toggleSettings() {
        // The first run has nothing behind it to go back to: Finish is the way out (D-394).
        if settings?.firstRun == true { return }

        if settings == nil {
            openSettings(at: nil)
        } else {
            closeSettings()
        }
    }

    /// The panel over the list and the terminal, under the title bar.
    func buildSettingsPanel() {
        guard let state = settings, let window, let root = window.contentView, let bar else { return }
        settingsCover?.removeFromSuperview()

        let panel = zoomed(SettingsPanelView(state: state, menuWidth: Self.listWidth))
        panel.translatesAutoresizingMaskIntoConstraints = false
        root.addSubview(panel, positioned: .below, relativeTo: bar)
        NSLayoutConstraint.activate([
            panel.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            panel.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            panel.topAnchor.constraint(equalTo: bar.bottomAnchor),
            panel.bottomAnchor.constraint(equalTo: root.bottomAnchor),
        ])
        settingsCover = panel
        self.state.settingsOpen = true

        // The keys go to the panel, so Esc closes it and no key reaches a terminal under it.
        window.makeFirstResponder(panel)
    }
}
