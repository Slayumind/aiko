import Foundation
import Testing

@testable import AikoKit

struct FaceArtTests {
    static func everyFace() -> [(FaceStyle, AikoFace, FaceGround, Bool)] {
        var data: [(FaceStyle, AikoFace, FaceGround, Bool)] = []
        for style in FaceStyle.allCases {
            for face in AikoFace.allCases {
                for ground in FaceGround.allCases {
                    data.append((style, face, ground, false))
                    data.append((style, face, ground, true))
                }
            }
        }
        return data
    }

    static let palette: Set<String> = [
        FaceArt.ink, FaceArt.paper, FaceArt.muted, FaceArt.caution, FaceArt.pink, FaceArt.drop, FaceArt.cautionOnLight,
    ]

    // Only the commands the mockup used, with plain numbers: a comma for a decimal point from a
    // Russian system would break every face.
    static let pathData = try! NSRegularExpression(pattern: "^[MLHVCQTAZ0-9 .\\-]+$")

    private func matchesPathData(_ text: String) -> Bool {
        Self.pathData.firstMatch(in: text, range: NSRange(location: 0, length: (text as NSString).length)) != nil
    }

    @Test(arguments: FaceArtTests.everyFace())
    func everyFaceIsDrawnInThePaletteWithReadablePathData(each: (FaceStyle, AikoFace, FaceGround, Bool)) {
        let (style, face, ground, small) = each
        let picture = FaceArt.draw(style, face, ground, small)
        let shapes = picture.groups.flatMap(\.shapes)

        #expect(!shapes.isEmpty)
        for shape in shapes {
            #expect(matchesPathData(shape.data), "path data: \(shape.data)")
            for hole in shape.holes ?? [] {
                #expect(matchesPathData(hole), "hole: \(hole)")
            }
            #expect(shape.fill != nil || shape.stroke != nil)
            #expect(shape.fill == nil || Self.palette.contains(shape.fill!), "\(shape.fill ?? "")")
            #expect(shape.stroke == nil || Self.palette.contains(shape.stroke!), "\(shape.stroke ?? "")")
            #expect(shape.opacity >= 0.1 && shape.opacity <= 1)
        }
    }

    @Test
    func theChibiHeadHasAnOutlineOnlyOnALightBackground() {
        let dark = FaceArt.draw(.chibi, .fresh, .dark, true).groups[0].shapes[0]
        let light = FaceArt.draw(.chibi, .fresh, .light, true).groups[0].shapes[0]

        #expect(dark.stroke == nil)
        #expect(light.stroke == FaceArt.ink)
        #expect(light.strokeWidth == 3.26)
    }

    @Test
    func aSmallChibiUsesThickerLinesAndATighterView() {
        let big = FaceArt.draw(.chibi, .done, .dark, false)
        let small = FaceArt.draw(.chibi, .done, .dark, true)

        #expect(big.viewLeft == 0 && big.viewTop == 0 && big.viewSize == 64)
        #expect(small.viewLeft == 3 && small.viewTop == 1 && small.viewSize == 59)
        #expect(small.groups[1].shapes[0].strokeWidth > big.groups[1].shapes[0].strokeWidth)
    }

    @Test
    func emojiHighlightsAreHolesSoTheBackgroundShowsThrough() {
        let eyes = FaceArt.draw(.emoji, .fresh, .dark, true).groups[0].shapes
            .filter { ($0.holes?.count ?? 0) > 0 }

        #expect(eyes.count == 2)
        for eye in eyes {
            #expect(eye.fill == FaceArt.paper)
        }
    }

    @Test(arguments: [
        (AikoFace.tired, FaceArt.drop),
        (AikoFace.error, FaceArt.drop),
        (AikoFace.waiting, FaceArt.caution),
    ])
    func signsSitOutsideTheFaceGroup(face: AikoFace, color: String) {
        let picture = FaceArt.draw(.chibi, face, .dark, true)

        #expect(picture.groups[picture.groups.count - 1].shapes.contains { $0.fill == color })
        #expect(!picture.groups[1].shapes.contains { $0.fill == color })
    }

    @Test
    func aSmallEmojiDrawsThickerLinesToo() {
        let big = FaceArt.draw(.emoji, .fresh, .dark, false).groups[0].shapes.filter { $0.stroke != nil }
        let small = FaceArt.draw(.emoji, .fresh, .dark, true).groups[0].shapes.filter { $0.stroke != nil }

        #expect(big.count == small.count)
        for (first, second) in zip(big, small) {
            #expect(second.strokeWidth > first.strokeWidth)
        }
    }
}
