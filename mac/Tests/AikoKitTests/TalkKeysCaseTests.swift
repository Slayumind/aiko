import Foundation
import Testing

@testable import AikoKit

/// Enter and Esc of the field read from spec/cases, the same table the xUnit tests read.
struct TalkKeysCaseTests {
    @Test
    func everyKeyDoesWhatTheTableSays() {
        for row in SpecCases.json("talk-keys")["keys"]?.arrayValue ?? [] {
            guard let key = row["key"]?.stringValue.flatMap(TalkKey.init(rawValue:)),
                  let expected = row["action"]?.stringValue.flatMap(TalkKeyAction.init(rawValue:)),
                  let card = row["card"]?.boolValue, let empty = row["empty"]?.boolValue,
                  let busy = row["busy"]?.boolValue, let dictating = row["dictating"]?.boolValue,
                  let voice = row["voice"]?.boolValue else {
                Issue.record("unreadable row in talk-keys")
                continue
            }

            let state = TalkKeyState(cardWaits: card, empty: empty, busy: busy, dictating: dictating, voiceActive: voice)
            #expect(TalkKeys.action(key, state) == expected, "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func theKeysAnswerTheCardOfTheTable() {
        for row in SpecCases.json("talk-keys")["cards"]?.arrayValue ?? [] {
            let feed = (row["feed"]?.arrayValue ?? []).enumerated().compactMap { at, item in Self.card(item, at) }
            let expected = row["answers"]?.int64Value.map { "req-\($0)" }

            #expect(TalkKeys.card(feed)?.requestId == expected, "\(row["name"]?.stringValue ?? "")")
        }
    }

    private static func card(_ item: JsonNode, _ at: Int) -> FeedItem? {
        let tool = item["tool"]?.stringValue ?? ""
        let line = #"{"type":"control_request","request_id":"req-\#(at)","request":{"subtype":"can_use_tool","#
            + #""tool_name":"\#(tool)","input":{},"tool_use_id":"toolu_\#(at)"}}"#
        guard case .request(let request) = StreamEvent.fromLine(line) else { return nil }

        let state: RequestState = switch item["state"]?.stringValue {
        case "open": .open
        case "allowed": .allowed
        case "denied": .denied
        default: .answered
        }
        return FeedItem(id: at, content: .card(request: RequestCard.from(request), state: state))
    }
}
