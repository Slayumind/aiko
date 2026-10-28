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
                row["wave"]?.boolValue ?? false)
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
    func dictatedWordsGoWhereTheCaretStood() {
        for row in SpecCases.json("dictation")["placed"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            let parts = DictationText.around(row["field"]?.stringValue ?? "", caret: Int(row["caret"]?.int64Value ?? 0))
            let placed = DictationText.placed(before: parts.before, words: row["words"]?.stringValue ?? "", after: parts.after)
            #expect(placed.text == row["text"]?.stringValue, "\(name)")
            #expect(placed.caret == Int(row["resultCaret"]?.int64Value ?? -1), "\(name): caret")
        }
    }

    @Test
    func confirmedWordsGrowAndNeverChange() {
        for row in SpecCases.json("dictation")["phrases"]?.arrayValue ?? [] {
            let phrases = DictationText.phrases(done: row["done"]?.stringValue ?? "", reading: row["reading"]?.stringValue ?? "")
            #expect(phrases.confirmedText == row["confirmed"]?.stringValue)
            #expect(phrases.tailText == row["tail"]?.stringValue)
        }

        for run in SpecCases.json("dictation")["runs"]?.arrayValue ?? [] {
            let name = run["name"]?.stringValue ?? ""
            let passes = (run["passes"]?.arrayValue ?? []).compactMap(\.stringValue)
            let steps = run["steps"]?.arrayValue ?? []
            #expect(passes.count == steps.count, "\(name)")

            var text = DictationText.empty
            for (index, pass) in passes.enumerated() where index < steps.count {
                // What was confirmed stays as it was: the next pass only adds after it.
                let before = text.confirmedText
                text = text.pass(pass)
                #expect(text.confirmedText.hasPrefix(before), "\(name), pass \(index + 1) changed confirmed words")
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
                language: row["language"]?.stringValue,
                maxLength: Int(row["max"]?.int64Value ?? 0))

            #expect(hint == row["hint"]?.stringValue, "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["echoes"]?.arrayValue ?? [] {
            #expect(VoiceHint.isEcho(row["phrase"]?.stringValue ?? "", hint: row["hint"]?.stringValue ?? "") == row["echo"]?.boolValue,
                    "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["unechoed"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            let result = VoiceHint.withoutEcho(row["phrase"]?.stringValue ?? "", hint: row["hint"]?.stringValue ?? "")
            #expect(result.text == row["text"]?.stringValue, "\(name)")
            #expect(Int64(result.dropped.count) == row["dropped"]?.int64Value, "\(name)")
            #expect(result.dropped.first.map { Int64($0.inHint) } == row["inHint"]?.int64Value, "\(name)")
        }

        for row in cases["branches"]?.arrayValue ?? [] {
            #expect(VoiceHint.branch(row["head"]?.stringValue) == row["branch"]?.stringValue)
        }

        for row in cases["gitFolders"]?.arrayValue ?? [] {
            #expect(VoiceHint.gitFolder(row["file"]?.stringValue) == row["folder"]?.stringValue)
        }

        for row in cases["languages"]?.arrayValue ?? [] {
            guard let setting = row["setting"]?.stringValue.flatMap(AikoLanguage.init(rawValue:)) else {
                Issue.record("unreadable row in voice-hint")
                continue
            }

            #expect(VoiceHint.language(setting, systemLanguage: row["system"]?.stringValue ?? "") == row["language"]?.stringValue)
        }
    }

    @Test
    func theModelAndTheKeyFollowTheTable() {
        let cases = SpecCases.json("voice-model")
        #expect(cases["url"]?.stringValue == VoiceModel.url)
        #expect(cases["bytes"]?.int64Value == VoiceModel.bytes)
        #expect(cases["sha256"]?.stringValue == VoiceModel.sha256)
        #expect(cases["cloudModel"]?.stringValue == VoiceModel.cloudModel)

        for row in cases["megabytes"]?.arrayValue ?? [] {
            #expect(VoiceModel.megabytes(row["bytes"]?.int64Value ?? 0) == row["megabytes"]?.int64Value)
        }

        for row in cases["readiness"]?.arrayValue ?? [] {
            guard let engine = row["engine"]?.stringValue.flatMap(VoiceEngine.init(rawValue:)),
                  let ready = row["ready"]?.stringValue.flatMap(VoiceReadiness.init(rawValue:)) else {
                Issue.record("unreadable row in voice-model")
                continue
            }

            #expect(VoiceModel.readiness(engine, modelBytes: row["modelBytes"]?.int64Value, keyStored: row["keyStored"]?.boolValue ?? false) == ready)
        }

        for row in cases["keys"]?.arrayValue ?? [] {
            let key = VoiceModel.cleanKey(row["pasted"]?.stringValue)
            #expect(key == row["key"]?.stringValue)
            #expect(key.map(VoiceModel.maskKey) == row["mask"]?.stringValue)
        }
    }

    /// The files of a conversation for the hint, from spec/cases/voice-hint.
    @Test
    func theHintTakesTheFilesAConversationReadOrChanged() {
        for row in SpecCases.json("voice-hint")["files"]?.arrayValue ?? [] {
            var items = [FeedItem(id: 1, content: .ownerMessage(text: "Look at the field.", queued: false, attachments: []))]
            for step in row["steps"]?.arrayValue ?? [] {
                let id = items.count + 1
                let kind: StepKind = switch step["kind"]?.stringValue {
                case "read": .read
                case "search": .search
                case "edit": .edit
                case "run": .run
                default: .other
                }
                let line = StepLine(
                    kind: kind, label: "", subject: step["subject"]?.stringValue ?? "", added: 0, removed: 0,
                    hidden: step["hidden"]?.boolValue ?? false)
                items.append(FeedItem(id: id, content: .step(FeedStep(
                    toolUseId: "t\(id)", line: line, state: .done, result: "", images: [], progress: ""))))
            }

            let files = (row["files"]?.arrayValue ?? []).compactMap(\.stringValue)
            #expect(VoiceHint.files(items) == files, "\(row["name"]?.stringValue ?? "")")
        }
    }

    private static func state(_ node: JsonNode?) -> VoiceState? {
        guard let pair = node?.arrayValue, pair.count == 2,
              let phase = pair[0].stringValue.flatMap(VoicePhase.init(rawValue:)),
              let notice = pair[1].stringValue.flatMap(VoiceNotice.init(rawValue:)) else { return nil }
        return VoiceState(phase, notice)
    }
}
