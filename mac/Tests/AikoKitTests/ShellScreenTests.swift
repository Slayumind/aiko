import Foundation
import Testing

@testable import AikoKit

/// The screen of a command line and its blocks, from spec/cases/shell-screen (D-412). The twin of
/// ShellScreenTests.cs.
struct ShellScreenTests {
    private static func number(_ node: JsonNode?) -> Int? {
        node?.int64Value.map(Int.init)
    }

    @Test
    func everyRecordedShellGivesTheSameBlocks() throws {
        let recordings = try #require(SpecCases.json("shell-screen")["recordings"]?.objectValue)
        #expect(!recordings.isEmpty)
        for member in recordings.members {
            let recording = SpecCases.json("shell-screen", "\(member.key).json")
            let screen = ShellScreen(
                columns: Int(recording["columns"]?.int64Value ?? 0), rows: Int(recording["rows"]?.int64Value ?? 0))
            screen.feed(recording["stream"]?.stringValue ?? "", now: 0)

            let blocks = ShellBlocks.of(screen)
            let expected = member.value.arrayValue ?? []
            #expect(blocks.count == expected.count, "\(member.key)")
            for (row, block) in zip(expected, blocks) {
                #expect(block.command == row["command"]?.stringValue, "\(member.key)")
                #expect(block.exitCode == Self.number(row["exitCode"]), "\(member.key): \(block.command)")
                #expect(block.lineCount == Self.number(row["lines"]), "\(member.key): \(block.command)")
                if let first = row["first"]?.stringValue {
                    #expect((block.lineCount > 0 ? screen.lineText(block.firstLine) : "") == first, "\(member.key): \(block.command)")
                }
            }

            #expect(!screen.fullScreen)
        }
    }

    @Test
    func everyScreenCaseLaysOutTheSame() {
        for row in SpecCases.json("shell-screen")["screens"]?.arrayValue ?? [] {
            let screen = ShellScreen(columns: Int(row["columns"]?.int64Value ?? 0), rows: 10)
            if let chunks = row["chunks"]?.arrayValue {
                for chunk in chunks {
                    screen.feed(chunk.stringValue ?? "", now: 0)
                }
            } else {
                screen.feed(row["stream"]?.stringValue ?? "", now: 0)
            }

            let lines = (row["lines"]?.arrayValue ?? []).compactMap(\.stringValue)
            #expect((0..<lines.count).map(screen.lineText) == lines, "\(row["name"]?.stringValue ?? "")")
            if let wrapped = row["wrapped"]?.arrayValue {
                #expect((0..<lines.count).map(screen.wrapped) == wrapped.compactMap(\.boolValue))
            }
            if let text = row["text"]?.stringValue {
                #expect(screen.text(fromRow: 0, fromColumn: 0, toRow: lines.count - 1, toColumn: Int.max) == text)
            }
            if let full = row["fullScreen"]?.boolValue {
                #expect(screen.fullScreen == full)
            }
        }
    }

    @Test
    func everyRunCaseKeepsItsColours() {
        for row in SpecCases.json("shell-screen")["runs"]?.arrayValue ?? [] {
            let screen = ShellScreen(columns: 80, rows: 10)
            screen.feed(row["stream"]?.stringValue ?? "", now: 0)

            let expected = (row["runs"]?.arrayValue ?? []).map {
                "\($0["text"]?.stringValue ?? "")|\($0["foreground"]?.int64Value ?? 0)|\($0["bold"]?.boolValue ?? false)"
            }
            #expect(screen.runs(0).map { "\($0.text)|\($0.style.foreground)|\($0.style.bold)" } == expected)
        }
    }

    @Test
    func everyBlockCaseCutsTheSame() {
        for row in SpecCases.json("shell-screen")["blocks"]?.arrayValue ?? [] {
            let screen = ShellScreen(columns: 80, rows: 10)
            screen.feed(row["stream"]?.stringValue ?? "", now: 0)

            let blocks = ShellBlocks.of(screen)
            let expected = row["blocks"]?.arrayValue ?? []
            #expect(blocks.count == expected.count)
            for (want, block) in zip(expected, blocks) {
                #expect(block.command == want["command"]?.stringValue)
                #expect(block.exitCode == Self.number(want["exitCode"]))
                if let timed = want["timed"]?.boolValue {
                    #expect(block.timed == timed)
                }
                #expect((block.firstLine..<(block.firstLine + block.lineCount)).map(screen.lineText)
                    == (want["lines"]?.arrayValue ?? []).compactMap(\.stringValue))
            }
        }
    }

    @Test
    func aBlockKnowsHowLongItsCommandRan() throws {
        let screen = ShellScreen(columns: 80, rows: 10)
        screen.feed("\u{1b}]133;A\u{07}$ \u{1b}]133;B\u{07}sleep 2\r\n\u{1b}]133;C\u{07}", now: 1_000)
        screen.feed("\u{1b}]133;D;0\u{07}", now: 3_000)

        let blocks = ShellBlocks.of(screen)
        #expect(blocks.count == 1)
        let block = try #require(blocks.first)
        #expect(block.startedAt == 1_000)
        #expect(block.endedAt == 3_000)
    }

    @Test
    func tenThousandLinesAreReadInWellUnderASecond() {
        let screen = ShellScreen(columns: 120, rows: 30)
        let stream = (1...10_000).map { "line \($0) with some words in it\r\n" }.joined()
        let started = Date()
        screen.feed(stream, now: 0)

        // A debug build of Swift is slower than the release build of .NET, so the bound here is a
        // second; the Windows twin holds half of it.
        #expect(Date().timeIntervalSince(started) < 1.0)
        #expect(screen.lineText(9_999) == "line 10000 with some words in it")
    }
}
