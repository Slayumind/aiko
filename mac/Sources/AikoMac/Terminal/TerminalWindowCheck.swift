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
    static let states = ["talk", "feed", "compacting", "long-input", "empty", "form", "elsewhere", "login", "close"]

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

        // Twelve lines typed after the window opened: more than the field shows, so it scrolls
        // and keeps the end in view (AIKO-245).
        if state == "long-input" {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                panel.input.text = (1...12).map { "Line \($0) of a long message." }.joined(separator: "\n")
            }
        }

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
            talk: state == "feed" || state == "long-input" ? feed() : state == "compacting" ? feed(compacting: true) : nil,
            now: now)
    }

    /// A short made up conversation: a message, steps, a markdown answer, a card that waits and a
    /// queued message. The same as the feed of TerminalSnapshot.cs. While compacting, there is no
    /// card, and a notice of an earlier compacting stands above the queued message (D-325).
    private static func feed(compacting: Bool = false) -> TalkScreen {
        func step(_ id: Int, _ kind: StepKind, _ subject: String, added: Int = 0, removed: Int = 0) -> FeedItem {
            FeedItem(id: id, content: .step(FeedStep(
                toolUseId: "t\(id)",
                line: StepLine(kind: kind, label: "", subject: subject, added: added, removed: removed, hidden: false),
                state: .done, result: "", images: [], progress: "")))
        }

        // A skill and two MCP tools, named the way the core names real calls (D-324).
        func tool(_ id: Int, _ name: String, _ input: JsonObject = JsonObject()) -> FeedItem {
            FeedItem(id: id, content: .step(FeedStep(
                toolUseId: "t\(id)", line: StepLine.forTool(name, input),
                state: .done, result: "", images: [], progress: "")))
        }

        let answer = [
            "## Where the groups come from",
            "",
            "The groups come from the core: **`NewSessionChoices.Headers`** says where each one starts. See [the form](mac/Sources/AikoMac/Terminal/TerminalPanel.swift:300).",
            "",
            "9. A header before the first *recent* folder.",
            "10. A header before the first bound folder.",
            "11. No header when a group is empty.",
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
            tool(21, "Skill", JsonObject([("skill", .string("aiko:copy"))])),
            tool(22, "mcp__plugin_aiko-persona_memory__remember"),
            tool(23, "mcp__claude_ai_Google_Calendar__list_events"),
            step(5, .edit, "SettingsControls.swift", added: 18, removed: 2),
        ]
        if compacting {
            items.append(FeedItem(id: 30, content: .notice(
                kind: .compacted, text: "", size: CompactSize(before: 46425, after: 6302))))
        } else if case .request(let asked) = StreamEvent.fromLine(request) {
            items.append(FeedItem(id: 6, content: .card(request: RequestCard.from(asked), state: .open)))
        }
        items.append(FeedItem(id: 7, content: .ownerMessage(
            text: "And the same on Windows, please.", images: 0, queued: true, attachments: [])))

        return TalkScreen(
            session: "a2", items: items, busy: true, hasOpenCard: !compacting, compacting: compacting, mode: .manual, model: ModelName.label(asked: nil, known: "claude-opus-5-5"),
            environment: "Aiko")
    }
}
