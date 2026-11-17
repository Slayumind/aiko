import AikoKit
import AppKit
import SwiftUI

/// The small marks of the terminal window, drawn from the same path data as Marks.cs on Windows, so
/// the two windows show the same lines: thin round strokes in a colour of the palette.
enum BarMarks {
    /// The width of every thin line, as Marks.LineWidth.
    static let lineWidth: CGFloat = 0.75

    static let list = "M1.5,1.5 L12.5,1.5 L12.5,11.5 L1.5,11.5 Z M5,1.5 L5,11.5"
    static let chevron = "M0,0 L4,4 L8,0"
    static let search = "M4.5,0.5 A4,4 0 1 1 4.49,0.5 Z M7.6,7.6 L11,11"
    static let pause = "M0.5,0.5 L0.5,9.5 M4.5,0.5 L4.5,9.5"

    /// The sound of a question of the agent: a speaker with two waves (D-396).
    static let sound = "M0.5,3.5 L2.5,3.5 L5,1 L5,9 L2.5,6.5 L0.5,6.5 Z M7,3.3 Q8.1,5 7,6.7 M8.6,1.8 Q10.6,5 8.6,8.2"

    /// check.svg of the project's icon set (SiteIcons.Check).
    static let check = "M23.855,6.353l-13.883,14c-.419,.418-.973,.647-1.562,.647s-1.144-.229-1.561-.646L.152,13.859c-.198-.192-.203-.509-.012-.707,.194-.198,.51-.203,.707-.012l6.703,6.5c.469,.467,1.256,.461,1.713,.006L23.145,5.647c.195-.194,.511-.197,.708-.003,.195,.195,.197,.512,.003,.708Z"

    /// The gear of the project's icon set, in a 24 unit box (GearPath in Tokens.xaml).
    static let gear = "M21.234,14.174l-.445-.274c.14-.64,.211-1.277,.211-1.899s-.071-1.26-.211-1.899l.445-.274c.682-.421,1.16-1.082,1.344-1.862,.185-.779,.054-1.585-.367-2.267-.869-1.407-2.72-1.845-4.128-.978l-.445,.275c-.801-.647-1.685-1.145-2.638-1.481v-.514c0-1.654-1.346-3-3-3s-3,1.346-3,3v.514c-.953,.337-1.837,.834-2.638,1.481l-.445-.275c-1.409-.867-3.26-.43-4.128,.978-.421,.682-.551,1.487-.367,2.267,.185,.78,.662,1.441,1.344,1.862l.445,.274c-.14,.64-.211,1.277-.211,1.899s.071,1.26,.211,1.899l-.445,.274c-.682,.421-1.16,1.082-1.344,1.862-.185,.779-.054,1.585,.367,2.267,.868,1.407,2.721,1.845,4.128,.978l.445-.275c.801,.647,1.685,1.145,2.638,1.481v.514c0,1.654,1.346,3,3,3s3-1.346,3-3v-.514c.953-.337,1.837-.834,2.638-1.481l.445,.275c1.41,.867,3.26,.43,4.128-.978,.421-.682,.551-1.487,.367-2.267-.185-.78-.662-1.441-1.344-1.862Zm.126,3.604c-.58,.938-1.815,1.232-2.752,.651l-.753-.465c-.187-.114-.427-.095-.592,.05-.862,.756-1.841,1.305-2.91,1.634-.21,.064-.353,.258-.353,.478v.875c0,1.103-.897,2-2,2s-2-.897-2-2v-.875c0-.22-.143-.413-.353-.478-1.069-.329-2.048-.878-2.91-1.634-.094-.082-.211-.124-.33-.124-.091,0-.182,.024-.263,.074l-.753,.465c-.938,.581-2.173,.287-2.752-.651-.28-.454-.367-.991-.244-1.511,.123-.521,.441-.961,.896-1.241l.753-.465c.187-.115,.276-.339,.221-.552-.176-.679-.265-1.354-.265-2.009s.089-1.33,.265-2.009c.055-.213-.035-.437-.221-.552l-.753-.465c-.455-.28-.772-.721-.896-1.241-.123-.52-.036-1.057,.244-1.511,.58-.939,1.814-1.232,2.752-.651l.753,.465c.187,.114,.426,.095,.592-.05,.862-.756,1.841-1.305,2.91-1.634,.21-.064,.353-.258,.353-.478v-.875c0-1.103,.897-2,2-2s2,.897,2,2v.875c0,.22,.143,.413,.353,.478,1.069,.329,2.048,.878,2.91,1.634,.166,.145,.406,.164,.592,.05l.753-.465c.938-.581,2.172-.288,2.752,.651,.28,.454,.367,.991,.244,1.511-.123,.521-.441,.961-.896,1.241l-.753,.465c-.187,.115-.276,.339-.221,.552,.176,.679,.265,1.354,.265,2.009s-.089,1.33-.265,2.009c-.055,.213,.035,.437,.221,.552l.753,.465c.455,.28,.772,.721,.896,1.241,.123,.52,.036,1.057-.244,1.511ZM12,8c-2.206,0-4,1.794-4,4s1.794,4,4,4,4-1.794,4-4-1.794-4-4-4Zm0,7c-1.654,0-3-1.346-3-3s1.346-3,3-3,3,1.346,3,3-1.346,3-3,3Z"

