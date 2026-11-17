import Foundation
import Testing

@testable import AikoKit

/// The log of Aiko never holds a word of a conversation (D-298). A promise like that is broken by one
/// handy line in the wrong file, so these tests read the source of the terminal. The twin of
/// TerminalPrivacyTests.cs.
///
/// The self test doors (`*Check.swift`) are left out: they run only with a `--try-*` argument, never
/// in a session of a person.
struct TerminalPrivacyTests {
    @Test
    func onlyTheTerminalLogWritesToTheLog() throws {
        let writers = try terminalSources()
            .filter { try String(contentsOf: $0, encoding: .utf8).contains("Log.write(") }
            .map(\.lastPathComponent)

        #expect(writers == ["TerminalLog.swift"])
    }

    @Test
    func theTerminalLogTakesNoTextButTheNameOfAnEnvironment() throws {
        let file = SpecCases.repository().appendingPathComponent("mac/Sources/AikoMac/Terminal/TerminalLog.swift")
        let source = try String(contentsOf: file, encoding: .utf8)
        let functions = try Regex(#"static func (\w+)\(([^)]*)\)"#)

        var count = 0
        for match in source.matches(of: functions) {
            count += 1
            let name = match.output[1].substring.map(String.init) ?? ""
            let parameters = (match.output[2].substring.map(String.init) ?? "")
                .split(separator: ",")
                .map { $0.trimmingCharacters(in: .whitespaces) }
                .filter { !$0.isEmpty }
            for parameter in parameters {
                let allowed = parameter == "environment: String"
                    || parameter.hasSuffix(": Bool")
                    || parameter.hasSuffix(": Int")
                    || parameter.hasSuffix(": Int?")
                #expect(allowed, "\(name) takes \"\(parameter)\". The terminal log takes numbers, yes or no, and the name of an environment.")
            }
        }

        // started, exited, loginShellNotStarted.
        #expect(count == 3)
    }

    private func terminalSources() throws -> [URL] {
        let root = SpecCases.repository()
        return try ["mac/Sources/AikoMac/Terminal", "mac/Sources/AikoKit/Terminal"].flatMap { folder in
            try FileManager.default.contentsOfDirectory(
                at: root.appendingPathComponent(folder), includingPropertiesForKeys: nil)
        }
        .filter { $0.pathExtension == "swift" && !$0.lastPathComponent.hasSuffix("Check.swift") }
    }
}
