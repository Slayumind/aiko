import Foundation

/// One step of an outline. Every curve is a cubic: arcs and quadratic curves are turned into
/// cubics here, so the drawing layer has three shapes to draw instead of seven.
public enum PathStep: Sendable, Equatable {
    case move(Point)
    case line(Point)
    case curve(Point, Point, Point)
    case close
}

/// Reads SVG path data, the shape FaceArt writes its faces in (D-213).
///
/// WPF parses the same text itself with Geometry.Parse; AppKit has nothing of the kind, so the
/// core reads it. That keeps the reading testable, and both systems draw the same outline.
public enum SvgPath {
    /// The steps of a path. Anything the reader cannot make sense of ends the path there: a face
    /// drawn short is better than a crash in the menu bar.
    public static func steps(_ data: String) -> [PathStep] {
        var reader = Reader(data)
        var steps: [PathStep] = []

        var at = Point(0, 0)
        var start = Point(0, 0)
        var lastCurve: Point?
        var lastQuad: Point?
        var command: Character?

        while true {
            reader.skipSeparators()
            if let letter = reader.peekCommand() {
                reader.step()
                command = letter
            } else if reader.isDone {
                break
            } else if command == nil {
                break
            } else if command == "M" {
                // More pairs after a move are a line, as the SVG rules say.
                command = "L"
            } else if command == "m" {
                command = "l"
            }

            guard let letter = command else { break }
            let relative = letter.isLowercase
            let base = Character(letter.uppercased())

            if base == "Z" {
                steps.append(.close)
                at = start
                lastCurve = nil
                lastQuad = nil
                continue
            }

            guard let taken = read(base, &reader, at, relative, lastCurve, lastQuad) else {
                break
            }

            steps.append(contentsOf: taken.steps)
            at = taken.at
            lastCurve = taken.lastCurve
            lastQuad = taken.lastQuad
            if base == "M" {
                start = taken.at
            }
        }

        return steps
    }

    private struct Taken {
        let steps: [PathStep]
        let at: Point
        let lastCurve: Point?
        let lastQuad: Point?
    }

    private static func read(
        _ base: Character, _ reader: inout Reader, _ at: Point, _ relative: Bool,
        _ lastCurve: Point?, _ lastQuad: Point?
    ) -> Taken? {
        func point(_ x: Double, _ y: Double) -> Point {
            relative ? Point(at.x + x, at.y + y) : Point(x, y)
        }

        switch base {
        case "M":
            guard let x = reader.number(), let y = reader.number() else { return nil }
            let to = point(x, y)
            return Taken(steps: [.move(to)], at: to, lastCurve: nil, lastQuad: nil)

        case "L":
            guard let x = reader.number(), let y = reader.number() else { return nil }
            let to = point(x, y)
            return Taken(steps: [.line(to)], at: to, lastCurve: nil, lastQuad: nil)

        case "H":
            guard let x = reader.number() else { return nil }
            let to = Point(relative ? at.x + x : x, at.y)
            return Taken(steps: [.line(to)], at: to, lastCurve: nil, lastQuad: nil)

        case "V":
            guard let y = reader.number() else { return nil }
            let to = Point(at.x, relative ? at.y + y : y)
            return Taken(steps: [.line(to)], at: to, lastCurve: nil, lastQuad: nil)

        case "C":
            guard let x1 = reader.number(), let y1 = reader.number(),
                  let x2 = reader.number(), let y2 = reader.number(),
                  let x = reader.number(), let y = reader.number() else { return nil }
            let second = point(x2, y2)
            let to = point(x, y)
            return Taken(steps: [.curve(point(x1, y1), second, to)], at: to, lastCurve: second, lastQuad: nil)

        case "S":
            guard let x2 = reader.number(), let y2 = reader.number(),
                  let x = reader.number(), let y = reader.number() else { return nil }
            let first = mirror(lastCurve, about: at)
            let second = point(x2, y2)
            let to = point(x, y)
            return Taken(steps: [.curve(first, second, to)], at: to, lastCurve: second, lastQuad: nil)

        case "Q":
            guard let cx = reader.number(), let cy = reader.number(),
                  let x = reader.number(), let y = reader.number() else { return nil }
            let control = point(cx, cy)
            let to = point(x, y)
            return Taken(steps: [cubic(at, control, to)], at: to, lastCurve: nil, lastQuad: control)

        case "T":
            guard let x = reader.number(), let y = reader.number() else { return nil }
            let control = mirror(lastQuad, about: at)
            let to = point(x, y)
            return Taken(steps: [cubic(at, control, to)], at: to, lastCurve: nil, lastQuad: control)

        case "A":
            guard let rx = reader.number(), let ry = reader.number(), let turn = reader.number(),
                  let large = reader.number(), let sweep = reader.number(),
                  let x = reader.number(), let y = reader.number() else { return nil }
            let to = point(x, y)
            let steps = arc(
                at, to, rx: rx, ry: ry, turn: turn, large: large != 0, sweep: sweep != 0)
            return Taken(steps: steps, at: to, lastCurve: nil, lastQuad: nil)

        default:
            return nil
        }
    }

