import Testing

@testable import AikoKit

/// The update line on the General page offers one action, the next step from where it stands.
struct UpdateBlockTests {
    @Test(arguments: [
        (UpdateStage.notAsked, UpdateAction.check),
        (.asking, .none),
        (.upToDate, .check),
        (.available, .install),
        (.checkFailed, .check),
        (.downloading, .none),
        (.ready, .restart),
        (.downloadFailed, .install),
        (.cannotInstall, .openDownloadPage),
    ])
    func everyStageHasOneAction(stage: UpdateStage, action: UpdateAction) {
        #expect(UpdateBlock.actionFor(stage) == action)
    }

    @Test(arguments: [
        (UpdateState.available, UpdateStage.available),
        (.upToDate, .upToDate),
        (.unknown, .checkFailed),
    ])
    func anAnswerWeCannotReadIsAFailedCheckNeverUpToDate(state: UpdateState, stage: UpdateStage) {
        #expect(UpdateBlock.after(state) == stage)
    }

    @Test(arguments: [(-5, 0), (0, 0), (64, 64), (100, 100), (140, 100)])
    func thePercentStaysBetween0And100(reported: Int, shown: Int) {
        #expect(UpdateBlock.percent(reported) == shown)
    }
}
