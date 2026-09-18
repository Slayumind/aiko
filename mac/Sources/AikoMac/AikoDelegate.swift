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

        if ProcessInfo.processInfo.environment["AIKO_MAC_SELF_TEST"] == "1" {
            shell.selfTest()
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        shell.stop()
    }
}