    /// The steps of a path as a SwiftUI path, in the units of its data.
    static func path(_ data: String) -> Path {
        var path = Path()
        for step in SvgPath.steps(data) {
            switch step {
            case .move(let to):
                path.move(to: CGPoint(x: to.x, y: to.y))
            case .line(let to):
                if path.isEmpty { path.move(to: CGPoint(x: to.x, y: to.y)) } else { path.addLine(to: CGPoint(x: to.x, y: to.y)) }
            case .curve(let first, let second, let to):
                path.addCurve(
                    to: CGPoint(x: to.x, y: to.y),
                    control1: CGPoint(x: first.x, y: first.y),
                    control2: CGPoint(x: second.x, y: second.y))
            case .close:
                path.closeSubpath()
            }
        }
        return path
    }
}

/// A mark of thin round lines at the size of its data, as Marks.Line: nothing is stretched.
struct LineMark: View {
    let data: String
    var tone: Color = Theme.muted

    var body: some View {
        let path = BarMarks.path(data)
        let box = path.boundingRect
        path
            .offsetBy(dx: -box.minX, dy: -box.minY)
            .stroke(tone, style: StrokeStyle(lineWidth: BarMarks.lineWidth, lineCap: .round, lineJoin: .round))
            .frame(width: box.width, height: box.height)
    }
}

/// A filled mark stretched into a square box, as a Path with Stretch.Uniform.
struct FilledMark: View {
    let data: String
    let size: CGFloat
    var tone: Color = Theme.muted

    var body: some View {
        let path = BarMarks.path(data)
        let box = path.boundingRect
        let scale = size / max(box.width, box.height)
        path
            .offsetBy(dx: -box.minX, dy: -box.minY)
            .applying(CGAffineTransform(scaleX: scale, y: scale))
            .fill(tone)
            .frame(width: box.width * scale, height: box.height * scale)
    }
}

/// The ring of an environment in a pill or a header column, as Marks.Ring: the grey track, and the
/// arc of the percentage over it. Nothing known yet is the track alone.
struct RingMark: View {
    let percent: Int?
    let tone: LimitTone
    var size: CGFloat = 14
    var thickness: CGFloat = 2

    var body: some View {
        ZStack {
            Circle()
                .inset(by: thickness / 2)
                .stroke(Theme.colour(RingArt.track), lineWidth: thickness)
            if let percent, percent > 0 {
                Circle()
                    .inset(by: thickness / 2)
                    .trim(from: 0, to: min(1, Double(percent) / 100))
                    .stroke(Theme.tone(tone), style: StrokeStyle(lineWidth: thickness, lineCap: .round))
                    .rotationEffect(.degrees(-90))
            }
        }
        .frame(width: size, height: size)
    }
}
