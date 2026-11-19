import AikoKit
import AppKit
import SwiftUI

/// The shared parts of the new look, the twins of the files in Theme/ on Windows: a rule, a
/// floating panel, a frame with a title in its line, the mark of a state and the bar of a limit.
/// A look that a second screen repeats becomes a part here, not a copy.

/// The icons of the slayumind icon set that the marks use, the same path data as SiteIcons.cs.
/// Each is a filled shape in a box of 24, so its lines stay thin at any size.
enum SiteIcons {
    static let check = BarMarks.check

    static let copy =
        "m16,18.5v1c0,2.481-2.019,4.5-4.5,4.5h-7c-2.481,0-4.5-2.019-4.5-4.5v-7c0-2.481,2.019-4.5,4.5-4.5h1"
        + "c.276,0,.5.224.5.5s-.224.5-.5.5h-1c-1.93,0-3.5,1.57-3.5,3.5v7c0,1.93,1.57,3.5,3.5,3.5h7"
        + "c1.93,0,3.5-1.57,3.5-3.5v-1c0-.276.224-.5.5-.5s.5.224.5.5Zm8-14v7c0,2.481-2.019,4.5-4.5,4.5h-7"
        + "c-2.481,0-4.5-2.019-4.5-4.5v-7c0-2.481,2.019-4.5,4.5-4.5h7c2.481,0,4.5,2.019,4.5,4.5Zm-1,0"
        + "c0-1.93-1.57-3.5-3.5-3.5h-7c-1.93,0-3.5,1.57-3.5,3.5v7c0,1.93,1.57,3.5,3.5,3.5h7"
        + "c1.93,0,3.5-1.57,3.5-3.5v-7Z"

    static let scrubber =
        "M12,8c-2.206,0-4,1.794-4,4s1.794,4,4,4,4-1.794,4-4-1.794-4-4-4Zm0,7c-1.654,0-3-1.346-3-3s1.346-3"
        + ",3-3,3,1.346,3,3-1.346,3-3,3Z"

    static let fireFlame =
        "M17.938,5.57c-1.175-1.511-2.389-3.073-3.26-4.786-.228-.447-.615-.722-1.063-.753-.515-.032-1.017,"
        + ".24-1.364,.736-.795,1.138-1.251,2.681-1.251,4.232,0,2.201,.648,3.516,1.219,4.677,.5,1.015,.895,1"
        + ".816,.783,3.023-.12,1.305-1.315,2.315-2.698,2.3-2.856-.032-3.174-2.772-3.305-6.485-.025-.704-.48"
        + "4-1.276-1.169-1.459-.69-.188-1.38,.088-1.755,.691-1.003,1.61-2.074,4.123-2.075,6.343,0,5.461,4.5"
        + "3,9.905,10.002,9.905,5.512-.034,9.997-4.477,9.997-9.896,.056-3.234-1.944-5.807-4.061-8.529Zm3.06"
        + "1,8.521c0,4.879-4.039,8.874-9.002,8.904-4.929,0-8.997-3.995-8.997-8.905,0-2.011,1.037-4.39,1.924"
        + "-5.815,.126-.202,.314-.274,.486-.274,.057,0,.112,.008,.163,.021,.097,.026,.413,.141,.427,.528,.1"
        + "08,3.078,.305,7.404,4.336,7.45,1.891,0,3.495-1.401,3.662-3.208,.138-1.488-.357-2.493-.882-3.558-"
        + ".549-1.114-1.116-2.267-1.116-4.234,0-1.352,.39-2.686,1.071-3.66,.17-.242,.354-.31,.475-.312,.093"
        + ",.007,.175,.077,.242,.209,.914,1.799,2.158,3.398,3.362,4.946,2.006,2.58,3.9,5.017,3.851,7.907Z"

    /// cross.svg, a line in the set, written as a filled shape one unit wide.
    static let cross =
        "M3.65,4.35l.7-.7L12,11.29l7.65-7.64.7.7L12.71,12l7.64,7.65-.7.7L12,12.71l-7.65,7.64-.7-.7L11.29,12Z"
}

