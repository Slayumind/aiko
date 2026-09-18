import Foundation

/// The steps between the rings and a face (D-211), as in the mockup's timeline.
public enum FacePhase: Sendable, Equatable, CaseIterable {
    case ringsOut
    case faceIn
    case faceOut
    case ringsBack
}

/// How the icon looks at one moment: the rings and the face, each with a size and an opacity. The
/// sweep draws the arc back from zero when the rings return.
public struct IconFrame: Sendable, Equatable {
    public let ringScale: Double
    public let ringOpacity: Double
    public let ringSweep: Double
    public let faceScale: Double
    public let faceOpacity: Double

    public init(_ ringScale: Double, _ ringOpacity: Double, _ ringSweep: Double, _ faceScale: Double, _ faceOpacity: Double) {
        self.ringScale = ringScale
        self.ringOpacity = ringOpacity
        self.ringSweep = ringSweep
        self.faceScale = faceScale
        self.faceOpacity = faceOpacity
    }

    public static let rings = IconFrame(1, 1, 1, 0.6, 0)

    public var showsFace: Bool { faceOpacity > 0 }
}

/// Frames for the tray icon. The icon is a picture the system is handed whole, so a transition is
/// a few pictures in a row rather than a smooth animation: eight per step, as in the mockup.
public enum TrayFaceMotion {
    public static let framesPerStep = 8

    /// From the event until the face is fully there. The face then stays TrayMood.showFor.
    public static let arrival: TimeInterval = duration(.ringsOut) + duration(.faceIn)

    public static func duration(_ phase: FacePhase) -> TimeInterval {
        switch phase {
        case .ringsOut: return 0.160
        case .faceIn: return 0.240
        case .faceOut: return 0.160
        case .ringsBack: return 0.280
        }
    }

    /// The frame at t from 0 to 1 through a step.
    public static func at(_ phase: FacePhase, _ time: Double) -> IconFrame {
        let t = min(max(time, 0), 1)
        switch phase {
        case .ringsOut:
            return IconFrame(1 - (0.4 * easeIn(t)), 1 - easeIn(t), 1, 0.6, 0)
        case .faceIn:
            return IconFrame(0.6, 0, 1, 0.6 + (0.4 * spring(t)), min(1, t * 2))
        case .faceOut:
            return IconFrame(0.6, 0, 1, 1 - (0.2 * easeIn(t)), 1 - easeIn(t))
        case .ringsBack:
            return IconFrame(0.6 + (0.4 * spring(t)), min(1, t * 2), min(1, t * 1.3), 0.6, 0)
        }
    }

    /// The pictures of one step, the last one exactly at its end.
    public static func frames(_ phase: FacePhase) -> [IconFrame] {
        (1...framesPerStep).map { at(phase, Double($0) / Double(framesPerStep)) }
    }

    /// How far the island has shrunk from the size of its rings to the size of the face, 0 to 1.
    /// It shrinks while the rings go and grows back while they return, so the face always sits with
    /// even room on every side.
    public static func islandFit(_ phase: FacePhase, _ time: Double) -> Double {
        let t = min(max(time, 0), 1)
        switch phase {
        case .ringsOut: return easeInOut(t)
        case .ringsBack: return 1 - easeInOut(t)
        default: return 1
        }
    }

    /// The steps from what the icon shows now to a face. A face already there needs only to come in
    /// again with its new look.
    public static func toFace(faceShown: Bool) -> [FacePhase] {
        faceShown ? [.faceIn] : [.ringsOut, .faceIn]
    }

    /// The steps back to the rings. Rings still on their way out just come back.
    public static func toRings(faceShown: Bool) -> [FacePhase] {
        faceShown ? [.faceOut, .ringsBack] : [.ringsBack]
    }

    private static func easeIn(_ t: Double) -> Double { t * t }

    private static func easeInOut(_ t: Double) -> Double {
        t < 0.5 ? 2 * t * t : 1 - (pow(-2 * t + 2, 2) / 2)
    }

    /// Overshoots a little past 1 and settles, like the spring in the mockup.
    private static func spring(_ t: Double) -> Double {
        1 - (pow(1 - t, 3) * cos(t * Double.pi * 1.15) * (1 - (t * 0.15)))
    }
}
