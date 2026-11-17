import AikoKit
import AppKit
import SwiftUI

/// The window of the terminal: the title bar of Aiko over the title of the system, the column of
/// sessions that slides away, and the stage of the terminal. The twin of TerminalPanel.cs and its
/// title bar (TerminalPanel.TitleBar.cs).
extension TerminalHost {
    // ---- the window ----

    func makeWindow() -> NSWindow {
        // The bar of Aiko is the title bar (D-392): the title of the system is see-through and empty,
        // and its three buttons stand in the middle of the bar.
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: WindowPlace.defaultWidth, height: WindowPlace.defaultHeight),
            styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView],
            backing: .buffered,
            defer: false)
        window.title = "Aiko"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.minSize = NSSize(width: WindowPlace.minWidth, height: WindowPlace.minHeight)
        window.backgroundColor = Theme.nsSurface
        window.isReleasedWhenClosed = false
        window.delegate = self
        place(window, sessions.window)

        let settings = Store.settings()
        state.headerOpen = settings.headerOpen
        state.listHidden = settings.sessionListHidden

        let bar = zoomed(TitleBarView(state: state))
        let list = zoomed(SessionListView(state: state))
        self.bar = bar
        let strip = zoomed(HeaderStripView(state: state))
        strip.wantsLayer = true
        strip.layer?.masksToBounds = true
        let cover = zoomed(StageView(state: state))
        stageCover = cover
        let listHolder = NSView()
        listHolder.wantsLayer = true
        listHolder.layer?.masksToBounds = true
        stage.wantsLayer = true
        stage.layer?.backgroundColor = Theme.nsSurface.cgColor

        let root = NSView()
        for view in [bar, strip, listHolder, list, stage, cover] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
        }
        root.addSubview(strip)
        root.addSubview(listHolder)
        listHolder.addSubview(list)
        root.addSubview(stage)
        root.addSubview(cover)
        root.addSubview(bar)

        // The list keeps its width while its column slides, so it is cut, not squeezed.
        let stripHeight = strip.heightAnchor.constraint(equalToConstant: 0)
        headerHeight = stripHeight
        let listWidth = listHolder.widthAnchor.constraint(equalToConstant: 0)
        listWidthConstraint = listWidth
        let barHeight = bar.heightAnchor.constraint(equalToConstant: 0)
        let listInner = list.widthAnchor.constraint(equalToConstant: 0)
        zoomedSizes = (barHeight, listInner)
        NSLayoutConstraint.activate([
            bar.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            bar.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            bar.topAnchor.constraint(equalTo: root.topAnchor),
            barHeight,
            strip.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            strip.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            strip.topAnchor.constraint(equalTo: bar.bottomAnchor),
            stripHeight,
            listHolder.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            listHolder.topAnchor.constraint(equalTo: strip.bottomAnchor),
            listHolder.bottomAnchor.constraint(equalTo: root.bottomAnchor),
            listWidth,
            list.leadingAnchor.constraint(equalTo: listHolder.leadingAnchor),
            list.topAnchor.constraint(equalTo: listHolder.topAnchor),
            list.bottomAnchor.constraint(equalTo: listHolder.bottomAnchor),
            listInner,
            stage.leadingAnchor.constraint(equalTo: listHolder.trailingAnchor),
            stage.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            stage.topAnchor.constraint(equalTo: strip.bottomAnchor),
            stage.bottomAnchor.constraint(equalTo: root.bottomAnchor),
            cover.leadingAnchor.constraint(equalTo: stage.leadingAnchor),
            cover.trailingAnchor.constraint(equalTo: stage.trailingAnchor),
            cover.topAnchor.constraint(equalTo: stage.topAnchor),
            cover.bottomAnchor.constraint(equalTo: stage.bottomAnchor),
        ])
        window.contentView = root
        self.window = window
        layStage()
        applyZoom()
        watchKeys()
        return window
    }

    /// Where the window stood when it was closed last, while that place is still on a screen; the
    /// middle of the first screen otherwise (WindowPlace.fit, the same rule as on Windows).
    func place(_ window: NSWindow, _ saved: WindowPlace?) {
        let place = WindowPlace.fit(saved, Self.screens())
        window.setFrame(Screens.rect(Box(place.left, place.top, place.width, place.height)), display: false)
        if place.maximized != window.isZoomed {
            window.zoom(nil)
        }
    }

    /// A SwiftUI part of the window at the zoom of the window: it follows every change of the zoom.
    func zoomed<Content: View>(_ view: Content) -> ZoomedHostingView<Content> {
        let host = ZoomedHostingView(rootView: view)
        host.zoom = zoom
        // A view that is gone answers false and leaves the list.
        zoomFollowers.append { [weak host] zoom in
            host?.zoom = zoom
            return host != nil
        }
        zoomChecks.append { [weak host] in host.map { $0.seesItsRoomForCheck } }
        return host
    }

    /// The zoom of the window from the settings: 1.15 for 115 % (D-384).
    var zoom: CGFloat { CGFloat(WindowZoom.scale(Store.settings().windowZoom)) }

    /// The height of the bar in whole points. AppKit keeps the title of the system on whole points,
    /// and a height of 50.6 was put back and set again for ever.
    var barHeight: CGFloat { (TitleBarView.height * zoom).rounded() }

    /// Everything that has a size in the window takes the zoom: the bar, the list, the header, the
    /// views of SwiftUI, the window buttons and the font of the terminal.
    func applyZoom() {
        let zoom = self.zoom
        zoomFollowers = zoomFollowers.filter { $0(zoom) }
        Tips.windowZoom = zoom
        zoomedSizes?.bar.constant = barHeight
        zoomedSizes?.list.constant = Self.listWidth * zoom
        placeList(animated: false)
        placeHeader(animated: false)
        let font = Self.terminalFont()
        for terminal in terminals.values where terminal.font != font {
            terminal.font = font
        }
        if let window { placeWindowButtons(window) }
    }

    /// The three buttons of the Mac stand in the middle of the bar, as the three of Windows do: the
    /// title of the system is only 28 points high, so its holder grows to the bar.
    ///
    /// AppKit lays the title out again when the window is shown, resized or leaves full screen, and
    /// puts the buttons back at the top; so the holder tells when its frame changes, and the buttons
    /// move back each time.
    func placeWindowButtons(_ window: NSWindow) {
        guard let close = window.standardWindowButton(.closeButton),
              let holder = close.superview?.superview else { return }
        if holderWatch == nil {
            holder.postsFrameChangedNotifications = true
            holderWatch = NotificationCenter.default.addObserver(
                forName: NSView.frameDidChangeNotification, object: holder, queue: .main
            ) { [weak self, weak window] _ in
                MainActor.assumeIsolated {
                    if let self, let window { self.placeWindowButtons(window) }
                }
            }
        }

        let height = barHeight
        let frame = NSRect(x: holder.frame.minX, y: window.frame.height - height,
                           width: holder.frame.width, height: height)
        if holder.frame != frame {
            holder.frame = frame
        }
        for (index, kind) in [NSWindow.ButtonType.closeButton, .miniaturizeButton, .zoomButton].enumerated() {
            guard let button = window.standardWindowButton(kind) else { continue }
            button.setFrameOrigin(NSPoint(x: 16 + CGFloat(index) * 20,
                                          y: (height - button.frame.height) / 2))
        }
    }

    /// The window took the keyboard: the open session is seen, and its count goes.
    func windowDidBecomeKey(_ notification: Notification) {
        draw()
    }

    func windowDidResize(_ notification: Notification) {
        if let window { placeWindowButtons(window) }
    }

    func windowDidExitFullScreen(_ notification: Notification) {
        if let window { placeWindowButtons(window) }
    }

    /// The list button hides the column of sessions or shows it again (D-376), and remembers it.
    func toggleList() {
        var settings = Store.settings()
        settings.sessionListHidden.toggle()
        Store.saveSettings(settings)
        state.listHidden = settings.sessionListHidden
        placeList(animated: true)
    }

    /// The chevron opens the header with the limits or folds it (D-373), and remembers it.
    func toggleHeader() {
        var settings = Store.settings()
        settings.headerOpen.toggle()
        Store.saveSettings(settings)
        state.headerOpen = settings.headerOpen
        state.header = header()
        placeHeader(animated: true)
    }

    /// The header opens in place and the body moves down: nothing covers a real terminal.
    func placeHeader(animated: Bool) {
        let height = state.headerOpen ? HeaderStripView.height * zoom : 0
        guard let constraint = headerHeight, constraint.constant != height else { return }
        if animated && !Motion.reduceMotion {
            NSAnimationContext.runAnimationGroup { context in
                context.duration = 0.32
                context.timingFunction = CAMediaTimingFunction(name: .easeOut)
                context.allowsImplicitAnimation = true
                constraint.animator().constant = height
                window?.contentView?.layoutSubtreeIfNeeded()
            }
        } else {
            constraint.constant = height
        }
    }

    func placeList(animated: Bool) {
        let width = state.listHidden ? 0 : Self.listWidth * zoom
        guard let constraint = listWidthConstraint, constraint.constant != width else { return }
        if animated && !Motion.reduceMotion {
            NSAnimationContext.runAnimationGroup { context in
                context.duration = 0.2
                context.allowsImplicitAnimation = true
                constraint.animator().constant = width
                window?.contentView?.layoutSubtreeIfNeeded()
            }
        } else {
            constraint.constant = width
        }
    }
}
