import Foundation
import Testing

@testable import AikoKit

struct LineBufferTests {
    private static func bytes(_ text: String) -> Data {
        Data(text.utf8)
    }

    @Test
    func wholeLinesComeOutAndTheRestWaits() {
        var buffer = LineBuffer()

        #expect(buffer.take(Self.bytes("one\ntwo\nthr")) == ["one", "two"])
        #expect(buffer.take(Self.bytes("ee\n")) == ["three"])
        #expect(buffer.rest() == nil)
    }

    @Test
    func aLetterCutBetweenTwoPiecesIsNotBroken() {
        let line = Self.bytes("Привет, 🙂\n")
        var buffer = LineBuffer()
        var lines: [String] = []

        // One byte at a time: every letter of more than one byte is cut somewhere.
        for byte in line {
            lines += buffer.take(Data([byte]))
        }

        #expect(lines == ["Привет, 🙂"])
    }

    @Test
    func aCarriageReturnBeforeTheLineBreakIsDropped() {
        var buffer = LineBuffer()

        #expect(buffer.take(Self.bytes("one\r\ntwo\r\n")) == ["one", "two"])
    }

    @Test
    func emptyLinesAreKept() {
        var buffer = LineBuffer()

        #expect(buffer.take(Self.bytes("\n\none\n")) == ["", "", "one"])
    }

    @Test
    func theLastLineWithoutALineBreakComesWhenThePipeCloses() {
        var buffer = LineBuffer()

        #expect(buffer.take(Self.bytes("one\nlast")) == ["one"])
        #expect(buffer.rest() == "last")
        #expect(buffer.rest() == nil)
    }

    @Test
    func aLongLineInManyPiecesComesOutWhole() {
        let text = String(repeating: "я🙂a", count: 200_000)
        let line = Self.bytes(text + "\n")
        var buffer = LineBuffer()
        var lines: [String] = []

        var at = line.startIndex
        while at < line.endIndex {
            let end = min(at + 65_536, line.endIndex)
            lines += buffer.take(line[at..<end])
            at = end
        }

        #expect(lines.count == 1)
        #expect(lines.first == text)
    }
}
