import AikoKit
import AppKit

@MainActor
final class AikoDelegate: NSObject, NSApplicationDelegate {
    private let shell = AikoShell()

    func applicationDidFinishLaunching(_ notification: Notification) {
        Strings.language = SessionReminder.isRussian(
            Store.settings().language, systemLanguageTag: Locale.preferredLanguages.first)
            ? .russian
            : .english

        Theme.registerFonts()
        shell.show()
        Log.write("started")

        // The self test is asked for by a variable, or by an argument: `open Aiko.app --args
        // --try-island` is the only way to pass anything to an app that is opened, not run.
        if let what = selfTestAskedFor() {
            shell.selfTest(what)
        }
    }

    private func selfTestAskedFor() -> String? {
        if let asked = ProcessInfo.processInfo.environment["AIKO_MAC_SELF_TEST"], !asked.isEmpty {
            return asked
        }

        for argument in ProcessInfo.processInfo.arguments where argument.hasPrefix("--try-") {
            return String(argument.dropFirst("--try-".count))
        }

        return nil
    }

    func applicationWillTerminate(_ notification: Notification) {
        shell.stop()
    }
}
