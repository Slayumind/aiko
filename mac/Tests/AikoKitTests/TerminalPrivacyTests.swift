import Foundation
import Testing

@testable import AikoKit

/// The log of Aiko never holds a word of a conversation (D-298). A promise like that is broken by
/// one handy line in the wrong file, so these tests read the source of the terminal.
struct TerminalPrivacyTests {
    private static let sources = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()  // AikoKitTests
        .deletingLastPathComponent()  // Tests
        .deletingLastPathComponent()  // mac
        .appendingPathComponent("Sources")

    private static func text(_ path: String) throws -> String {
        try String(contentsOf: sources.appendingPathComponent(path), encoding: .utf8)
    }

    @Test
    func onlyTheTerminalLogWritesToTheLog() throws {
        var writers: [String] = []
        for folder in ["AikoKit/Terminal", "AikoMac/Terminal"] {
            let files = try FileManager.default.contentsOfDirectory(
                atPath: Self.sources.appendingPathComponent(folder).path)
            for file in files.sorted() where file.hasSuffix(".swift") {
                if try Self.text("\(folder)/\(file)").contains(/\bLog\s*\.\s*write\s*\(/) {
                    writers.append(file)
                }
            }
        }

        #expect(writers == ["TerminalLog.swift"])
    }

    @Test
    func theTerminalLogTakesNoTextButTheNameOfAnEnvironment() throws {
        let seam = try Self.text("AikoKit/Terminal/ConversationProcess.swift")
        let log = try Self.text("AikoMac/Terminal/TerminalLog.swift")
        guard let start = seam.range(of: "protocol TerminalLog") else {
            Issue.record("the protocol of the terminal log was not found")
            return
        }

        var methods = 0
        for source in [String(seam[start.lowerBound...]), log] {
            for method in source.matches(of: /\bfunc\s+(\w+)\s*\(([^)]*)\)/) {
                methods += 1
                for piece in method.2.split(separator: ",") {
                    let parameter = piece.trimmingCharacters(in: .whitespacesAndNewlines)
                    let allowed = parameter.wholeMatch(of: /(\w+\s+)?\w+:\s*(Bool|Int)/) != nil
                        || parameter == "_ environment: String"
                    #expect(
                        allowed,
                        "\(method.1) takes \"\(parameter)\". The terminal log takes numbers, yes or no, and the name of an environment.")
                }
            }
        }

        // Four in the seam, the same four and the six of the doors in the app. Windows has two
        // fewer: it draws the window of --snapshot-terminal into a file and logs nothing.
        #expect(methods == 14)
    }
}
