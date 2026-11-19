import AppKit
import SwiftUI

/// The tip of every window of Aiko: the island, the terminal window and the settings. The twin of
/// Theme/ToolTip.xaml and Tips.cs on Windows: the look of the right-click menu, the dark ground, one line,
/// square corners, the small mono text. A tip of the system would look like another app.
///
/// One small window shows it under the pointer, as the tip of the system stands. It takes no
/// clicks and no keys, and it goes when the pointer leaves or presses.
@MainActor
enum Tips {
    /// How long the pointer rests before the tip shows: the hover time of the system on Windows.
    static let delay: TimeInterval = 0.4

    /// The zoom of the terminal window: a tip over it takes it too, as on Windows (PopupZoom). The
    /// island has none.
    static var windowZoom: CGFloat = 1

    private static var window: NSPanel?
    private static var waiting: DispatchWorkItem?
    private static var owner: AnyHashable?

    /// The pointer came to something with a tip: it shows after a rest, unless the pointer moves on.
    static func hover(_ text: String, owner: AnyHashable, zoom: CGFloat = 1) {
        guard !text.isEmpty else { return }
        cancel()
        self.owner = owner
        let work = DispatchWorkItem { MainActor.assumeIsolated { show(text, at: NSEvent.mouseLocation, zoom: zoom) } }
        waiting = work
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: work)
    }

    /// The pointer left the thing, or pressed it: the tip goes.
    static func leave(owner: AnyHashable) {
        guard self.owner == owner else { return }
        cancel()
    }

    static func cancel() {
        waiting?.cancel()
        waiting = nil
        owner = nil
        window?.orderOut(nil)
    }

    /// The tip at a point of the screen, under the pointer and inside the screen.
    static func show(_ text: String, at point: NSPoint, zoom: CGFloat = 1) {
        let panel = window ?? makeWindow()
        window = panel
        // One line when it fits, and lines wrapped in the widest tip when it does not.
        var view = NSHostingView(rootView: TipView(text: text, wraps: false, zoom: zoom))
        if view.fittingSize.width > TipView.mostWidth * zoom {
            view = NSHostingView(rootView: TipView(text: text, wraps: true, zoom: zoom))
        }
        panel.contentView = view
        let size = view.fittingSize
        panel.setContentSize(size)

        let screen = NSScreen.screens.first { $0.frame.contains(point) }?.visibleFrame
            ?? NSScreen.main?.visibleFrame ?? .zero
        var origin = NSPoint(x: point.x, y: point.y - 22 - size.height)
        origin.x = min(max(origin.x, screen.minX + 4), screen.maxX - size.width - 4)
        if origin.y < screen.minY + 4 {
            origin.y = point.y + 12
        }
        panel.setFrameOrigin(origin)
        panel.orderFrontRegardless()
    }

    /// The size of the tip on screen, for --try-terminal.
    static var sizeForCheck: NSSize? { window?.isVisible == true ? window?.frame.size : nil }

    private static func makeWindow() -> NSPanel {
        let panel = NSPanel(
            contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.level = .popUpMenu
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        panel.isReleasedWhenClosed = false
        return panel
    }
}

/// The face of a tip.
struct TipView: View {
    let text: String
    let wraps: Bool
    var zoom: CGFloat = 1

    /// The widest tip, as on Windows, and the text inside its padding.
    static let mostWidth: CGFloat = 320
    private static let width: CGFloat = mostWidth - 16

    private var words: some View {
        Text(text)
            .font(Theme.mono(Theme.textSmall * zoom))
            .foregroundStyle(Theme.ink)
    }

    var body: some View {
        Group {
            if wraps {
                words.frame(width: Self.width * zoom, alignment: .leading).fixedSize(horizontal: false, vertical: true)
            } else {
                words.fixedSize()
            }
        }
            .padding(.leading, 8 * zoom)
            .padding(.trailing, 8 * zoom)
            .padding(.top, 4 * zoom)
            .padding(.bottom, 5 * zoom)
            .background(Theme.background)
            .hairlineFrame()
    }
}

extension View {
    /// The tip of Aiko instead of the tip of the system. An empty text shows nothing.
    func tip(_ text: String) -> some View {
        modifier(TipModifier(text: text))
    }
}

private struct TipModifier: ViewModifier {
    let text: String
    @State private var id = UUID()

    func body(content: Content) -> some View {
        content
            .onHover { inside in
                if inside {
                    Tips.hover(text, owner: id, zoom: Tips.windowZoom)
                } else {
                    Tips.leave(owner: id)
                }
            }
            .simultaneousGesture(TapGesture().onEnded { Tips.leave(owner: id) })
            .onDisappear { Tips.leave(owner: id) }
    }
}
