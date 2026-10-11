import AikoKit
import AppKit
import SwiftUI

/// Everything that is not about one environment: where Aiko lives, startup, language, access.
/// The twin of Settings/GeneralPage.xaml(.cs).
///
struct GeneralPageView: View {
    @ObservedObject var state: SettingsState

    @State private var startsAtLogin = LoginItem.isEnabled()
    @State private var downloadUrl = UpdateRun.releasesPage
    /// The version the site named, kept for the line shown while it downloads.
    @State private var newerVersion: String?

    /// Where the update line stands, and what went wrong with the last download, in words.
    @State private var stage = UpdateStage.notAsked
    @State private var problem: String?
    @State private var percent = 0
    @State private var asking = false
    @State private var deleteAsking = false
    @State private var diagnostics = Strings.diagnosticsWhat
    @State private var accessLine = ""
    @State private var offeringToAdd = false
    @State private var trashSecond = false

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
            deleteAiko.padding(.top, 14)

            HStack {
                Spacer()
                FlatButton(title: Strings.quitAiko, look: .ghost, press: state.onQuit)
            }
            .padding(.top, 14)
            .padding(.trailing, -10)
        }
        .onAppear {
            showAccess()
            if UpdateInstall.readyVersion != nil {
                stage = .ready
            }
            tryUpdateStage()
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

    /// One state on the left, one action on the right (D-278). Downloading and replacing Aiko
    /// starts here and nowhere else: the daily check only ever says that a newer version is out.
    private var updates: some View {
        let words = updateWords
        let action = UpdateBlock.actionFor(stage)

        // The step forward is the one bright button on the page; asking again is an ordinary one.
        let forward = action == .install || action == .restart

        return HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Control.rowName(words.title).fixedSize(horizontal: false, vertical: true)
                if !words.detail.isEmpty {
                    Control.rowNote(words.detail)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            if stage == .downloading {
                // In place of the button while a download runs: there is nothing to press then.
                Text("\(UpdateBlock.percent(percent))%")
                    .font(Theme.mono(Theme.textSmall))
                    .foregroundStyle(Theme.ink)
                    .padding(.horizontal, 10)
            } else {
                FlatButton(
                    title: buttonTitle(action), look: forward ? .primary : .plain,
                    enabled: action != .none, press: pressUpdate)
            }
        }
    }

    /// What the line says at its stage, and the small print under it.
    private var updateWords: (title: String, detail: String) {
        let version = AppVersion.current()
        let newer = newerShown
        let current = Strings.format(Strings.updateThisVersion, version)
        let comparing = Strings.format(Strings.updateNow, version)

        switch stage {
        case .notAsked:
            return (current, "")
        case .asking:
            return (Strings.updateAsking, current)
        case .upToDate:
            guard let at = UpdateRun.lastAnswered else { return (Strings.updateLatest, current) }
            return (Strings.updateLatest, Strings.format(Strings.updateCheckedAt, version, Self.clock(at)))
        case .available:
            return (Strings.format(Strings.updateAvailable, newer), comparing)
        case .checkFailed:
            return (Strings.updateFailed, current)
        case .downloading:
            return (Strings.format(Strings.updateDownloading, newer), comparing)
        case .ready:
            return (Strings.format(Strings.updateReady, newer), Strings.updateReadyDetail)
        case .downloadFailed, .cannotInstall:
            return (problem ?? Strings.updateDownloadFailed, comparing)
        }
    }

    private var newerShown: String {
        UpdateInstall.readyVersion ?? newerVersion ?? ""
    }

    private func buttonTitle(_ action: UpdateAction) -> String {
        switch action {
        case .install: return Strings.format(Strings.updateInstallNow, newerShown)
        case .restart: return Strings.updateRestartNow
        case .openDownloadPage: return Strings.openDownloadPage
        case .check, .none: return Strings.checkNow
        }
    }

    /// 12:40 in every language, as on Windows.
    private static func clock(_ moment: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "HH:mm"
        return formatter.string(from: moment)
    }

    /// A door for snapshots: AIKO_TRY_UPDATE=UpToDate, Available, Downloading, Ready, CheckFailed,
    /// DownloadFailed or CannotInstall draws the update line at that stage, with a made-up version.
    /// It asks nobody and writes nothing.
    private func tryUpdateStage() {
        guard let asked = ProcessInfo.processInfo.environment["AIKO_TRY_UPDATE"],
              let wanted = UpdateStage.allCases.first(where: { "\($0)".lowercased() == asked.lowercased() })
        else {
            return
        }

        newerVersion = "9.9.9"
        problem = wanted == .cannotInstall ? Strings.updateBadSignature : Strings.updateDownloadFailed
        stage = wanted
        percent = wanted == .downloading ? 64 : 0
    }

    /// The one button of the line does whatever is the next step from where the line stands.
    private func pressUpdate() {
        switch UpdateBlock.actionFor(stage) {
        // Asked for by hand, so it runs whatever the switch says: the switch decides whether Aiko
        // checks on its own, not whether the person may ask.
        case .check: check()
        case .install: install()
        case .restart: UpdateInstall.relaunch()
        case .openDownloadPage: open(downloadUrl)
        case .none: break
        }
    }

    private func check() {
        // Already asking or downloading. Two answers racing into one line is worse than one.
        guard UpdateBlock.actionFor(stage) != .none else { return }
        stage = .asking

        Task {
            let info = await UpdateRun.ask()
            let state = info.compareWith(AppVersion.current())

            newerVersion = state == .available ? info.latest : nil
            downloadUrl = info.downloadUrl ?? UpdateRun.releasesPage

            // A version already downloaded and checked in this session stays offered, whatever the
            // site says now.
            stage = UpdateInstall.readyVersion == nil ? UpdateBlock.after(state) : .ready
        }
    }

    /// Downloads the new version and puts it in place only if the release list is signed with our
    /// key and the zip matches its line (D-246). Pressing this is the consent to download; the
    /// restart asks once more.
    private func install() {
        guard let version = newerVersion, UpdateBlock.actionFor(stage) == .install else { return }
        percent = 0
        stage = .downloading

        Task {
            // URLSession reports from its own thread; the number is shown on ours. It only grows:
            // two reports can arrive out of order.
            let outcome = await UpdateInstall.download(version: version) { reported in
                Task { @MainActor in percent = max(percent, reported) }
            }

            problem = Self.words(outcome)
            switch outcome.step {
            case .ready: stage = .ready
            case .nothingNewer: stage = .upToDate
            case .failed: stage = .downloadFailed

            // A refusal never offers to try the same file again: the answer is the download page,
            // where a person can look for themselves.
            case .notInstalled, .refused: stage = .cannotInstall
            }
        }
    }

    /// The words for a download that did not end with a version ready to install.
    private static func words(_ outcome: InstallOutcome) -> String? {
        switch outcome.step {
        case .ready, .nothingNewer:
            return nil
        case .notInstalled:
            return Strings.updateNotInstalled
        case .failed:
            return Strings.updateDownloadFailed
        case .refused:
            switch outcome.verdict {
            case .noKeyYet: return Strings.updateNoKeyYet
            case .notListed, .hashMismatch: return Strings.updateBadFile
            default: return Strings.updateBadSignature
            }
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

    // ---- removing Aiko ----

    /// The one thing on this page that cannot be undone, so it asks a second time, says what goes
    /// and, when the app cannot put itself in the Bin, says that before the person agrees.
    private var deleteAiko: some View {
        VStack(alignment: .leading, spacing: 0) {
            if !deleteAsking {
                HStack(spacing: 12) {
                    Control.rowHint(Strings.deleteWhat)
                    Spacer(minLength: 12)
                    FlatButton(title: Strings.delete, look: .ghostDanger) { deleteAsking = true }
                        .padding(.trailing, -10)
                }
            }

            Reveal(isOpen: deleteAsking) {
                VStack(alignment: .leading, spacing: 0) {
                    Text(Strings.format(
                        Strings.deleteLineMac,
                        state.environments.environments.map(\.command).joined(separator: ", ")))
                        .font(Theme.sans(Theme.textSmall))
                        .foregroundStyle(Theme.ink)
                        .fixedSize(horizontal: false, vertical: true)

                    Control.rowHint(Strings.deleteKeepMac).padding(.top, 8)

                    if !Uninstall.canBinTheApp {
                        Control.rowHint(Strings.deleteAppStays).padding(.top, 6)
                    }

                    if state.deleting {
                        Control.rowHint(Strings.deleteWorking).padding(.top, 12)
                    } else {
                        HStack(spacing: 8) {
                            Spacer()
                            FlatButton(title: Strings.cancel, look: .ghost) { deleteAsking = false }
                            FlatButton(title: Strings.delete, look: .danger) { state.deleteAiko() }
                        }
                        .padding(.top, 12)
                    }
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
