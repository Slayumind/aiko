import Foundation

/// What the parser found in the output of a terminal (D-412). A sequence split between two chunks is
/// kept until its end comes, so the receiver sees whole pieces only. Characters of sequences are
/// UTF-16 units, as on Windows.
public protocol VtReceiver: AnyObject {
    /// One printable character, a whole code point.
    func print(_ codePoint: Int)

    /// A control character: CR, LF, BS, TAB and the like.
    func control(_ control: UInt16)

    /// A CSI sequence: its parameters (missing ones are -1), the private marker ('?', '>' or 0) and
    /// the final character.
    func csi(_ parameters: [Int], marker: UInt16, final: UInt16)

    /// An escape sequence with no CSI: ESC 7, ESC 8, ESC M and the like.
    func escape(_ final: UInt16)

    /// An operating system command: its text without ESC ] and the terminator.
    func osc(_ text: String)
}

/// Splits the output of a terminal into characters and sequences: the states of the parser in the
/// VT500 diagram, cut down to what a shell sends. DCS, SOS, PM and APC strings are skipped whole.
/// Rows: spec/cases/shell-screen. The twin of VtParser.cs.
public final class VtParser {
    private enum State { case ground, escape, escapeIntermediate, csi, osc, oscEscape, skip, skipEscape }

    private static let maxParameters = 32
    private static let maxOscLength = 4096

    private static let esc: UInt16 = 0x1B
    private static let bell: UInt16 = 0x07
    private static let backslash = UInt16(UInt8(ascii: "\\"))

    private let receiver: VtReceiver
    private var parameters: [Int] = []
    private var oscUnits: [UInt16] = []
    private var state = State.ground
    private var current = -1
    private var marker: UInt16 = 0
    private var highSurrogate: UInt16 = 0

    public init(_ receiver: VtReceiver) {
        self.receiver = receiver
    }

    public func feed(_ chunk: String) {
        for unit in chunk.utf16 {
            step(unit)
        }
    }

    private func step(_ c: UInt16) {
        switch state {
        case .ground:
            ground(c)
        case .escape:
            afterEscape(c)
        case .escapeIntermediate:
            // ESC ( B and the like choose a character set: one more character, nothing to do.
            state = .ground
        case .csi:
            inCsi(c)
        case .osc:
            inOsc(c)
        case .oscEscape:
            if c == Self.backslash {
                endOsc()
            } else {
                state = .osc
                inOsc(c)
            }
        case .skip:
            if c == Self.esc {
                state = .skipEscape
            } else if c == Self.bell {
                state = .ground
            }
        case .skipEscape:
            state = c == Self.backslash ? .ground : .skip
        }
    }

    private func ground(_ c: UInt16) {
        if c == Self.esc {
            state = .escape
            return
        }

        if c < 0x20 || c == 0x7F {
            if c != 0x7F {
                receiver.control(c)
            }
            return
        }

        if UTF16.isLeadSurrogate(c) {
            highSurrogate = c
            return
        }

        if UTF16.isTrailSurrogate(c) && highSurrogate != 0 {
            let point = 0x10000 + ((Int(highSurrogate) - 0xD800) << 10) + (Int(c) - 0xDC00)
            receiver.print(point)
            highSurrogate = 0
            return
        }

        highSurrogate = 0
        receiver.print(Int(c))
    }

    private func afterEscape(_ c: UInt16) {
        switch Unicode.Scalar(c).map(Character.init) ?? " " {
        case "[":
            parameters.removeAll(keepingCapacity: true)
            current = -1
            marker = 0
            state = .csi
        case "]":
            oscUnits.removeAll(keepingCapacity: true)
            state = .osc
        case "P", "X", "^", "_":
            state = .skip
        case "(", ")", "*", "+", "#", "%":
            state = .escapeIntermediate
        default:
            state = .ground
            receiver.escape(c)
        }
    }

    private func inCsi(_ c: UInt16) {
        switch c {
        case 0x30...0x39:
            current = (current < 0 ? 0 : current) * 10 + Int(c - 0x30)
            if current > 99_999 {
                current = 99_999
            }
        case UInt16(UInt8(ascii: ";")), UInt16(UInt8(ascii: ":")):
            addParameter()
        case UInt16(UInt8(ascii: "?")), UInt16(UInt8(ascii: ">")), UInt16(UInt8(ascii: "<")), UInt16(UInt8(ascii: "=")):
            marker = c
        case 0x20...0x2F:
            // An intermediate byte, as in CSI ? 2026 $ p: the final byte still ends it.
            break
        case 0x40...0x7E:
            addParameter()
            state = .ground
            receiver.csi(parameters, marker: marker, final: c)
        case Self.esc:
            state = .escape
        default:
            // A control in the middle of a sequence acts at once, as in a real terminal.
            receiver.control(c)
        }
    }

    private func addParameter() {
        if parameters.count < Self.maxParameters {
            parameters.append(current)
        }
        current = -1
    }

    private func inOsc(_ c: UInt16) {
        if c == Self.bell {
            endOsc()
        } else if c == Self.esc {
            state = .oscEscape
        } else if oscUnits.count < Self.maxOscLength {
            oscUnits.append(c)
        }
    }

    private func endOsc() {
        state = .ground
        receiver.osc(String(decoding: oscUnits, as: UTF16.self))
    }
}