extension NSBezierPath {
    /// A rectangle with its corners cut at 45 degrees, each by its own size; a size of 0 leaves the
    /// corner square. The island and its landing strip have this shape (D-448). The corner named top
    /// is at minY, so it wants a place where y grows downwards.
    static func cutCorners(_ rect: NSRect, _ corners: Corners) -> NSBezierPath {
        let (topLeft, topRight) = (CGFloat(corners.topLeft), CGFloat(corners.topRight))
        let (bottomRight, bottomLeft) = (CGFloat(corners.bottomRight), CGFloat(corners.bottomLeft))
        let path = NSBezierPath()
        path.move(to: NSPoint(x: rect.minX + topLeft, y: rect.minY))
        path.line(to: NSPoint(x: rect.maxX - topRight, y: rect.minY))
        path.line(to: NSPoint(x: rect.maxX, y: rect.minY + topRight))
        path.line(to: NSPoint(x: rect.maxX, y: rect.maxY - bottomRight))
        path.line(to: NSPoint(x: rect.maxX - bottomRight, y: rect.maxY))
        path.line(to: NSPoint(x: rect.minX + bottomLeft, y: rect.maxY))
        path.line(to: NSPoint(x: rect.minX, y: rect.maxY - bottomLeft))
        path.line(to: NSPoint(x: rect.minX, y: rect.minY + topLeft))
        path.close()
        return path
    }
}

/// A thin line that divides two parts of a screen: across by default, down when asked.
struct Rule: View {
    var down = false

    var body: some View {
        if down {
            Rectangle().fill(Theme.hairline).frame(width: 1)
        } else {
            Rectangle().fill(Theme.hairline).frame(height: 1)
        }
    }
}

extension View {
    /// What floats over the window: a menu, a list, a question. The ground, the line and the one
    /// shadow of Aiko are set here, so every floating thing looks the same (FloatingPanel.cs).
    func floatingPanel() -> some View {
        background(Theme.background)
            .hairlineFrame()
            .shadow(color: .black.opacity(Theme.shadowOpacity), radius: Theme.shadowBlur / 2, y: Theme.shadowOffset)
    }
}

/// A thin frame with a title in a gap of its top line, and an optional second label at the right
/// end of that line. The terminal, the editor and the floating panels use it (D-437, D-441).
struct TitledFrame<Title: View, Aside: View, Content: View>: View {
    var padding = EdgeInsets(top: 8, leading: 12, bottom: 10, trailing: 12)
    var line = Theme.hairline
    @ViewBuilder let title: () -> Title
    @ViewBuilder let aside: () -> Aside
    @ViewBuilder let content: () -> Content

    @State private var titleSize = CGSize.zero
    @State private var asideSize = CGSize.zero

    private static var titleLeft: CGFloat { 14 }
    private static var gap: CGFloat { 6 }

    var body: some View {
        let labelHeight = max(titleSize.height, asideSize.height)
        content()
            .padding(padding)
            .padding(.top, labelHeight)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background(alignment: .topLeading) {
                GeometryReader { box in
                    frameLine(in: box.size, top: labelHeight / 2)
                        .fill(line)
                }
            }
            .overlay(alignment: .topLeading) {
                title()
                    .fixedSize()
                    .onGeometryChange(for: CGSize.self, of: \.size) { titleSize = $0 }
                    .offset(x: Self.titleLeft)
            }
            .overlay(alignment: .topTrailing) {
                aside()
                    .fixedSize()
                    .onGeometryChange(for: CGSize.self, of: \.size) { asideSize = $0 }
                    .offset(x: -Self.titleLeft)
            }
    }

