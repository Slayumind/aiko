import Foundation

/// What the face sits on: the dark menu bar and Aiko's windows, or a light one.
public enum FaceGround: Sendable, Equatable, CaseIterable {
    case dark
    case light
}

/// One filled or stroked outline. Data is SVG path data, which the drawing layer reads as it is.
/// Holes are cut out of the fill, so a highlight stays transparent on any background.
public struct FaceShape: Sendable, Equatable {
    public let data: String
    public let fill: String?
    public let stroke: String?
    public let strokeWidth: Double
    public let opacity: Double
    public let holes: [String]?

    public init(
        _ data: String,
        fill: String? = nil,
        stroke: String? = nil,
        strokeWidth: Double = 0,
        opacity: Double = 1,
        holes: [String]? = nil
    ) {
        self.data = data
        self.fill = fill
        self.stroke = stroke
        self.strokeWidth = strokeWidth
        self.opacity = opacity
        self.holes = holes
    }
}

/// Shapes that share one transform: scaled around a centre, then moved.
public struct FaceGroup: Sendable, Equatable {
    public let shapes: [FaceShape]
    public let scale: Double
    public let centerX: Double
    public let centerY: Double
    public let offsetX: Double
    public let offsetY: Double

    public init(
        _ shapes: [FaceShape],
        scale: Double = 1,
        centerX: Double = 0,
        centerY: Double = 0,
        offsetX: Double = 0,
        offsetY: Double = 0
    ) {
        self.shapes = shapes
        self.scale = scale
        self.centerX = centerX
        self.centerY = centerY
        self.offsetX = offsetX
        self.offsetY = offsetY
    }
}

/// A face in a square view box, drawn in order.
public struct FacePicture: Sendable, Equatable {
    public let viewLeft: Double
    public let viewTop: Double
    public let viewSize: Double
    public let groups: [FaceGroup]

    public init(viewLeft: Double, viewTop: Double, viewSize: Double, groups: [FaceGroup]) {
        self.viewLeft = viewLeft
        self.viewTop = viewTop
        self.viewSize = viewSize
        self.groups = groups
    }
}

/// Aiko's chibi face as shapes (D-213, D-214, D-462).
/// A port of the accepted mockup generators, with the same numbers, so the app and the mockup match.
/// The core knows no drawing framework; the app turns these shapes into a picture.
public enum FaceArt {
    /// The palette of D-213: the eight colours of DESIGN plus the blue drop.
    public static let ink = "#0A0A0A"
    public static let paper = "#FAFAFA"
    public static let muted = "#A1A1A1"
    public static let caution = "#FE9A00"
    public static let pink = "#FF6467"
    public static let drop = "#33A9EE"

    /// Below this size the chibi draws with thicker lines and a tighter view box.
    public static let smallUpTo = 48.0

    public static func draw(_ face: AikoFace, _ ground: FaceGround, _ small: Bool) -> FacePicture {
        Chibi.draw(face, ground, small)
    }

    /// Two places, the invariant way: a comma for a decimal point from a Russian system would break
    /// every face.
    static func n(_ value: Double) -> String {
        let rounded = round2(value)
        if rounded == rounded.rounded() && abs(rounded) < 1e15 {
            return rounded == 0 && rounded.sign == .minus ? "-0" : String(Int64(rounded))
        }
        return String(rounded)
    }

    static func round2(_ value: Double) -> Double {
        (value * 100).rounded(.toNearestOrEven) / 100
    }

    static func circle(_ x: Double, _ y: Double, _ r: Double) -> String {
        "M\(n(x - r)) \(n(y)) A\(n(r)) \(n(r)) 0 1 0 \(n(x + r)) \(n(y)) A\(n(r)) \(n(r)) 0 1 0 \(n(x - r)) \(n(y)) Z"
    }

