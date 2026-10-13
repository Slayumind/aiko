import AikoKit
import Darwin
import Foundation

/// The doors of the terminal that need no window: they check the pipes and the conversation and
/// say in the log what they found. Each returns the exit code of the run.
///
/// The twin of Terminal/TerminalCheck.cs.
enum TerminalCheck {
    private static let patience: TimeInterval = 20

    /// Things handed from one thread to another, taken in order, with a limit on the wait.
    private final class Handed<Thing>: @unchecked Sendable {
        private let condition = NSCondition()
        private var things: [Thing] = []

        func add(_ thing: Thing) {
            condition.lock()
            things.append(thing)
            condition.signal()
            condition.unlock()
        }

        func take(within seconds: TimeInterval) -> Thing? {
            let deadline = Date().addingTimeInterval(seconds)
            condition.lock()
            defer { condition.unlock() }

            while things.isEmpty {
                if !condition.wait(until: deadline) {
                    return nil
                }
            }
            return things.removeFirst()
        }
    }

    /// --try-pipe: starts Aiko as a child of itself through the real process code and checks what
    /// a conversation relies on: letters of any language come back whole, a line of five megabytes
    /// comes back whole, the variables are set and removed, and stopping ends the child together
    /// with the child it started.
    static func pipe() -> Int32 {
        let lines = Handed<String>()
        let exits = Handed<Int>()
        setenv(PipeChild.removedVariable, "still here", 1)

        let process = ClaudeProcess()
        let started = process.start(
            ProcessStart(
                executable: PipeChild.ownPath,
                arguments: [PipeChild.argument],
                variables: [
                    VariableChange(PipeChild.setVariable, "set"),
                    VariableChange(PipeChild.removedVariable, nil),
                ],
                workingDirectory: NSTemporaryDirectory()),
            onLine: { lines.add($0) },
            onExit: { exits.add($0) })

        guard started else {
            AppTerminalLog.pipeChecked(
                started: false, letters: false, longLine: false, variables: false, exitSeen: false, treeGone: false)
            return 1
        }

        func asked(_ line: String) -> String? {
            process.write(line) ? lines.take(within: patience) : nil
        }

        let lettersText = "Привет, こんにちは, 🙂 naïve"
        let letters = asked(lettersText) == lettersText

        let longText = String(repeating: "я🙂a", count: 5 * 1024 * 1024 / 7)
        let longLine = asked(longText) == longText

        let variables = asked("variables") == "set|"

        let family = (asked("spawn") ?? "").split(separator: " ").map { pid_t($0) ?? 0 }
        let bothStarted = family.count == 2 && family.allSatisfy(isAlive)

        process.stop()
        let exitSeen = exits.take(within: patience) != nil
        let treeGone = bothStarted && waitUntilGone(family)

        AppTerminalLog.pipeChecked(
            started: true, letters: letters, longLine: longLine, variables: variables, exitSeen: exitSeen,
            treeGone: treeGone)
        return letters && longLine && variables && exitSeen && treeGone ? 0 : 1
    }

    /// --try-conversation <script>: plays a conversation against a scripted Claude Code, with the
    /// real log behind it. The script names a mark, a word that is said in the conversation; the
    /// check fails when the mark is found in the log of Aiko.
    static func conversation(_ scriptPath: String) -> Int32 {
        let json = readOrEmpty(scriptPath)
        let mark = JsonNode.parse(json)?["mark"]?.stringValue ?? ""
        guard let talk = TalkScript.parse(json), !mark.isEmpty else {
            AppTerminalLog.conversationChecked(
                read: false, markSeen: false, logClean: false, items: 0, starts: 0, written: 0)
            return 1
        }

        let claude = ScriptedClaude(talk.replies)
        let runner = ConversationRunner(
            .macOS,
            setup(claude: "claude", folder: NSTemporaryDirectory(), environmentName: nil),
            newProcess: claude.newProcess,
            post: { work in work() },
            log: AppTerminalLog())
        talk.play(runner)
        runner.close()

        let markSeen = claude.written.contains { $0.contains(mark) }
            && talk.replies.contains { reply in reply.lines.contains { $0.contains(mark) } }
        let logClean = !readOrEmpty(Log.filePath).contains(mark)

        AppTerminalLog.conversationChecked(
            read: true, markSeen: markSeen, logClean: logClean, items: runner.state.items.count,
            starts: claude.starts.count, written: claude.written.count)
        return markSeen && logClean ? 0 : 1
    }