    /// The four lines as thin filled strips, with gaps where the labels stand.
    private func frameLine(in size: CGSize, top: CGFloat) -> Path {
        var path = Path()
        let bottom = size.height - 1
        let right = size.width - 1
        var from: CGFloat = 0

        func across(_ start: CGFloat, _ end: CGFloat, _ y: CGFloat) {
            if end > start { path.addRect(CGRect(x: start, y: y, width: end - start, height: 1)) }
        }

        if titleSize.width > 0 {
            across(from, Self.titleLeft - Self.gap, top)
            from = Self.titleLeft + titleSize.width + Self.gap
        }
        if asideSize.width > 0 {
            across(from, size.width - Self.titleLeft - asideSize.width - Self.gap, top)
            from = size.width - Self.titleLeft + Self.gap
        }
        across(from, size.width, top)
        across(0, size.width, bottom)
        path.addRect(CGRect(x: 0, y: top, width: 1, height: bottom - top + 1))
        path.addRect(CGRect(x: right, y: top, width: 1, height: bottom - top + 1))
        return path
    }
}

extension TitledFrame where Aside == EmptyView {
    init(
        padding: EdgeInsets = EdgeInsets(top: 8, leading: 12, bottom: 10, trailing: 12),
        line: Color = Theme.hairline,
        @ViewBuilder title: @escaping () -> Title,
        @ViewBuilder content: @escaping () -> Content
    ) {
        self.init(padding: padding, line: line, title: title, aside: { EmptyView() }, content: content)
    }
}

/// What a session or a limit is doing, in the words of the new look.
enum StatusLook {
    case working
    case waiting
    case done
    case asleep
    case stopped
    case limitNearEnd

    /// The colour of the sign and the word. Green, orange and red belong to sessions, violet to
    /// limits (D-440).
    var tone: Color {
        switch self {
        case .working: return Theme.ink
        case .done: return Theme.positive
        case .waiting: return Theme.caution
        case .stopped: return Theme.destructive
        case .limitNearEnd: return Theme.limitStrong
        case .asleep: return Theme.quietText
        }
    }

    /// The look of a row of the session list; a session the person has seen has none.
    init?(_ state: RowState) {
        switch state {
        case .working: self = .working
        case .waiting: self = .waiting
        case .done: self = .done
        case .asleep: self = .asleep
        case .stopped: self = .stopped
        default: return nil
        }
    }

    /// The word of a session state, the same strings as StatusMark.Of on Windows.
    var word: String {
        switch self {
        case .working: return Strings.terminalWorking
        case .waiting: return Strings.terminalWaiting
        case .done: return Strings.terminalDone
        case .asleep: return Strings.terminalAsleep
        case .stopped: return Strings.terminalStopped
        case .limitNearEnd: return ""
        }
    }
}

/// A state is always a sign and a word, never a colour alone (D-441). The twin of StatusMark.cs.
struct StatusMark: View {
    let look: StatusLook
    var word: String?

    var body: some View {
        HStack(spacing: 5) {
            StatusSign(look: look)
            Text(word ?? look.word)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(look.tone)
        }
    }
}

/// The sign of a state alone, for counts on the island and the tabs.
struct StatusSign: View {
    let look: StatusLook

    var body: some View {
        switch look {
        case .working: FilledMark(data: SiteIcons.scrubber, size: 9, tone: look.tone)
        case .done: FilledMark(data: SiteIcons.check, size: 12, tone: look.tone)
        case .stopped: FilledMark(data: SiteIcons.cross, size: 11, tone: look.tone)
        case .limitNearEnd: FilledMark(data: SiteIcons.fireFlame, size: 13, tone: look.tone)
        case .waiting: letters("||")
        case .asleep: letters("–")
        }
    }

    private func letters(_ text: String) -> some View {
        Text(text)
            .font(Theme.mono(Theme.textSmall))
            .foregroundStyle(look.tone)
    }
}

/// A bar of the new look: a tall fill that stands on a thin track, both on one bottom line (D-447).
/// The twin of LimitBar.cs.
struct LimitBar: View {
    let value: Double
    var fill = Theme.ink
    var trackHeight: CGFloat = 5
    var height: CGFloat = 12

    var body: some View {
        GeometryReader { box in
            ZStack(alignment: .bottomLeading) {
                Rectangle().fill(Theme.barTrack).frame(height: trackHeight)
                Rectangle().fill(fill).frame(width: (box.size.width * min(max(value, 0), 1)).rounded())
            }
            .frame(width: box.size.width, height: box.size.height, alignment: .bottomLeading)
        }
        .frame(height: height)
    }
}
