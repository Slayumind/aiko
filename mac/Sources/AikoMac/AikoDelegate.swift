import AikoKit
import AppKit

@MainActor
final class AikoDelegate: NSObject, NSApplicationDelegate {
    private let shell = AikoShell()

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Cut, Copy and Paste in every field come from the main menu, even though Aiko shows none.
        AppMenu.install()

        Strings.language = SessionReminder.isRussian(
            Store.settings().language, systemLanguageTag: Locale.preferredLanguages.first)
            ? .russian
            : .english
        // A snapshot in the other language, as AIKO_TRY_LANGUAGE on Windows: the setting stays.
        switch ProcessInfo.processInfo.environment["AIKO_TRY_LANGUAGE"] {
        case "English": Strings.language = .english
        case "Russian": Strings.language = .russian
        default: break
        }

        // The self test is asked for by a variable, or by an argument: `open Aiko.app --args
        // --try-island` is the only way to pass anything to an app that is opened, not run.
        let asked = selfTestAskedFor()

        Theme.registerFonts()
        shell.show(changingNothing: asked != nil)
        Log.write("started")

        if let asked {
            shell.selfTest(asked.what, value: asked.value)
        }
    }

    /// `--try-settings general` and `--try-wizard commands` take a value; the island doors take
    /// none. A value that starts with a dash belongs to the next door, not to this one.
    private func selfTestAskedFor() -> (what: String, value: String?)? {
        if let asked = ProcessInfo.processInfo.environment["AIKO_MAC_SELF_TEST"], !asked.isEmpty {
            return (asked, ProcessInfo.processInfo.environment["AIKO_TRY_VALUE"])
        }

        let arguments = ProcessInfo.processInfo.arguments
        for (at, argument) in arguments.enumerated() where argument.hasPrefix("--try-") {
            let next = arguments.count > at + 1 ? arguments[at + 1] : nil
            return (String(argument.dropFirst("--try-".count)),
                    next?.hasPrefix("-") == false ? next : nil)
        }

        return nil
    }

    /// With programs running in the window Aiko asks first and quits only after a yes (D-351).
    /// The end of the Mac session does not wait for that answer, as the end of a Windows session
    /// does not: log out, restart and shut down quit at once.
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if Self.macSessionEnds() {
            shell.endForSessionEnd()
            return .terminateNow
        }
        return shell.mayQuit { yes in sender.reply(toApplicationShouldTerminate: yes) }
    }

    /// The quit event of log out, restart or shut down names its reason.
    private static func macSessionEnds() -> Bool {
        guard let event = NSAppleEventManager.shared().currentAppleEvent,
              let reason = event.attributeDescriptor(forKeyword: code("why?"))?.enumCodeValue else { return false }
        return ["logo", "rlgo", "rrst", "rest", "rsdn", "shut"].map(code).contains(reason)
    }

    private static func code(_ text: String) -> OSType {
        text.utf8.reduce(0) { $0 << 8 | OSType($1) }
    }

    /// Aiko started again while it runs, from Spotlight, Launchpad or the Finder (D-419).
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        shell.openAskedByAnotherStart()
        return false
    }

    func applicationWillTerminate(_ notification: Notification) {
        shell.stop()
    }
}
