import AikoKit
import AppKit
import SwiftUI

/// Everything that is not about one environment: where Aiko lives, startup, language, access.
/// The twin of Settings/GeneralPage.xaml(.cs).
///
/// There is no Velopack on macOS, so the update part shows the version and the check and nothing
/// else: the answer is a number and a page to open, never an install button.
struct GeneralPageView: View {
    @ObservedObject var state: SettingsState

    @State private var startsAtLogin = LoginItem.isEnabled()
    @State private var updateLine: String?
    @State private var downloadUrl = UpdateRun.releasesPage
    @State private var showDownload = false
    @State private var asking = false
    @State private var diagnostics = Strings.diagnosticsWhat
    @State private var accessLine = ""
    @State private var offeringToAdd = false
    @State private var trashSecond = false
    @State private var checking = false

    private static let languages: [AikoLanguage] = [.system, .russian, .english]

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.pageTitle(Strings.navGeneral)

            Control.sectionLabel(Strings.sectionWhereToShow).padding(.top, 18).padding(.bottom, 8)
            place

            Reveal(isOpen: state.app.place == .island) {
                HStack(spacing: 12) {
                    Control.rowName(Strings.hideIslandInFullScreen)
                    Spacer(minLength: 12)
                    AikoSwitch(isOn: state.app.hideIslandInFullScreen) {
                        var next = state.app
                        next.hideIslandInFullScreen = $0
                        state.saveApp(next)
                    }
                }
                .padding(.top, 12)
            }

            Control.sectionLabel(Strings.sectionStartupAndUpdates).padding(.top, 22).padding(.bottom, 8)
            HStack(spacing: 12) {
                Control.rowName(Strings.startAtLogin)
                Spacer(minLength: 12)
                AikoSwitch(isOn: startsAtLogin, set: setStartup)
            }

            // The update switch itself lives on the privacy page: it is a consent, not a
            // preference. The button stays here, because pressing it is the person's own action
            // (D-113).
            updates.padding(.top, 20)
            Control.rowHint(Strings.privacyLinkFromGeneral).padding(.top, 8).padding(.trailing, 56)

            Control.sectionLabel(Strings.sectionLanguage).padding(.top, 22).padding(.bottom, 8)
            DropDown(
                items: [Strings.languageSystem, Strings.languageRussian, Strings.languageEnglish],
                index: Self.languages.firstIndex(of: state.app.language) ?? 0,
                pick: pickLanguage)
                .frame(width: 220)

            Control.sectionLabel(Strings.sectionAccess).padding(.top, 22).padding(.bottom, 8)
            HStack(spacing: 12) {
                Control.rowHint(accessLine)
                Spacer(minLength: 12)
                FlatButton(title: offeringToAdd ? Strings.accessSetUp : Strings.accessCheck,
                           press: checkAccess)
            }

            Control.sectionLabel(Strings.sectionDiagnostics).padding(.top, 22).padding(.bottom, 8)
            HStack(spacing: 12) {
                Control.rowHint(diagnostics)
                Spacer(minLength: 12)
                FlatButton(title: Strings.copyDiagnostics, press: copyDiagnostics)
            }

            Control.hairline().padding(.top, 22).padding(.bottom, 14)
            startOver

