import Foundation
import Testing

@testable import AikoKit

/// Voice input read from spec/cases, the same tables the xUnit tests read.
struct VoiceCaseTests {
    @Test
    func everyPressAndCueLeadsWhereTheTableSays() {
        for row in SpecCases.json("voice-input")["next"]?.arrayValue ?? [] {
            guard let from = Self.state(row["from"]), let to = Self.state(row["to"]),
                  let cue = row["cue"]?.stringValue.flatMap(VoiceCue.init(rawValue:)),
                  let ready = row["ready"]?.stringValue.flatMap(VoiceReadiness.init(rawValue:)) else {
                Issue.record("unreadable row in voice-input")
                continue
            }

            #expect(VoiceInput.next(from, cue, ready) == to, "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func everyPhaseShowsTheLineAndButtonOfTheTable() {
        for row in SpecCases.json("voice-input")["lines"]?.arrayValue ?? [] {
            guard let phase = row["phase"]?.stringValue.flatMap(VoicePhase.init(rawValue:)),
                  let engine = row["engine"]?.stringValue.flatMap(VoiceEngine.init(rawValue:)),
                  let word = row["word"]?.stringValue.flatMap(VoiceWord.init(rawValue:)),
                  let button = row["button"]?.stringValue.flatMap(VoiceButton.init(rawValue:)) else {
                Issue.record("unreadable row in voice-input")
                continue
            }

            let expected = VoiceLine(
                word,
                row["time"]?.stringValue,
                row["where"]?.stringValue.flatMap(VoiceEngine.init(rawValue:)),
                row["cancel"]?.boolValue ?? false,
                button,
                row["ring"]?.boolValue ?? false)
            let line = VoiceInput.line(VoiceState(phase, .none), listened: row["seconds"]?.doubleValue ?? 0, engine: engine)

            #expect(line == expected)
        }
    }

    @Test
    func theClockCountsMinutesAndSeconds() {
        for row in SpecCases.json("voice-input")["clock"]?.arrayValue ?? [] {
            #expect(VoiceInput.clock(row["seconds"]?.doubleValue ?? 0) == row["text"]?.stringValue)
        }
    }

    @Test
    func confirmedWordsGrowAndNeverChange() {
        for run in SpecCases.json("dictation")["runs"]?.arrayValue ?? [] {
            let name = run["name"]?.stringValue ?? ""
            let passes = (run["passes"]?.arrayValue ?? []).compactMap(\.stringValue)
            let steps = run["steps"]?.arrayValue ?? []
            #expect(passes.count == steps.count, "\(name)")

            var text = DictationText.empty
            for (index, pass) in passes.enumerated() where index < steps.count {
                text = text.pass(pass)
                #expect(text.confirmedText == steps[index]["confirmed"]?.stringValue, "\(name), pass \(index + 1)")
                #expect(text.tailText == steps[index]["tail"]?.stringValue, "\(name), pass \(index + 1)")
            }

            if let final = run["final"]?.stringValue {
                text = text.final(final)
                #expect(text.tailText == "")
            }

            #expect(text.text == run["text"]?.stringValue, "\(name)")
        }
    }

    @Test
    func theHintNamesTheFolderBranchFilesAndKeys() {
        let cases = SpecCases.json("voice-hint")
        for row in cases["hints"]?.arrayValue ?? [] {
            let hint = VoiceHint.build(
                folder: row["folder"]?.stringValue,
                branch: row["branch"]?.stringValue,
                files: (row["files"]?.arrayValue ?? []).compactMap(\.stringValue),
                memoryKeys: row["keys"]?.arrayValue?.compactMap(\.stringValue),
                maxLength: Int(row["max"]?.int64Value ?? 0))

            #expect(hint == row["hint"]?.stringValue, "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["languages"]?.arrayValue ?? [] {
            guard let setting = row["setting"]?.stringValue.flatMap(AikoLanguage.init(rawValue:)) else {
                Issue.record("unreadable row in voice-hint")
                continue
            }

            #expect(VoiceHint.language(setting, systemLanguage: row["system"]?.stringValue ?? "") == row["language"]?.stringValue)
        }
    }

    private static func state(_ node: JsonNode?) -> VoiceState? {
        guard let pair = node?.arrayValue, pair.count == 2,
              let phase = pair[0].stringValue.flatMap(VoicePhase.init(rawValue:)),
              let notice = pair[1].stringValue.flatMap(VoiceNotice.init(rawValue:)) else { return nil }
        return VoiceState(phase, notice)
    }
}
