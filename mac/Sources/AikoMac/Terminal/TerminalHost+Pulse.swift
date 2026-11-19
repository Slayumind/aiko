import AikoKit
import AppKit
import UserNotifications

/// What the sessions do (D-352, D-360, D-364, D-409): the title Claude Code sets, its last hook
/// event and what the person typed make the mark and the second line of a row; the counts go to
/// the menu bar icon, the island and the Dock, and a session that starts to wait plays the sound.
/// The twin of the same part of TerminalHost.cs.
extension TerminalHost {
    /// Milliseconds since 1970, as the events count time.
    static var nowMs: Int64 { Int64(Date().timeIntervalSince1970 * 1000) }

    func wirePulse() {
        events.changed = { [weak self] id, event in self?.eventCame(id, event) }
    }

    /// Claude Code set its title. The turning mark changes every second while a turn goes on, so
    /// the list is drawn only when working or the name changed.
    func titleCame(_ id: String, _ text: String) {
        guard let title = ClaudeTitle.parse(text), titles[id] != title else { return }
        titles[id] = title
        if title.name != ClaudeTitle.defaultName {
            let titled = sessions.titled(id, title.name)
            if titled != sessions {
                sessions = titled
                save()
            }
        }
        draw()
    }

    private func eventCame(_ id: String, _ event: TerminalEvent) {
        guard terminals[id] != nil else { return }
        lastEvents[id] = event
        followConversation(id, event.conversation)
        draw()
    }

    /// /clear moved the session to a new conversation: the next start comes back to that one.
    func followConversation(_ id: String, _ conversation: String) {
        let moved = sessions.moveToConversation(id, conversation)
        if moved != sessions {
            sessions = moved
            save()
        }
    }

    /// The person typed into the session: a question it asked is answered.
    func typed(_ id: String) {
        let waiting = live(id)?.activity == .waiting
        answeredAt[id] = Self.nowMs
        if waiting {
            draw()
        }
    }

    func forgetPulse(_ id: String) {
        titles[id] = nil
        lastEvents[id] = nil
        seenAt[id] = nil
        answeredAt[id] = nil
        activities[id] = nil
        exitedAt[id] = nil
        events.forget(id)
    }

    func live(_ id: String) -> LiveTerminal? {
        if exitedAt[id] != nil {
            return LiveTerminal(activity: .exited, line: "")
        }
        guard terminals[id] != nil else { return nil }

        let last = lastEvents[id]
        let activity = TerminalPulse.of(titles[id], last, seenAt: seenAt[id] ?? 0, answeredAt: answeredAt[id] ?? 0)
        // The end of a turn says nothing the mark does not: the line shows the time again.
        return LiveTerminal(activity: activity, line: last.map { $0.kind == .finished ? "" : Self.eventLine($0) } ?? "")
    }

    /// The second line of a row (D-352): what the session did last, in the words of the person.
    static func eventLine(_ last: TerminalEvent) -> String {
        let format: String = switch last.kind {
        case .thinking: Strings.terminalEventThinking
        case .reading: Strings.terminalEventReading
        case .editing: Strings.terminalEventEditing
        case .running: Strings.terminalEventRunning
        case .searching: Strings.terminalEventSearching
        case .browsing: Strings.terminalEventBrowsing
        case .delegating: Strings.terminalEventDelegating
        case .using: Strings.terminalEventUsing
        case .asksPermission: Strings.terminalEventAsksPermission
        case .asksQuestion: Strings.terminalEventAsksQuestion
        case .failed: Strings.terminalEventFailed
        default: Strings.terminalEventOutOfLimit
        }

        // A format without its detail drops the colon too: "running" rather than "running: ".
        return !last.detail.isEmpty || !format.contains("{0}")
            ? Strings.format(format, last.detail)
            : format.replacingOccurrences(of: ": {0}", with: "").replacingOccurrences(of: " {0}", with: "")
    }

    /// The open session in a window in focus is seen: a turn that ends there is no news.
    func markSeen() {
        guard let window, window.isVisible, window.isKeyWindow, !window.isMiniaturized,
              let open = model.session(sessions) else { return }
        seenAt[open.id] = Self.nowMs
    }

    /// What changed in the sessions since the last drawing: the counts, and the sound of a session
    /// that starts to wait (D-409). Everything that changes a session draws the window, so this is
    /// the one place to look.
    func notice() {
        let counts = SessionCounts.of(terminals.keys.compactMap { id in counted(id).map { (id: id, activity: $0) } })
        if counts != sessionCounts {
            sessionCounts = counts
            showDockBadge(counts.badge)
            onSessionsChanged?(counts)
        }

        var sounds = false
        let sound = Store.settings().notifySound
        let inFocus = window?.isKeyWindow == true && window?.isVisible == true
        let open = model.session(sessions)?.id
        for id in terminals.keys {
            guard let now = live(id)?.activity else { continue }
            let before = activities[id]
            activities[id] = now
            sounds = Notices.sounds(before, now, sound: sound, inFocus: inFocus, open: id == open) || sounds
        }
        if sounds {
            onWaitingStarted?()
        }
    }

    /// What a session counts as: a stop the person has seen since is no longer news.
    private func counted(_ id: String) -> TerminalActivity? {
        let activity = live(id)?.activity
        if activity == .exited, !SessionCounts.stoppedIsNews(stoppedAt: exitedAt[id] ?? 0, seenAt: seenAt[id] ?? 0) {
            return .idle
        }
        return activity
    }

    /// The number on the icon in the Dock while the window is open, as on the button of the taskbar
    /// (D-425). macOS shows it only for an app allowed to badge its icon, so the first sign asks
    /// (D-426); the colour is the one of the system. A finished turn is a dot.
    private func showDockBadge(_ badge: SessionBadge) {
        let label: String? = switch badge.kind {
        case .none: nil
        case .done: "•"
        default: badge.text
        }
        if label != nil && !changingNothing && !askedToBadge {
            askedToBadge = true
            UNUserNotificationCenter.current().requestAuthorization(options: [.badge]) { _, _ in }
        }
        NSApp.dockTile.badgeLabel = label
    }

    /// A click on the mark of "waits" on the island: the window comes forward on its session.
    func openSession(_ id: String) {
        show()
        guard let session = sessions.find(id) else { return }
        model = model.toEnvironment(session.environment, sessions).toSession(id, sessions)
        draw()
    }
}