            HStack {
                Spacer()
                FlatButton(title: Strings.quitAiko, look: .ghost, press: state.onQuit)
            }
            .padding(.top, 14)
            .padding(.trailing, -10)
        }
        .onAppear {
            showAccess()
            if state.askForUpdate {
                state.askForUpdate = false
                check()
            }
        }
    }

    // ---- where Aiko shows ----

    private var place: some View {
        HStack(spacing: 10) {
            PickCard(chosen: state.app.place == .tray, pick: { setPlace(.tray) }) {
                VStack(spacing: 8) {
                    ScreenPicture(show: .menuBar)
                    Control.rowName(Strings.placeMenuBar)
                }
            }
            PickCard(chosen: state.app.place == .island, pick: { setPlace(.island) }) {
                VStack(spacing: 8) {
                    ScreenPicture(show: .island)
                    Control.rowName(Strings.placeIsland)
                }
            }
        }
        .frame(maxWidth: 360, alignment: .leading)
    }

    private func setPlace(_ place: AikoPlace) {
        var next = state.app
        next.place = place
        state.saveApp(next)
    }

    // ---- startup and updates ----

    private func setStartup(_ wanted: Bool) {
        LoginItem.set(wanted)
        startsAtLogin = LoginItem.isEnabled()
        var next = state.app
        next.runAtStartup = wanted
        state.saveApp(next)
    }

    private var updates: some View {
        HStack(spacing: 8) {
            FlatButton(title: Strings.checkNow, enabled: !checking, press: check)
            if showDownload {
                FlatButton(title: Strings.openDownloadPage) { open(downloadUrl) }
            }
            if let updateLine {
                Control.rowHint(updateLine).padding(.leading, 2)
            }
        }
    }

    /// Asked for by hand, so it runs whatever the switch says: the switch decides whether Aiko
    /// checks on its own, not whether the person may ask.
    private func check() {
        guard !checking else { return }
        checking = true
        updateLine = Strings.updateAsking
        showDownload = false

        Task {
            let info = await UpdateRun.ask()
            let version = AppVersion.current()

            switch info.compareWith(version) {
            case .available:
                updateLine = Strings.format(Strings.updateAvailable, info.latest ?? "")
                showDownload = true
            case .upToDate:
                updateLine = Strings.format(Strings.updateLatest, version)
            // Never "you are up to date" when we do not know: that is the one answer that would
            // keep every copy quiet after a bad deploy.
            case .unknown:
                updateLine = Strings.updateFailed
            }

            downloadUrl = info.downloadUrl ?? UpdateRun.releasesPage
            checking = false
        }
    }

    private func pickLanguage(_ index: Int) {
        guard Self.languages.indices.contains(index) else { return }
        let language = Self.languages[index]
        guard language != state.app.language else { return }

        var next = state.app
        next.language = language
        state.saveApp(next)

        Strings.language = SessionReminder.isRussian(
            language, systemLanguageTag: Locale.preferredLanguages.first) ? .russian : .english
        state.onReopen()
    }

    // ---- access to the limits ----

    private func foldersWithoutOurLine() -> [String] {
        state.environments.environments
            .flatMap(\.configDirectories)
            .filter { !ClaudeSettingsFile.hasOurLine($0) }
    }

    private func showAccess() {
        let missing = foldersWithoutOurLine()
        if missing.isEmpty {
            accessLine = state.environments.hasEnvironments ? Strings.accessOk : ""
            offeringToAdd = false
            return
        }

        accessLine = missing.count == 1
            ? Strings.accessMissingOne
            : Strings.format(Strings.accessMissingMany, missing.count)
        offeringToAdd = true
    }

    /// The first press only looks. The second one writes, and the button says so before it does:
    /// this is somebody else's settings file, and pressing "check" should never change it.
    private func checkAccess() {
        guard offeringToAdd else {
            showAccess()
            return
        }

        guard let bridge = BridgePath.current() else {
            accessLine = Strings.accessNoBridge
            return
        }

        var problem = PatchProblem.none
        for folder in foldersWithoutOurLine() {
            let outcome = ClaudeSettingsFile.addBridge(folder, bridge)
            if problem == .none { problem = outcome.problem }
        }

        showAccess()
        state.showSaved()
        if problem != .none {
            accessLine = ClaudeSettingsFile.words(problem)
        }
    }

    // ---- diagnostics ----

    /// Everything a bug report needs and nothing it does not: no tokens, no email addresses, no
    /// numbers from the limits, no paths from Claude Code.
    private func copyDiagnostics() {
        var facts = DiagnosticsFacts()
        facts.version = AppVersion.withCommit()
        facts.system = AppVersion.system()
        facts.place = state.app.place
        facts.language = state.app.language
        facts.startsAtLogin = LoginItem.isEnabled()
        facts.checksUpdates = state.app.checkUpdates
        facts.sendsStats = state.app.sendStats
        facts.environments = state.environments.environments.count
        facts.directMode = state.environments.environments.filter(\.directMode).count
        facts.boundFolders = EnvironmentEdits.bindings(state.environments).count
        facts.commandsSetUp = CommandFolder.isSetUp
        facts.shell = "\(ClaudeSettingsFile.shell)"
        facts.logPath = Log.filePath

        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(DiagnosticsText.build(facts), forType: .string)
        diagnostics = Strings.diagnosticsCopied
    }

    // ---- starting over ----

    /// Starting over takes a lot away, so it asks a second time and says what goes.
    private var startOver: some View {
        let second = state.environments.second(.macOS, Store.home)

        return VStack(alignment: .leading, spacing: 0) {
            if !asking {
                HStack(spacing: 12) {
                    Control.rowHint(Strings.restartWhat)
                    Spacer(minLength: 12)
                    FlatButton(title: Strings.restart, look: .ghostDanger) { asking = true }
                        .padding(.trailing, -10)
                }
            }

            Reveal(isOpen: asking) {
                VStack(alignment: .leading, spacing: 0) {
                    Text(Strings.format(
                        Strings.restartLineMac,
                        state.environments.environments.map(\.command).joined(separator: ", ")))
                        .font(Theme.sans(Theme.textSmall))
                        .foregroundStyle(Theme.ink)
                        .fixedSize(horizontal: false, vertical: true)

                    // Only the second environment's folder can go: .claude belongs to the IDE
                    // panel and Claude Desktop too.
                    if let second, let folder = second.configDirectories.first {
                        Tick(isOn: $trashSecond,
                             title: Strings.format(Strings.restartTrash, (folder as NSString).lastPathComponent))
                            .padding(.top, 12)
                    }

                    Control.rowHint(Strings.restartKeep).padding(.top, 8)

                    HStack(spacing: 8) {
                        Spacer()
                        FlatButton(title: Strings.cancel, look: .ghost) {
                            asking = false
                            trashSecond = false
                        }
                        FlatButton(title: Strings.restart, look: .danger) {
                            asking = false
                            state.startOver(trashSecond: trashSecond)
                        }
                    }
                    .padding(.top, 12)
                }
                .padding(12)
                .background(Theme.destructive.opacity(0.06))
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .strokeBorder(Theme.destructive.opacity(0.22), lineWidth: 1))
            }
        }
    }

    private func open(_ address: String) {
        guard let url = URL(string: address) else { return }
        NSWorkspace.shared.open(url)
    }
}
