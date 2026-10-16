import Testing

@testable import AikoKit

/// The twin of ModelSwitchTests.cs: a click on the model and the full id Claude Code names.
struct ModelSwitchTests {
    static let switched =
        #"{"type":"user","message":{"role":"user","content":"<local-command-stdout>Set model to `sonnet (claude-sonnet-5-5)`</local-command-stdout>"}}"#

    @Test(arguments: [
        ("opus", "sonnet"),
        ("sonnet", "haiku"),
        ("haiku", "opus"),
        ("claude-sonnet-5-5", "haiku"),
        ("claude-haiku-5-5-20261001", "opus"),
        ("some-other-model", "opus"),
        ("", "opus"),
    ])
    func aClickOnTheModelTakesTheNextOne(model: String, next: String) {
        #expect(NewSessionChoices.nextModel(model) == next)
    }

    @Test
    func noModelGoesToTheFirstOne() {
        #expect(NewSessionChoices.nextModel(nil) == "opus")
    }

    @Test
    func aNewModelIsAskedAndGoesToClaudeCode() {
        let outcome = Self.ready("claude-opus-5-5").setModel("haiku")

        #expect(outcome.state.askedModel == "haiku")
        #expect(outcome.state.model == "claude-opus-5-5")
        #expect(outcome.state.latestModel == "haiku")
        #expect(outcome.written.count == 1)
        #expect(outcome.written.first?.contains(#""subtype":"set_model""#) == true)
        #expect(outcome.written.first?.contains(#""model":"haiku""#) == true)
    }

    @Test
    func claudeCodeNamesTheFullIdAfterAChange() {
        let asked = Self.ready("claude-opus-5-5").setModel("sonnet").state

        let named = asked.on(StreamEvent.fromLine(Self.switched)).state

        #expect(named.model == "claude-sonnet-5-5")
        #expect(named.askedModel == "")
        #expect(named.items == asked.items)
    }

    @Test
    func theNextStartNamesTheFullIdOfAnAskedModel() {
        let asked = Conversation.empty().setModel("haiku").state

        let named = asked.on(.ready(sessionId: "s1", model: "claude-haiku-5-5", mode: nil, version: "")).state

        #expect(named.model == "claude-haiku-5-5")
        #expect(named.askedModel == "")
    }

    @Test
    func aStartWithoutAModelKeepsTheAskedOne() {
        let asked = Conversation.empty().setModel("haiku").state

        let next = asked.on(.ready(sessionId: "s1", model: "", mode: nil, version: "")).state

        #expect(next.askedModel == "haiku")
    }

    @Test
    func theFormStartsWithTheFamilyOfAKeptFullId() {
        let sessions = TerminalSessions.empty.add(
            id: "a", environment: "Work", folder: "/work", name: "a", model: "claude-sonnet-5-5", mode: .manual, now: 10)

        #expect(NewSessionChoices.defaultModel(sessions, "Work") == "sonnet")
    }

    private static func ready(_ model: String) -> Conversation {
        Conversation.empty().on(.ready(sessionId: "s1", model: model, mode: nil, version: "")).state
    }
}
