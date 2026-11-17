import Testing

@testable import AikoKit

/// Every way out that stops the sessions asks with words of its own (D-351). The twin of
/// StopQuestionTests.cs.
struct StopQuestionTests {
    @Test
    func everyReasonHasItsOwnWords() {
        let questions = StopReason.allCases.map(StopQuestion.of)

        #expect(Set(questions.map(\.body)).count == questions.count)
        #expect(questions.allSatisfy { $0.stop != $0.keep })
        #expect(questions.allSatisfy { $0.title == Strings.terminalQuitTitle })
    }

    @Test
    func onlyRemovingOneEnvironmentLeavesTheOtherSessions() {
        #expect(StopReason.allCases.filter { !StopQuestion.endsEverySession($0) } == [.removeEnvironment])
    }

    @Test
    func quitKeepsTheWordsItHad() {
        let quit = StopQuestion.of(.quit)

        #expect(quit.body == Strings.terminalQuitBody)
        #expect(quit.stop == Strings.terminalStopAndQuit)
        #expect(quit.keep == Strings.terminalDontQuit)
    }
}