    static func ellipse(_ x: Double, _ y: Double, _ rx: Double, _ ry: Double) -> String {
        "M\(n(x - rx)) \(n(y)) A\(n(rx)) \(n(ry)) 0 1 0 \(n(x + rx)) \(n(y)) A\(n(rx)) \(n(ry)) 0 1 0 \(n(x - rx)) \(n(y)) Z"
    }

    static func roundRect(_ x: Double, _ y: Double, _ w: Double, _ h: Double, _ r: Double) -> String {
        "M\(n(x + r)) \(n(y)) H\(n(x + w - r)) A\(n(r)) \(n(r)) 0 0 1 \(n(x + w)) \(n(y + r)) V\(n(y + h - r)) "
            + "A\(n(r)) \(n(r)) 0 0 1 \(n(x + w - r)) \(n(y + h)) H\(n(x + r)) A\(n(r)) \(n(r)) 0 0 1 \(n(x)) \(n(y + h - r)) "
            + "V\(n(y + r)) A\(n(r)) \(n(r)) 0 0 1 \(n(x + r)) \(n(y)) Z"
    }

    static func line(_ data: String, _ width: Double, _ color: String, _ opacity: Double = 1) -> FaceShape {
        FaceShape(data, stroke: color, strokeWidth: round2(width), opacity: opacity)
    }

    private enum Chibi {
        static let head = "M32 7 C48 7 58 18.5 58 34 C58 49 46.5 58 32 58 C17.5 58 6 49 6 34 C6 18.5 16 7 32 7 Z"
        static let l = 22.0
        static let r = 42.0

        static func draw(_ face: AikoFace, _ ground: FaceGround, _ small: Bool) -> FacePicture {
            // Thicker lines at 16 to 32 px, or they vanish.
            let k = small ? 1.45 : 1
            let light = ground == .light
            var shapes: [FaceShape] = []

            // On a dark menu bar the white head needs no outline (D-214).
            let headShape = light
                ? FaceShape(head, fill: FaceArt.paper, stroke: FaceArt.ink, strokeWidth: FaceArt.round2(2.25 * k))
                : FaceShape(head, fill: FaceArt.paper)

            var extras: [FaceShape] = []
            switch face {
            case .fresh:
                brows(&shapes, "soft", k)
                dome(&shapes, l, k)
                dome(&shapes, r, k)
                shapes.append(FaceArt.line("M26.8 46 Q29.4 49.6 32 46.6 Q34.6 49.6 37.2 46", 1.7 * k, FaceArt.ink))
                blush(&shapes, 0.5)
                shapes.append(FaceArt.line(
                    "M29.4 42.2 L30.4 40.8 M31.6 42.2 L32.6 40.8 M33.8 42.2 L34.8 40.8", 0.9 * k, FaceArt.pink, 0.7))

            case .tired:
                brows(&shapes, "sad", k)
                lidded(&shapes, l, 36.2, -1.6, k)
                lidded(&shapes, r, 36.2, 1.6, k)
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(l - 4.2)) 44.6 Q\(FaceArt.n(l)) 46 \(FaceArt.n(l + 4.2)) 44.6", 1 * k, FaceArt.muted))
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(r - 4.2)) 44.6 Q\(FaceArt.n(r)) 46 \(FaceArt.n(r + 4.2)) 44.6", 1 * k, FaceArt.muted))
                shapes.append(FaceArt.line(
                    "M26.8 48 Q28.2 46.2 29.6 48 T32.4 48 T35.2 48 T37.4 47.8", 1.5 * k, FaceArt.ink))
                blush(&shapes, 0.35)
                extras.append(dropShape())

