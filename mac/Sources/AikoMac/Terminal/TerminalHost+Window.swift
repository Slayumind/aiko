import AikoKit
import AppKit
import SwiftUI

/// The window of the terminal: the title bar of Aiko over the title of the system, the column of
/// sessions that slides away, and the stage of the terminal. The twin of TerminalPanel.cs and its
/// title bar (TerminalPanel.TitleBar.cs).
extension TerminalHost {
    // ---- the window ----

    func makeWindow() -> NSWindow {
        let place = WindowPlace.fit(nil, Self.screens())
        // The bar of Aiko is the title bar (D-392): the title of the system is see-through and empty,
        // and its three buttons stand in the middle of the bar.
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: place.width, height: place.height),
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
        window.center()

        let settings = Store.settings()
        state.headerOpen = settings.headerOpen
        state.listHidden = settings.sessionListHidden

        // The title of the system is a safe area for SwiftUI; the bar draws over it on purpose.
        let bar = NSHostingView(rootView: TitleBarView(state: state))
        bar.safeAreaRegions = []
        let list = NSHostingView(rootView: SessionListView(state: state))
        list.safeAreaRegions = []
        self.bar = bar
        let strip = NSHostingView(rootView: HeaderStripView(state: state))
        strip.safeAreaRegions = []
        strip.wantsLayer = true
        strip.layer?.masksToBounds = true
        let cover = NSHostingView(rootView: StageView(state: state))
        cover.safeAreaRegions = []
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
        let stripHeight = strip.heightAnchor.constraint(equalToConstant: state.headerOpen ? HeaderStripView.height : 0)
        headerHeight = stripHeight
        let listWidth = listHolder.widthAnchor.constraint(equalToConstant: state.listHidden ? 0 : Self.listWidth)
        listWidthConstraint = listWidth
        NSLayoutConstraint.activate([
            bar.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            bar.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            bar.topAnchor.constraint(equalTo: root.topAnchor),
            bar.heightAnchor.constraint(equalToConstant: TitleBarView.height),
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
            list.widthAnchor.constraint(equalToConstant: Self.listWidth),
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
        placeWindowButtons(window)
        watchKeys()
        return window
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

        let height = TitleBarView.height
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
        let height = state.headerOpen ? HeaderStripView.height : 0
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
        let width = state.listHidden ? 0 : Self.listWidth
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
