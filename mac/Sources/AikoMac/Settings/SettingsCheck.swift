import AikoKit
import AppKit

/// The self test doors of the settings: `--try-settings <page>` and `--try-wizard <item>`. They
/// open the terminal window at one page of the settings and write down what came out, so a
/// screenshot can be taken on a Mac nobody is clicking. The twins of --snapshot-settings and --snapshot-wizard on Windows,
/// which save a picture; over SSH a picture shows only the wallpaper, so this one holds the window
/// on screen instead and the picture is taken from outside.
///
/// Nothing here writes a setting and nothing is left running: AIKO_TRY_SECONDS says how long the
/// page stays, and then the app quits.
@MainActor
enum SettingsCheck {
    /// "terminal-openai" draws the page of the window with OpenAI chosen, without saving it: the
    /// twin of SettingsScene.VoiceEngine on Windows.
    static var voiceEngine: VoiceEngine?
    static func run(_ what: String, value: String?, shell: AikoShell) {
        let seconds = Double(ProcessInfo.processInfo.environment["AIKO_TRY_SECONDS"] ?? "") ?? 6

        let state = what == "wizard"
            ? shell.openSettings(at: .checklist, item: item(value))
            : shell.openSettings(at: page(value, shell: shell))

        Log.write("self test: settings at \(state.page.key), language \(Strings.language), "
            + "fewer animations: \(Motion.reduceMotion)")
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) {
            Log.write("self test: settings panel \(shell.settingsPanelForCheck)")
        }

        if let checklist = state.checklist {
            Log.write("self test: checklist \(checklist.progress.done) of \(checklist.progress.total) done, "
                + "open item \(checklist.opened.map(String.init(describing:)) ?? "none"), "
                + "finish enabled: \(checklist.canFinish), "
                // A model made outside SettingsState.newChecklist has nobody listening, so Finish
                // writes everything and the page stays where it is.
                + "finish wired: \(checklist.onFinished != nil)")

            if state.firstRun {
                Log.write("self test: first run, one step at a time: open \(checklist.opened.map { "\($0)" } ?? "none"), "
                    + "back to \(checklist.step(-1).map { "\($0)" } ?? "nothing"), "
                    + "next to \(checklist.step(1).map { "\($0)" } ?? "nothing")")
            }

            for item in ChecklistLayout.order {
                Log.write("self test: item \(item) \(checklist.state(of: item)) "
                    + "\"\(ChecklistLayout.status(item, checklist.state(of: item), checklist.words))\"")
            }
        } else {
            for row in state.navRows {
                Log.write("self test: nav \(describe(row))")
            }
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) {
            state.onClose()
            NSApp.terminate(nil)
        }
    }

    /// The page name as the argument spells it, the same keys SettingsPage uses. An unknown name
    /// opens the first environment, so a typo shows something rather than nothing.
    private static func page(_ value: String?, shell: AikoShell) -> SettingsPage? {
        guard let value, !value.isEmpty else { return nil }
        if value == "terminal-openai" {
            voiceEngine = .openAI
            return .terminal
        }
        if let page = SettingsPage.fromKey(value) { return page }

        // "env1" and "env2" spare the caller a whole path on the command line.
        let environments = SettingsNav.ordered(Store.environments(), .macOS, Store.home)
        if value == "env1", let folder = environments.first?.configDirectories.first {
            return .environment(folder)
        }
        if value == "env2", environments.count > 1,
           let folder = environments[1].configDirectories.first {
            return .environment(folder)
        }

        return nil
    }

    private static func item(_ value: String?) -> ChecklistItem? {
        guard let value, !value.isEmpty else { return nil }
        return ChecklistLayout.order.first { "\($0)".lowercased() == value.lowercased() }
    }

    private static func describe(_ row: SettingsNavEntry) -> String {
        switch row {
        case .item(let item): return "item \(item.page.key) \"\(item.title)\" \"\(item.note ?? "")\""
        case .addSecondEnvironment: return "add a second environment"
        case .gap: return "gap"
        }
    }
}
