import AikoKit
import AppKit

/// --try-terminal <state>: the window of the terminal on made up sessions, held on screen for a
/// picture from another computer, then Aiko quits. Nothing is read from the person's sessions
/// and nothing is saved. "real" opens the real terminal instead and leaves it open.
///
/// The twin of Terminal/TerminalSnapshot.cs. Windows draws the panel into a file; here the
/// window stands on the screen, and AikoShot takes the picture (/look).
@MainActor
enum TerminalWindowCheck {
    static let states = ["talk", "feed", "empty", "form", "elsewhere", "login", "close"]

    /// Kept alive while the window is shown.
    private static var shown: (TerminalWindow, TerminalPanelState)?

    static func run(_ state: String, shell: AikoShell) {
        if state == "real" {
            let host = shell.openTerminal()
            report(states.count, host.frameForCheck, host.windowNumberForCheck)
            return
        }

        guard let number = states.firstIndex(of: state) else {
            report(-1, .zero, 0)
            NSApp.terminate(nil)
            return
        }

        let width = Double(ProcessInfo.processInfo.environment["AIKO_TRY_WIDTH"] ?? "") ?? WindowPlace.defaultWidth
        let seconds = Double(ProcessInfo.processInfo.environment["AIKO_TRY_SECONDS"] ?? "") ?? 4

        let panel = TerminalPanelState()
        panel.show(screen(state))
        let place = WindowPlace.fit(
            WindowPlace(left: 80, top: 60, width: width, height: 700, maximized: false), TerminalWindow.screens())
        let window = TerminalWindow(panel: panel, place: place)
        shown = (window, panel)
        window.show()
        report(number, window.frame, window.number)

        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) {
            AppTerminalLog.windowCheckDone(state: number)
            NSApp.terminate(nil)
        }
    }

    private static func report(_ state: Int, _ frame: NSRect, _ window: Int) {
        AppTerminalLog.windowChecked(
            state: state, left: Int(frame.minX), top: Int(frame.minY),
            width: Int(frame.width), height: Int(frame.height), window: window)
    }

    private static func screen(_ state: String) -> TerminalScreen {
        let now = Date()
        let yesterday = Int64(now.addingTimeInterval(-86_400).timeIntervalSince1970)

        func glance(_ activity: TalkActivity, _ line: GlanceLine, _ kind: StepKind, _ text: String) -> TalkGlance {
            TalkGlance(activity: activity, line: line, kind: kind, label: "", text: text)
        }

        let groups: [RowGroup] = state == "empty" ? [] : [
            RowGroup(folder: "aiko", name: "aiko", rows: [
                SessionRow(id: "a1", name: "office: reports", state: .working,
                           glance: glance(.working, .step, .read, "office.mjs"), lastUsed: 0),
                SessionRow(id: "a2", name: "terminal", state: .waiting,
                           glance: glance(.waiting, .request, .edit, "NewSessionForm.cs"), lastUsed: 0),
                SessionRow(id: "a3", name: "memory polish", state: .done,
                           glance: glance(.done, .answer, .other, "Done. Snapshots match in both locales"), lastUsed: 0),
                SessionRow(id: "a4", name: "icon polish", state: .read,
                           glance: glance(.idle, .answer, .other, "Both locales checked, no differences"), lastUsed: 0),
            ]),
            RowGroup(folder: "code-crawler", name: "code-crawler", rows: [
                SessionRow(id: "c1", name: "weapon balance", state: .asleep, glance: nil, lastUsed: yesterday),
                SessionRow(id: "c2", name: "installer build", state: .stopped,
                           glance: glance(.failed, .stoppedAt, .run, "dotnet publish"), lastUsed: 0),
            ]),
        ]

        let folders = [
            FolderChoice(path: "/Users/someone/projects/aiko", name: "aiko", source: .recent),
            FolderChoice(path: "/Users/someone/projects/for-work", name: "for-work", source: .inside),
        ]
        let form: SessionForm? = switch state {
        case "form":
            SessionForm(environment: "Aiko", folders: folders, folder: 0, boundElsewhere: nil, model: 0, defaultName: "aiko 3")
        case "elsewhere":
            SessionForm(environment: "Aiko", folders: folders, folder: 1, boundElsewhere: "Work · Kodland", model: 0,
                        defaultName: "for-work 1")
        default:
            nil
        }

        return TerminalScreen(
            tabs: [
                TerminalTab(name: "Aiko", selected: true, waiting: 0),
                TerminalTab(name: "Work · Kodland", selected: false, waiting: 1),
            ],
            badge: TerminalBadge(percent: 42, tone: .normal, kind: .fiveHour),
            groups: groups,
            environment: "Aiko",
            sessionId: state == "empty" ? nil : "a2",
            sessionName: state == "empty" ? nil : "terminal",
            main: form != nil ? .form : (state == "empty" ? .noSessions : .talk),
            signedOut: state == "login",
            form: form,
            ask: state == "close"
                ? CloseAsk(title: Strings.format(Strings.terminalCloseTitle, "terminal"), body: Strings.terminalCloseBody)
                : nil,
            talk: state == "feed" ? feed() : nil,
            now: now)
    }

    /// A short made up conversation: a message, steps, a markdown answer, a card that waits and a
    /// queued message. The same as the feed of TerminalSnapshot.cs.
    private static func feed() -> TalkScreen {
        func step(_ id: Int, _ kind: StepKind, _ subject: String, added: Int = 0, removed: Int = 0) -> FeedItem {
            FeedItem(id: id, content: .step(FeedStep(
                toolUseId: "t\(id)",
                line: StepLine(kind: kind, label: "", subject: subject, added: added, removed: removed, hidden: false),
                state: .done, result: "", images: [], progress: "")))
        }

        let answer = [
            "## Where the groups come from",
            "",
            "The groups come from the core: **`NewSessionChoices.Headers`** says where each one starts. See [the form](mac/Sources/AikoMac/Terminal/TerminalPanel.swift:300).",
            "",
            "1. A header before the first *recent* folder.",
            "2. A header before the first bound folder.",
            "",
            "```swift",
            "let headers = NewSessionChoices.headers(form.folders)",
            "```",
            "",
            "| Group | Starts at |",
            "|---|---:|",
            "| Recent | 0 |",
            "| Bound | 3 |",
        ].joined(separator: "\n")

        let request = #"{"type":"control_request","request_id":"r1","request":{"subtype":"can_use_tool","tool_name":"Edit","tool_use_id":"t6","input":{"file_path":"/work/aiko/NewSessionForm.swift","old_string":"a\nb","new_string":"a\nc\nd"}}}"#
        var items: [FeedItem] = [
            FeedItem(id: 1, content: .ownerMessage(
                text: "Add the folder groups to the form of a new session.", images: 0, queued: false, attachments: [])),
            step(2, .read, "NewSessionChoices.swift"),
            step(3, .search, "FolderSource"),
            FeedItem(id: 4, content: .answer(text: answer, streaming: false)),
            step(5, .edit, "SettingsControls.swift", added: 18, removed: 2),
        ]
        if case .request(let asked) = StreamEvent.fromLine(request) {
            items.append(FeedItem(id: 6, content: .card(request: RequestCard.from(asked), state: .open)))
        }
        items.append(FeedItem(id: 7, content: .ownerMessage(
            text: "And the same on Windows, please.", images: 0, queued: true, attachments: [])))

        return TalkScreen(
            session: "a2", items: items, busy: true, hasOpenCard: true, mode: .manual, model: "Opus 5.5",
            environment: "Aiko")
    }
}
