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

        Theme.registerFonts()
        shell.show()
        Log.write("started")

        // The self test is asked for by a variable, or by an argument: `open Aiko.app --args
        // --try-island` is the only way to pass anything to an app that is opened, not run.
        if let asked = selfTestAskedFor() {
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

    func applicationWillTerminate(_ notification: Notification) {
        shell.stop()
    }
}