    /// --try-conversation real <folder> [environment]: one short turn with the real Claude Code,
    /// on the smallest model. It spends a little of the limits and leaves one conversation in the
    /// history of that folder, so it is run only when the person says so.
    static func real(_ folder: String, environmentName: String?) -> Int32 {
        var isFolder: ObjCBool = false
        guard let claude = ClaudeLauncher.findClaude(),
              FileManager.default.fileExists(atPath: folder, isDirectory: &isFolder), isFolder.boolValue
        else {
            AppTerminalLog.realChecked(claudeFound: false, answered: false, signedOut: false, failed: false, items: 0)
            return 1
        }

        let work = Handed<@Sendable () -> Void>()
        let runner = ConversationRunner(
            .macOS,
            setup(claude: claude, folder: folder, environmentName: environmentName),
            newProcess: { ClaudeProcess() },
            post: { work.add($0) },
            log: AppTerminalLog())

        // Nothing is allowed: the check asks for one word and has no reason to touch a file.
        runner.changed = { [unowned runner] in
            for item in runner.state.items {
                if case .card(let request, .open) = item.content {
                    runner.choose(request.requestId, .deny(""))
                }
            }
        }

        runner.send("Reply with the single word: ok", images: [])

        let deadline = Date().addingTimeInterval(120)
        while runner.state.isBusy && Date() < deadline {
            work.take(within: 0.2)?()
        }

        let state = runner.state
        runner.close()

        let answered = state.items.contains { item in
            if case .answer(let text, _) = item.content { return !text.isEmpty }
            return false
        }
        AppTerminalLog.realChecked(
            claudeFound: true, answered: answered, signedOut: state.activity == .signedOut,
            failed: state.activity == .failed, items: state.items.count)
        return answered ? 0 : 1
    }

    /// --try-history <folder> <session id> [environment]: reads the file Claude Code keeps of a
    /// conversation the way the window will, and counts what came out. The text stays in memory.
    static func history(_ folder: String, sessionId: String, environmentName: String?) -> Int32 {
        let config = setup(claude: "claude", folder: folder, environmentName: environmentName).configDirectory
        let found = TerminalStore.transcriptFile(configDirectory: config, folder: folder, sessionId: sessionId) != nil
        let items = TerminalStore.history(
            configDirectory: config, folder: folder, sessionId: sessionId, mode: .manual).items

        func count(_ matches: (FeedItem.Content) -> Bool) -> Int {
            items.filter { matches($0.content) }.count
        }

        AppTerminalLog.historyChecked(
            found: found,
            items: items.count,
            messages: count { if case .ownerMessage = $0 { true } else { false } },
            answers: count { if case .answer = $0 { true } else { false } },
            steps: count { if case .step = $0 { true } else { false } },
            notices: count { if case .notice = $0 { true } else { false } })
        return found && !items.isEmpty ? 0 : 1
    }

    private static func setup(claude: String, folder: String, environmentName: String?) -> RunnerSetup {
        let environments = Store.environments().environments
        let environment = environments.first { $0.name == environmentName } ?? environments.first

        return RunnerSetup(
            executable: claude,
            workingDirectory: folder,
            configDirectory: environment?.configDirectories.first
                ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home),
            userProfile: Store.home,
            environmentName: environment?.name ?? "",
            sessionId: UUID().uuidString.lowercased(),
            started: false,
            mode: .manual,
            model: "haiku",
            inheritedVariables: Array(ProcessInfo.processInfo.environment.keys))
    }

    private static func readOrEmpty(_ path: String) -> String {
        (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
    }

    private static func waitUntilGone(_ processIds: [pid_t]) -> Bool {
        let deadline = Date().addingTimeInterval(patience)
        while processIds.contains(where: isAlive) && Date() < deadline {
            usleep(100_000)
        }
        return !processIds.contains(where: isAlive)
    }

    private static func isAlive(_ processId: pid_t) -> Bool {
        processId > 0 && kill(processId, 0) == 0
    }
}
