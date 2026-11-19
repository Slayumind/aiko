import Testing
@testable import AikoKit

/// /clear starts a new conversation in the same session (D-453).
struct SessionConversationTests {
    private let first = "5f0c2a8e-1b7d-4c1e-9f3a-2d6b8e4a7c10"
    private let second = "7a1d3c52-9e04-4b6f-8c21-0f3e5d7a9b64"

    private func one() -> TerminalSessions {
        var session = TerminalSession(id: first, environment: "Aiko", folder: "/work", name: "work")
        session.started = true
        return TerminalSessions(sessions: [session], lastId: first)
    }

    @Test func theSessionRemembersTheConversationAcrossTheFile() {
        let read = TerminalSessions.fromJson(one().moveToConversation(first, second).toJson())
        #expect(read.find(first)?.resumeId == second)
        #expect(read.ownerOfConversation(second)?.id == first)
    }

    @Test func anEmptyIdOrTheSameOneChangesNothing() {
        #expect(one().moveToConversation(first, "") == one())
        #expect(one().moveToConversation(first, first) == one())
    }
}