            case .asleep:
                brows(&shapes, "soft", k)
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(l - 5)) 36.2 Q\(FaceArt.n(l)) 40.6 \(FaceArt.n(l + 5)) 36.2", 1.9 * k, FaceArt.ink))
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(r - 5)) 36.2 Q\(FaceArt.n(r)) 40.6 \(FaceArt.n(r + 5)) 36.2", 1.9 * k, FaceArt.ink))
                shapes.append(FaceShape(FaceArt.ellipse(32, 47.4, 1.7, 2), fill: FaceArt.ink))
                blush(&shapes, 0.55)

            case .working:
                brows(&shapes, "focused", k)
                dome(&shapes, l, k, 36.4, 0.4, 1.2)
                dome(&shapes, r, k, 36.4, 0.4, 1.2)
                shapes.append(FaceArt.line("M26.4 46.4 L35.4 47", 1.7 * k, FaceArt.ink))
                shapes.append(FaceShape(
                    "M31.4 46.8 L31.3 49.8 Q33.8 54.4 36.4 50 L36.4 47.1 Z",
                    fill: FaceArt.pink,
                    stroke: FaceArt.ink,
                    strokeWidth: FaceArt.round2(1.1 * k)))
                blush(&shapes, 0.4)
                extras.append(FaceArt.line(
                    "M55.5 7.5 L59.1 3.3 M57.5 13.1 L62.1 11.7 M51.3 5.1 L51.9 1.3",
                    min(2.6, 1.9 * k),
                    light ? FaceArt.ink : FaceArt.paper))

            case .waiting:
                brows(&shapes, "raised", k)
                roundEye(&shapes, l, k)
                roundEye(&shapes, r, k)
                shapes.append(FaceShape(FaceArt.ellipse(32, 48, 2.7, 3.3), fill: FaceArt.pink))
                blush(&shapes, 0.5)
                extras.append(FaceShape(FaceArt.roundRect(53.5, 1.5, 5, 13, 2.5), fill: FaceArt.caution))
                extras.append(FaceShape(FaceArt.circle(56, 19.4, 2.8), fill: FaceArt.caution))

            case .done:
                brows(&shapes, "soft", k)
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(l - 5)) 38 Q\(FaceArt.n(l)) 31.2 \(FaceArt.n(l + 5)) 38", 2.1 * k, FaceArt.ink))
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(r - 5)) 38 Q\(FaceArt.n(r)) 31.2 \(FaceArt.n(r + 5)) 38", 2.1 * k, FaceArt.ink))
                shapes.append(FaceShape("M25.6 44 L38.4 44 Q38.4 53.6 32 53.6 Q25.6 53.6 25.6 44 Z", fill: FaceArt.ink))
                shapes.append(FaceShape("M28.6 51 Q32 48 35.4 51 Q34 53.2 32 53.2 Q30 53.2 28.6 51 Z", fill: FaceArt.pink))
                blush(&shapes, 0.55)

            case .error:
                brows(&shapes, "sad", k)
                shapes.append(FaceArt.line("M18 31.6 L25 35 L18 38.4", 2.2 * k, FaceArt.ink))
                shapes.append(FaceArt.line("M46 31.6 L39 35 L46 38.4", 2.2 * k, FaceArt.ink))
                shapes.append(FaceShape(
                    "M26 51.6 Q26 43.4 32 43.4 Q38 43.4 38 51.6 Q32 49.6 26 51.6 Z", fill: FaceArt.ink))
                shapes.append(FaceShape("M28.4 50.9 Q32 47.6 35.6 50.9 Q32 49.9 28.4 50.9 Z", fill: FaceArt.pink))
                extras.append(dropShape())
            }

            return FacePicture(
                viewLeft: small ? 3 : 0,
                viewTop: small ? 1 : 0,
                viewSize: small ? 59 : 64,
                groups: [
                    FaceGroup([headShape]),
                    FaceGroup(shapes, scale: 1.1, centerX: 32, centerY: 40),
                    FaceGroup(extras),
                ])
        }

        static func dropShape() -> FaceShape {
            FaceShape("M51.5 10 C45.8 18.4 45.8 24.6 51.5 24.6 C57.2 24.6 57.2 18.4 51.5 10 Z", fill: FaceArt.drop)
        }

        static func brows(_ shapes: inout [FaceShape], _ kind: String, _ k: Double) {
            for (x, side) in [(l, -1.0), (r, 1.0)] {
                func o(_ dx: Double) -> String { FaceArt.n(x + side * dx) }
                let data: String
                switch kind {
                case "soft": data = "M\(o(-4.6)) 26.2 Q\(o(0)) 22.6 \(o(4.8)) 25.4"
                case "sad": data = "M\(o(-4.6)) 23.2 Q\(o(-0.6)) 25.8 \(o(4.8)) 26.6"
                case "raised": data = "M\(o(-4.8)) 23 Q\(o(0)) 18.2 \(o(4.8)) 22.2"
                default: data = "M\(o(-4.8)) 26.4 Q\(o(0)) 24 \(o(5)) 23.6"
                }
                shapes.append(FaceArt.line(data, 1.5 * k, FaceArt.ink))
            }
        }

        static func highlight(_ shapes: inout [FaceShape], _ x: Double, _ y: Double, _ r: Double) {
            shapes.append(FaceShape(FaceArt.circle(x, y, r), fill: FaceArt.paper))
        }

        /// A round top, a flat base, a big highlight and a small one.
        static func dome(
            _ shapes: inout [FaceShape], _ x: Double, _ k: Double, _ y: Double = 36, _ gx: Double = 0, _ gy: Double = 0
        ) {
            shapes.append(FaceShape(
                "M\(FaceArt.n(x - 6)) \(FaceArt.n(y + 3.2)) C\(FaceArt.n(x - 6.2)) \(FaceArt.n(y - 6.8)) "
                    + "\(FaceArt.n(x + 6.2)) \(FaceArt.n(y - 6.8)) \(FaceArt.n(x + 6)) \(FaceArt.n(y + 3.2)) "
                    + "Q\(FaceArt.n(x)) \(FaceArt.n(y + 4)) \(FaceArt.n(x - 6)) \(FaceArt.n(y + 3.2)) Z",
                fill: FaceArt.ink))
            highlight(&shapes, x + 1.8 + gx, y - 1.6 + gy, min(2.6, 2 * k))
            highlight(&shapes, x - 2.6 + gx, y + 1.4 + gy, min(1.1, 0.8 * k))
        }

        static func roundEye(_ shapes: inout [FaceShape], _ x: Double, _ k: Double, _ y: Double = 35.5) {
            shapes.append(FaceShape(FaceArt.circle(x, y, 5.6), fill: FaceArt.ink))
            highlight(&shapes, x + 1.6, y - 1.8, min(2.5, 1.9 * k))
            highlight(&shapes, x - 2, y + 2, min(1.1, 0.8 * k))
        }

        static func lidded(_ shapes: inout [FaceShape], _ x: Double, _ y: Double, _ tilt: Double, _ k: Double) {
            shapes.append(FaceShape(
                "M\(FaceArt.n(x - 5.2)) \(FaceArt.n(y - tilt)) L\(FaceArt.n(x + 5.2)) \(FaceArt.n(y + tilt)) "
                    + "C\(FaceArt.n(x + 5)) \(FaceArt.n(y + 6.4)) \(FaceArt.n(x - 5)) \(FaceArt.n(y + 6.4)) "
                    + "\(FaceArt.n(x - 5.2)) \(FaceArt.n(y - tilt)) Z",
                fill: FaceArt.ink))
            shapes.append(FaceArt.line(
                "M\(FaceArt.n(x - 6.2)) \(FaceArt.n(y - tilt - 0.2)) L\(FaceArt.n(x + 6.2)) \(FaceArt.n(y + tilt + 0.2))",
                1.9 * k,
                FaceArt.ink))
            highlight(&shapes, x + 1.2, y + 2.6, min(1.4, 1.1 * k))
        }

        static func blush(_ shapes: inout [FaceShape], _ opacity: Double) {
            shapes.append(FaceShape(FaceArt.ellipse(l - 4.4, 45, 4.4, 2.5), fill: FaceArt.pink, opacity: opacity))
            shapes.append(FaceShape(FaceArt.ellipse(r + 4.4, 45, 4.4, 2.5), fill: FaceArt.pink, opacity: opacity))
        }
    }
}
