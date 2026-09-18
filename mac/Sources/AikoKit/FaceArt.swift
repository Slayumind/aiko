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

/// Aiko's two faces as shapes (D-213, D-214, D-215): the chibi mochi head and the floating emoji.
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

    /// A darker caution for the emoji on a light background, where #FE9A00 is too pale to read.
    public static let cautionOnLight = "#D97F00"

    /// Below this size the chibi draws with thicker lines and a tighter view box.
    public static let smallUpTo = 48.0

    public static func draw(_ style: FaceStyle, _ face: AikoFace, _ ground: FaceGround, _ small: Bool) -> FacePicture {
        style == .emoji ? Emoji.draw(face, ground, small) : Chibi.draw(face, ground, small)
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

    private enum Emoji {
        static let lx = 18.0
        static let rx = 46.0

        static func draw(_ face: AikoFace, _ ground: FaceGround, _ small: Bool) -> FacePicture {
            // At 16 to 32 px the emoji's thin lines fall under a pixel and turn grey. The chibi solves it
            // the same way.
            let k = small ? 1.35 : 1
            let light = ground == .light
            let ink = light ? FaceArt.ink : FaceArt.paper
            let caution = light ? FaceArt.cautionOnLight : FaceArt.caution

            // Pink on a dark background looks weaker, so the blush is stronger there.
            let blushK = light ? 1.0 : 1.55

            var shapes: [FaceShape] = []
            var extras: [FaceShape] = []

            switch face {
            case .fresh:
                brows(&shapes, "soft", ink, k)
                dome(&shapes, lx, 29, ink)
                dome(&shapes, rx, 29, ink)
                shapes.append(FaceArt.line("M24.6 44 Q28.3 49.4 32 44.8 Q35.7 49.4 39.4 44", 3.4 * k, ink))
                blush(&shapes, blushK)

            case .tired:
                brows(&shapes, "sad", ink, k)
                lidded(&shapes, lx, 28.6, -1.8, ink, k)
                lidded(&shapes, rx, 28.6, 1.8, ink, k)
                shapes.append(FaceArt.line("M25 47 Q27.4 44.4 29.8 47 T34.6 47 T39.2 46.6", 3.2 * k, ink))
                blush(&shapes, blushK, 0.35)
                extras.append(dropShape())

            case .asleep:
                brows(&shapes, "soft", ink, k)
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(lx - 7)) 28.4 Q\(FaceArt.n(lx)) 35.4 \(FaceArt.n(lx + 7)) 28.4", 3.6 * k, ink))
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(rx - 7)) 28.4 Q\(FaceArt.n(rx)) 35.4 \(FaceArt.n(rx + 7)) 28.4", 3.6 * k, ink))
                shapes.append(FaceShape(FaceArt.ellipse(32, 46, 2.6, 3), fill: ink))
                blush(&shapes, blushK, 0.5)

            case .working:
                brows(&shapes, "focused", ink, k)
                dome(&shapes, lx, 30, ink, 0.6, 1.6)
                dome(&shapes, rx, 30, ink, 0.6, 1.6)
                shapes.append(FaceShape("M31 45.8 L30.8 49.8 Q34.6 56.6 38.6 50.2 L38.6 46.6 Z", fill: FaceArt.pink))
                shapes.append(FaceArt.line("M24 45.2 L37.6 46.4", 3.4 * k, ink))
                blush(&shapes, blushK, 0.4)
                extras.append(FaceArt.line("M56 6 L59.6 1.8 M58 11.6 L62.6 10.2 M51.8 3.6 L52.4 -0.2", 2.8 * k, ink))

            case .waiting:
                brows(&shapes, "raised", ink, k)
                roundEye(&shapes, lx, 28.6, ink)
                roundEye(&shapes, rx, 28.6, ink)
                shapes.append(FaceShape(FaceArt.ellipse(32, 46.4, 4.2, 5), fill: FaceArt.pink))
                blush(&shapes, blushK, 0.5)
                extras.append(FaceShape(FaceArt.roundRect(56, 0.5, 6, 14, 3), fill: caution))
                extras.append(FaceShape(FaceArt.circle(59, 20, 3.2), fill: caution))

            case .done:
                brows(&shapes, "soft", ink, k)
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(lx - 7)) 31 Q\(FaceArt.n(lx)) 21.6 \(FaceArt.n(lx + 7)) 31", 3.8 * k, ink))
                shapes.append(FaceArt.line(
                    "M\(FaceArt.n(rx - 7)) 31 Q\(FaceArt.n(rx)) 21.6 \(FaceArt.n(rx + 7)) 31", 3.8 * k, ink))
                shapes.append(FaceShape("M23.4 41 L40.6 41 Q40.6 54.2 32 54.2 Q23.4 54.2 23.4 41 Z", fill: FaceArt.pink))
                blush(&shapes, blushK)

            case .error:
                brows(&shapes, "sad", ink, k)
                shapes.append(FaceArt.line("M12.6 23.4 L22.6 28.4 L12.6 33.4", 3.6 * k, ink))
                shapes.append(FaceArt.line("M51.4 23.4 L41.4 28.4 L51.4 33.4", 3.6 * k, ink))
                shapes.append(FaceShape(
                    "M23.6 51.4 Q23.6 41.2 32 41.2 Q40.4 41.2 40.4 51.4 Q32 48.6 23.6 51.4 Z", fill: FaceArt.pink))
                extras.append(dropShape())
            }

            return FacePicture(
                viewLeft: 0,
                viewTop: 0,
                viewSize: 64,
                groups: [FaceGroup(shapes, offsetY: 3), FaceGroup(extras)])
        }

        static func dropShape() -> FaceShape {
            FaceShape("M57.4 1 C51.4 9.8 51.4 16.2 57.4 16.2 C63.4 16.2 63.4 9.8 57.4 1 Z", fill: FaceArt.drop)
        }

        static func brows(_ shapes: inout [FaceShape], _ kind: String, _ ink: String, _ k: Double) {
            for (x, side) in [(lx, -1.0), (rx, 1.0)] {
                func o(_ dx: Double) -> String { FaceArt.n(x + side * dx) }
                let data: String
                switch kind {
                case "soft": data = "M\(o(-6)) 16.4 Q\(o(0)) 11.8 \(o(6)) 15.2"
                case "sad": data = "M\(o(-6)) 12.6 Q\(o(-0.8)) 16 \(o(6)) 17"
                case "raised": data = "M\(o(-6)) 12.4 Q\(o(0)) 6.4 \(o(6)) 11.4"
                default: data = "M\(o(-6.4)) 17 Q\(o(0)) 13.8 \(o(6.6)) 13.2"
                }
                shapes.append(FaceArt.line(data, 2.6 * k, ink))
            }
        }

        static func dome(
            _ shapes: inout [FaceShape], _ x: Double, _ y: Double, _ ink: String, _ gx: Double = 0, _ gy: Double = 0
        ) {
            shapes.append(FaceShape(
                "M\(FaceArt.n(x - 7)) \(FaceArt.n(y + 3.6)) C\(FaceArt.n(x - 7.2)) \(FaceArt.n(y - 8)) "
                    + "\(FaceArt.n(x + 7.2)) \(FaceArt.n(y - 8)) \(FaceArt.n(x + 7)) \(FaceArt.n(y + 3.6)) "
                    + "Q\(FaceArt.n(x)) \(FaceArt.n(y + 4.6)) \(FaceArt.n(x - 7)) \(FaceArt.n(y + 3.6)) Z",
                fill: ink,
                holes: [FaceArt.circle(x + 2.2 + gx, y - 1.8 + gy, 2.6), FaceArt.circle(x - 3 + gx, y + 1.6 + gy, 1.1)]))
        }

        static func roundEye(_ shapes: inout [FaceShape], _ x: Double, _ y: Double, _ ink: String) {
            shapes.append(FaceShape(
                FaceArt.circle(x, y, 7),
                fill: ink,
                holes: [FaceArt.circle(x + 2, y - 2.2, 2.9), FaceArt.circle(x - 2.6, y + 2.4, 1.3)]))
        }

        static func lidded(
            _ shapes: inout [FaceShape], _ x: Double, _ y: Double, _ tilt: Double, _ ink: String, _ k: Double
        ) {
            shapes.append(FaceShape(
                "M\(FaceArt.n(x - 6.6)) \(FaceArt.n(y - tilt)) L\(FaceArt.n(x + 6.6)) \(FaceArt.n(y + tilt)) "
                    + "C\(FaceArt.n(x + 6.2)) \(FaceArt.n(y + 8)) \(FaceArt.n(x - 6.2)) \(FaceArt.n(y + 8)) "
                    + "\(FaceArt.n(x - 6.6)) \(FaceArt.n(y - tilt)) Z",
                fill: ink,
                holes: [FaceArt.circle(x + 1.6, y + 3.2, 1.7)]))
            shapes.append(FaceArt.line(
                "M\(FaceArt.n(x - 8)) \(FaceArt.n(y - tilt - 0.3)) L\(FaceArt.n(x + 8)) \(FaceArt.n(y + tilt + 0.3))",
                3.2 * k,
                ink))
        }

        static func blush(_ shapes: inout [FaceShape], _ blushK: Double, _ opacity: Double = 0.55) {
            let alpha = FaceArt.round2(min(0.9, opacity * blushK))
            shapes.append(FaceShape(FaceArt.ellipse(lx - 6, 39, 5.4, 3.2), fill: FaceArt.pink, opacity: alpha))
            shapes.append(FaceShape(FaceArt.ellipse(rx + 6, 39, 5.4, 3.2), fill: FaceArt.pink, opacity: alpha))
        }
    }
}
