import AikoKit
import AppKit
import SwiftUI

extension Motion {
    /// The system is asked to show fewer animations: everything lands at its end state at once,
    /// the way Motion.IsOn works on Windows.
    static var reduceMotion: Bool {
        NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
    }

    static func curve(_ bezier: CubicBezier, _ duration: Double) -> Animation {
        .timingCurve(
            bezier.x1, bezier.y1, bezier.x2, bezier.y2,
            duration: or0(duration, reduceMotion: reduceMotion))
    }

    static func timing(_ bezier: CubicBezier) -> CAMediaTimingFunction {
        CAMediaTimingFunction(
            controlPoints: Float(bezier.x1), Float(bezier.y1), Float(bezier.x2), Float(bezier.y2))
    }
}

/// A borderless window is dragged by this part. AppKit moves the window, so there is no mouse maths.
struct WindowDragArea: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView { DragView() }

    func updateNSView(_ view: NSView, context: Context) {}

    private final class DragView: NSView {
        override func mouseDown(with event: NSEvent) {
            window?.performDrag(with: event)
        }
    }
}