    /// The smooth commands take the last control point turned around the current point. With no
    /// curve before them the control point is the current point itself.
    private static func mirror(_ control: Point?, about at: Point) -> Point {
        guard let control else { return at }
        return Point((2 * at.x) - control.x, (2 * at.y) - control.y)
    }

    /// A quadratic curve as a cubic one: the two control points sit two thirds of the way from the
    /// ends towards the single one.
    private static func cubic(_ from: Point, _ control: Point, _ to: Point) -> PathStep {
        .curve(
            Point(from.x + (2.0 / 3 * (control.x - from.x)), from.y + (2.0 / 3 * (control.y - from.y))),
            Point(to.x + (2.0 / 3 * (control.x - to.x)), to.y + (2.0 / 3 * (control.y - to.y))),
            to)
    }

    /// An elliptical arc as cubic curves, by the endpoint to centre conversion of the SVG rules.
    /// Every piece covers at most a quarter turn, which is where a cubic still follows a circle
    /// closely enough to see no difference at 18 points.
    private static func arc(
        _ from: Point, _ to: Point, rx: Double, ry: Double, turn: Double, large: Bool, sweep: Bool
    ) -> [PathStep] {
        if from == to {
            return []
        }

        var rx = abs(rx)
        var ry = abs(ry)
        if rx == 0 || ry == 0 {
            return [.line(to)]
        }

        let angle = turn * Double.pi / 180
        let cosine = cos(angle)
        let sine = sin(angle)

        let halfX = (from.x - to.x) / 2
        let halfY = (from.y - to.y) / 2
        let x1 = (cosine * halfX) + (sine * halfY)
        let y1 = (-sine * halfX) + (cosine * halfY)

        // Radii too small to reach are grown until they just do, as the rules ask.
        let wanted = ((x1 * x1) / (rx * rx)) + ((y1 * y1) / (ry * ry))
        if wanted > 1 {
            let grow = sqrt(wanted)
            rx *= grow
            ry *= grow
        }

        let top = (rx * rx * ry * ry) - (rx * rx * y1 * y1) - (ry * ry * x1 * x1)
        let bottom = (rx * rx * y1 * y1) + (ry * ry * x1 * x1)
        var reach = bottom == 0 ? 0 : sqrt(max(0, top / bottom))
        if large == sweep {
            reach = -reach
        }

        let cx1 = reach * rx * y1 / ry
        let cy1 = -reach * ry * x1 / rx
        let centre = Point(
            (cosine * cx1) - (sine * cy1) + ((from.x + to.x) / 2),
            (sine * cx1) + (cosine * cy1) + ((from.y + to.y) / 2))

        let start = turnOf(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry)
        var covered = turnOf((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry)
        if !sweep && covered > 0 {
            covered -= 2 * Double.pi
        } else if sweep && covered < 0 {
            covered += 2 * Double.pi
        }

        let pieces = max(1, Int(ceil(abs(covered) / (Double.pi / 2))))
        let step = covered / Double(pieces)
        // How far the control points sit along the tangents for one piece of this size.
        let pull = 4.0 / 3 * tan(step / 4)

        var steps: [PathStep] = []
        var at = start
        for _ in 0..<pieces {
            let next = at + step
            let from = onEllipse(centre, rx, ry, cosine, sine, at)
            let to = onEllipse(centre, rx, ry, cosine, sine, next)
            let fromSlope = slope(rx, ry, cosine, sine, at)
            let toSlope = slope(rx, ry, cosine, sine, next)

            steps.append(.curve(
                Point(from.x + (pull * fromSlope.x), from.y + (pull * fromSlope.y)),
                Point(to.x - (pull * toSlope.x), to.y - (pull * toSlope.y)),
                to))
            at = next
        }

        return steps
    }

    private static func onEllipse(
        _ centre: Point, _ rx: Double, _ ry: Double, _ cosine: Double, _ sine: Double, _ at: Double
    ) -> Point {
        let x = rx * cos(at)
        let y = ry * sin(at)
        return Point(centre.x + (cosine * x) - (sine * y), centre.y + (sine * x) + (cosine * y))
    }

    private static func slope(
        _ rx: Double, _ ry: Double, _ cosine: Double, _ sine: Double, _ at: Double
    ) -> Point {
        let x = -rx * sin(at)
        let y = ry * cos(at)
        return Point((cosine * x) - (sine * y), (sine * x) + (cosine * y))
    }

    /// The turn from one direction to another, with a sign.
    private static func turnOf(_ ux: Double, _ uy: Double, _ vx: Double, _ vy: Double) -> Double {
        let lengths = sqrt((ux * ux) + (uy * uy)) * sqrt((vx * vx) + (vy * vy))
        if lengths == 0 {
            return 0
        }

        let cosine = min(max(((ux * vx) + (uy * vy)) / lengths, -1), 1)
        return ((ux * vy) - (uy * vx)) < 0 ? -acos(cosine) : acos(cosine)
    }

    /// Reads the letters and the numbers of path data, one at a time.
    private struct Reader {
        private let characters: [Character]
        private var at = 0

        init(_ text: String) {
            characters = Array(text)
        }

        var isDone: Bool { at >= characters.count }

        mutating func step() {
            at += 1
        }

        mutating func skipSeparators() {
            while at < characters.count, characters[at] == "," || characters[at].isWhitespace {
                at += 1
            }
        }

        /// The next letter, if there is one where we stand. F1 and F0, the fill rule WPF writes in
        /// front of a path, are not commands and are skipped by the caller's number reader.
        func peekCommand() -> Character? {
            guard at < characters.count else { return nil }
            let character = characters[at]
            return "MmLlHhVvCcSsQqTtAaZz".contains(character) ? character : nil
        }

        mutating func number() -> Double? {
            skipSeparators()
            var text = ""

            if at < characters.count, characters[at] == "-" || characters[at] == "+" {
                text.append(characters[at])
                at += 1
            }

            while at < characters.count, characters[at].isNumber || characters[at] == "." {
                text.append(characters[at])
                at += 1
            }

            if at < characters.count, characters[at] == "e" || characters[at] == "E" {
                var exponent = String(characters[at])
                var next = at + 1
                if next < characters.count, characters[next] == "-" || characters[next] == "+" {
                    exponent.append(characters[next])
                    next += 1
                }

                var digits = ""
                while next < characters.count, characters[next].isNumber {
                    digits.append(characters[next])
                    next += 1
                }

                if !digits.isEmpty {
                    text += exponent + digits
                    at = next
                }
            }

            return Double(text)
        }
    }
}
