import AppKit

/// Says when a window takes the whole screen and when it lets it go. The island hides while a game,
/// a video or a presentation owns the screen (D-161).
///
/// The twin of FullScreenWatch.cs. Windows listens for a foreground event; macOS has no such event,
/// so it listens for the two things that come with going full screen — the space changes, or
/// another app comes to the front — and then looks at what is in front. Nothing is asked in
/// between, so Aiko still sleeps while nothing happens.
@MainActor
final class FullScreenWatch {
    private var tokens: [NSObjectProtocol] = []

    /// True while a full screen window is in front.
    var changed: ((Bool) -> Void)?

    init() {
        let workspace = NSWorkspace.shared.notificationCenter
        for name in [
            NSWorkspace.activeSpaceDidChangeNotification,
            NSWorkspace.didActivateApplicationNotification,
            NSWorkspace.didDeactivateApplicationNotification,
        ] {
            tokens.append(workspace.addObserver(forName: name, object: nil, queue: .main) { _ in
                MainActor.assumeIsolated {
                    // A moment later: the window takes the screen after the space has changed.
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
                        self.tell()
                    }
                }
            })
        }
    }

    func stop() {
        for token in tokens {
            NSWorkspace.shared.notificationCenter.removeObserver(token)
        }

        tokens = []
        changed = nil
    }

    private func tell() {
        changed?(FullScreenWatch.isFullScreenInFront())
    }

    /// The window in front covers a whole screen, and it is not ours and not the desktop.
    static func isFullScreenInFront() -> Bool {
        let windows = CGWindowListCopyWindowInfo(
            [.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]]
        guard let windows else { return false }

        let ours = Int(ProcessInfo.processInfo.processIdentifier)

        for window in windows {
            // Layer 0 is an ordinary window. The menu bar, the Dock and our own panels sit above
            // it and are not what is being looked for.
            guard window[kCGWindowLayer as String] as? Int == 0,
                  window[kCGWindowOwnerPID as String] as? Int != ours,
                  let bounds = window[kCGWindowBounds as String] as? [String: Any],
                  let rect = CGRect(dictionaryRepresentation: bounds as CFDictionary) else {
                continue
            }

            // The first ordinary window in the list is the front one, and it answers for all.
            return covers(rect)
        }

        return false
    }

    /// CGWindowList counts from the top left, as the core does, so a screen is turned over once and
    /// the two are compared as they are.
    private static func covers(_ window: CGRect) -> Bool {
        NSScreen.screens.contains { screen in
            let frame = Screens.box(screen.frame)
            return window.minX <= frame.x + 1
                && window.minY <= frame.y + 1
                && window.maxX >= frame.right - 1
                && window.maxY >= frame.bottom - 1
        }
    }
}
