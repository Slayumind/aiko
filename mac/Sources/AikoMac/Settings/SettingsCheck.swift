import AikoKit
import AppKit

/// The self test doors of the settings window: `--try-settings <page>` and `--try-wizard <item>`.
/// They open the window at one page and write down what came out, so a screenshot can be taken on
/// a Mac nobody is clicking. The twins of --snapshot-settings and --snapshot-wizard on Windows,
/// which save a picture; over SSH a picture shows only the wallpaper, so this one holds the window
/// on screen instead and the picture is taken from outside.
///
/// Nothing here writes a setting and nothing is left running: AIKO_TRY_SECONDS says how long the
/// window stays, and then the app quits.
@MainActor
enum SettingsCheck {
    static func run(_ what: String, value: String?, shell: AikoShell) {
        let seconds = Double(ProcessInfo.processInfo.environment["AIKO_TRY_SECONDS"] ?? "") ?? 6

        let window = what == "wizard"
            ? shell.openSettings(at: .checklist, item: item(value))
            : shell.openSettings(at: page(value, shell: shell))

        // AIKO_TRY_GROW makes the window as tall as the page; "end" keeps its bottom on screen
        // when the page is taller than the screen itself.
        if let grow = ProcessInfo.processInfo.environment["AIKO_TRY_GROW"] {
            window.show()
            window.growToPage(showingTheEnd: grow == "end")
        }

        Log.write("self test: settings at \(window.state.page.key), "
            + "window \(size(window.frame)) at \(place(window.frame)), "
            + "panel \(size(window.panelFrame)) at \(place(window.panelFrame)), "
            + "language \(Strings.language), fewer animations: \(Motion.reduceMotion)")

        if let checklist = window.state.checklist {
            Log.write("self test: checklist \(checklist.progress.done) of \(checklist.progress.total) done, "
                + "open item \(checklist.opened.map(String.init(describing:)) ?? "none"), "
                + "finish enabled: \(checklist.canFinish)")

            for item in ChecklistLayout.order {
                Log.write("self test: item \(item) \(checklist.state(of: item)) "
                    + "\"\(ChecklistLayout.status(item, checklist.state(of: item), checklist.words))\"")
            }
        } else {
            for row in window.state.navRows {
                Log.write("self test: nav \(describe(row))")
            }
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) {
            window.close()
            NSApp.terminate(nil)
        }
    }

    /// The page name as the argument spells it, the same keys SettingsPage uses. An unknown name
    /// opens the first environment, so a typo shows something rather than nothing.
    private static func page(_ value: String?, shell: AikoShell) -> SettingsPage? {
        guard let value, !value.isEmpty else { return nil }
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
        case .label(let text): return "label \"\(text)\""
        case .item(let item): return "item \(item.page.key) \"\(item.title)\" \"\(item.note ?? "")\""
        case .addSecondEnvironment: return "add a second environment"
        case .separator: return "separator"
        }
    }

    private static func size(_ rect: NSRect) -> String {
        "\(Int(rect.width))x\(Int(rect.height))"
    }

    private static func place(_ rect: NSRect) -> String {
        let box = Screens.box(rect)
        return "\(Int(box.x)),\(Int(box.y))"
    }
}
