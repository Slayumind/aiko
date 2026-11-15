import Testing

@testable import AikoKit

/// The keys of Aiko are Cmd on the Mac (D-425). The words come from the same list as on Windows, so
/// tools/strings.py turns every Ctrl+ into Cmd+ for the Mac; a word that still says Ctrl+ would send
/// the person to a key that does nothing.
struct MacKeysTests {
    @Test func noWordNamesCtrl() {
        let left = Strings.table.filter { $0.value.english.contains("Ctrl+") || $0.value.russian.contains("Ctrl+") }
        #expect(left.isEmpty, "\(left.keys.sorted())")
    }

    @Test func theKeysSayCmd() {
        #expect(Strings.table["TerminalNewSessionKey"]?.english == "Cmd+Shift+T")
        #expect(Strings.table["TerminalFindConversation"]?.russian == "Поиск сессий · Cmd+Shift+K")
    }
}
