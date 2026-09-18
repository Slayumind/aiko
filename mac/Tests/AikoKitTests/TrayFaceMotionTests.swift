import Testing

@testable import AikoKit

struct TrayFaceMotionTests {
    private func near(_ expected: IconFrame, _ actual: IconFrame) {
        expectClose(expected.ringScale, actual.ringScale, places: 3)
        expectClose(expected.ringOpacity, actual.ringOpacity, places: 3)
        expectClose(expected.ringSweep, actual.ringSweep, places: 3)
        expectClose(expected.faceScale, actual.faceScale, places: 3)
        expectClose(expected.faceOpacity, actual.faceOpacity, places: 3)
    }

    @Test
    func eachStepEndsWhereTheNextOneStarts() {
        near(IconFrame(0.6, 0, 1, 0.6, 0), TrayFaceMotion.at(.ringsOut, 1))
        near(IconFrame(0.6, 0, 1, 1, 1), TrayFaceMotion.at(.faceIn, 1))
        near(IconFrame(0.6, 0, 1, 0.8, 0), TrayFaceMotion.at(.faceOut, 1))
        near(IconFrame.rings, TrayFaceMotion.at(.ringsBack, 1))
    }

    @Test
    func theRingsStartWholeAndComeBackWhole() {
        near(IconFrame.rings, TrayFaceMotion.at(.ringsOut, 0))
        #expect(TrayFaceMotion.at(.ringsBack, 0).ringSweep == 0)
    }

    @Test
    func theFaceSpringsALittlePastItsSize() throws {
        let sizes = (0...100).map { TrayFaceMotion.at(.faceIn, Double($0) / 100.0).faceScale }
        let largest = try #require(sizes.max())

        #expect(largest > 1)
        #expect(largest < 1.1)
    }

    @Test
    func eightFramesAStepAndTheLastOneAtTheEnd() {
        for phase in FacePhase.allCases {
            let frames = TrayFaceMotion.frames(phase)

            #expect(frames.count == TrayFaceMotion.framesPerStep)
            near(TrayFaceMotion.at(phase, 1), frames[frames.count - 1])
        }
    }

    @Test
    func theIslandShrinksToTheFaceWhileTheRingsGoAndGrowsBackWithThem() {
        #expect(TrayFaceMotion.islandFit(.ringsOut, 0) == 0)
        #expect(TrayFaceMotion.islandFit(.ringsOut, 1) == 1)
        #expect(TrayFaceMotion.islandFit(.faceIn, 0.3) == 1)
        #expect(TrayFaceMotion.islandFit(.faceOut, 0.7) == 1)
        #expect(TrayFaceMotion.islandFit(.ringsBack, 1) == 0)
        expectClose(0.5, TrayFaceMotion.islandFit(.ringsBack, 0.5), places: 6)
    }

    @Test
    func theFaceIsFullyThere400MsAfterTheEvent() {
        #expect(TrayFaceMotion.arrival == 0.4)
    }

    @Test
    func aFaceAlreadyShownOnlyComesInAgain() {
        #expect(TrayFaceMotion.toFace(faceShown: false) == [.ringsOut, .faceIn])
        #expect(TrayFaceMotion.toFace(faceShown: true) == [.faceIn])
        #expect(TrayFaceMotion.toRings(faceShown: true) == [.faceOut, .ringsBack])
        #expect(TrayFaceMotion.toRings(faceShown: false) == [.ringsBack])
    }
}
