import AikoKit
import AppKit
import SwiftUI

/// The window of the terminal (D-292): an ordinary window the system sees, with the buttons of
/// macOS and Aiko's own title bar around them. The twin of Terminal/TerminalWindow.cs.
///
/// Aiko lives in the menu bar and has no Dock icon. While this window is open she has one, and a
/// menu bar of her own, so the window can be found with Cmd+Tab like any other (spike 2026-10-13).
@MainActor
final class TerminalWindow: NSObject, NSWindowDelegate {
    private let window: NSWindow

    /// Says no to closing while something still works; the host then asks the person.
    var mayClose: (() -> Bool)?

    /// The window is about to close: the last moment to read where it stands.
    var onClosing: (() -> Void)?
    var onClosed: (() -> Void)?

    init(panel: TerminalPanelState, place: WindowPlace) {
        window = NSWindow(
            contentRect: Self.rect(place),
            styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView],
            backing: .buffered,
            defer: false)
        super.init()

        window.title = Strings.terminalTitle
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.isMovableByWindowBackground = false
        window.isReleasedWhenClosed = false
        window.backgroundColor = Theme.nsColour(Rgba(0x0A, 0x0A, 0x0A))
        window.minSize = NSSize(width: WindowPlace.minWidth, height: WindowPlace.minHeight)
        window.collectionBehavior = [.fullScreenPrimary]
        window.delegate = self
        window.contentView = NSHostingView(rootView: TerminalPanelView(state: panel))
        window.setFrame(Self.rect(place), display: false)

        if place.maximized {
            window.zoom(nil)
        }
    }

    var isMiniaturized: Bool { window.isMiniaturized }

    /// Where the window stands now, to open it there the next time.
    var place: WindowPlace {
        let frame = window.frame
        return WindowPlace(
            left: frame.minX,
            top: Self.mainTop - frame.maxY,
            width: frame.width,
            height: frame.height,
            maximized: window.isZoomed)
    }

    func show() {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
        centreWindowButtons()
    }

    func activate() {
        if window.isMiniaturized {
            window.deminiaturize(nil)
        }
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    /// Closes after the host agreed: mayClose is asked again and says yes.
    func close() {
        window.performClose(nil)
    }

    /// The window as it is drawn now, for a picture of it.
    var frame: NSRect { window.frame }

    var number: Int { window.windowNumber }

    // MARK: - The screens

    /// The working areas of all screens, top growing downwards from the top of the main screen,
    /// as WindowPlace has them. The screen with the menu bar is first.
    static func screens() -> [ScreenArea] {
        let areas = NSScreen.screens.map { screen in
            let frame = screen.visibleFrame
            return ScreenArea(left: frame.minX, top: mainTop - frame.maxY, width: frame.width, height: frame.height)
        }
        return areas.isEmpty ? [ScreenArea(left: 0, top: 0, width: 1440, height: 900)] : areas
    }

    /// AppKit counts up from the bottom of the main screen; WindowPlace counts down from its top.
    private static var mainTop: Double {
        Double(NSScreen.screens.first?.frame.maxY ?? 900)
    }

    private static func rect(_ place: WindowPlace) -> NSRect {
        NSRect(x: place.left, y: mainTop - place.top - place.height, width: place.width, height: place.height)
    }

    // MARK: - NSWindowDelegate

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        mayClose?() ?? true
    }

    func windowWillClose(_ notification: Notification) {
        onClosing?()
        // The Dock icon goes with the window: Aiko is back in the menu bar only.
        NSApp.setActivationPolicy(.accessory)
        onClosed?()
    }

    func windowDidResize(_ notification: Notification) {
        centreWindowButtons()
    }

    func windowDidExitFullScreen(_ notification: Notification) {
        centreWindowButtons()
    }

    /// The three buttons of the window stand in the middle of Aiko's bar, which is taller than the
    /// title bar of macOS. The bar of macOS is made as tall as Aiko's, and the buttons move down
    /// to its middle. In full screen the system hides them, and they stay as they are.
    private func centreWindowButtons() {
        guard !window.styleMask.contains(.fullScreen),
              let close = window.standardWindowButton(.closeButton),
              let titleBar = close.superview,
              let container = titleBar.superview
        else {
            return
        }

        let height = TerminalPanelView.barHeight
        container.frame = NSRect(
            x: container.frame.minX, y: window.frame.height - height,
            width: container.frame.width, height: height)
        titleBar.frame = container.bounds

        let kinds: [NSWindow.ButtonType] = [.closeButton, .miniaturizeButton, .zoomButton]
        for kind in kinds {
            guard let button = window.standardWindowButton(kind) else { continue }
            button.setFrameOrigin(NSPoint(
                x: button.frame.minX,
                y: ((height - button.frame.height) / 2).rounded()))
        }
    }
}
