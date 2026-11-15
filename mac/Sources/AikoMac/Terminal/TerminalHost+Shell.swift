import AikoKit
import AppKit
import SwiftTerm

/// What a command line session shows in place of its terminal (D-363, D-412): its output as blocks,
/// cut by the marks of the shell, with the search over them. A full-screen program (vim, less)
/// shows the real terminal. The twin of the shell part of TerminalSurface.cs.
@MainActor
final class ShellPane {
    let screen = ShellScreen(columns: 120, rows: 30)
    let blocks: ShellBlocksView
    let find: ShellFindBar
    let shellName: String

    /// The bytes of a character cut in two between two pieces of output.
    private var leftover: [UInt8] = []

    init(shellName: String, folder: String) {
        self.shellName = shellName
        blocks = ShellBlocksView(screen: screen)
        find = ShellFindBar(view: blocks)
        blocks.folder = folder
    }

    /// A piece of output: the screen takes it as text, whole characters only.
    func feed(_ bytes: ArraySlice<UInt8>) {
        var all = leftover + bytes
        let whole = Self.wholeCharacters(all)
        leftover = Array(all[whole...])
        all.removeSubrange(whole...)
        screen.feed(String(decoding: all, as: UTF8.self), now: TerminalHost.nowMs)
        blocks.refresh()
    }

    /// Where the bytes stop being whole UTF-8 characters: a lead byte near the end whose
    /// continuation has not come yet.
    private static func wholeCharacters(_ bytes: [UInt8]) -> Int {
        var at = bytes.count
        var back = 0
        while at > 0 && back < 4 {
            let byte = bytes[at - 1]
            if byte & 0xC0 == 0x80 {
                at -= 1
                back += 1
                continue
            }
            let length = byte >= 0xF0 ? 4 : byte >= 0xE0 ? 3 : byte >= 0xC0 ? 2 : 1
            return length > back + 1 ? at - 1 : bytes.count
        }
        return bytes.count
    }
}

extension TerminalHost {
    /// A command line with the marks of the shell gets its pane; the output reaches both.
    func wireShell(_ terminal: AikoTerminalView, _ session: TerminalSession, _ launch: ShellLaunch) {
        guard launch.marks else { return }
        let pane = ShellPane(shellName: launch.name, folder: session.folder)
        shells[session.id] = pane
        terminal.received = { [weak pane] bytes in pane?.feed(bytes) }
        pane.screen.fullScreenChanged = { [weak self] on in
            Task { @MainActor in
                guard let self else { return }
                self.draw()
                // vim and less read the keys themselves; back in the blocks, the editor has them again.
                if !on, self.model.sessionId == session.id { self.focusOpenTerminal() }
            }
        }
        pane.blocks.explainAsked = { [weak self, weak pane] block, output in
            guard let self else { return }
            self.ask(session, ShellAsk.explain(shell: launch.name, command: block.command, exitCode: block.exitCode ?? 1,
                                               output: output, language: Self.answerLanguage)) { answer in
                pane?.blocks.showExplanation(block.startedAt, answer)
            }
        }
        pane.find.closed = { [weak self] in self?.focusOpenTerminal() }
        pane.blocks.useFont(Self.terminalFont(), zoom: zoom)
        zoomFollowers.append { [weak pane] zoom in
            pane?.blocks.useFont(TerminalHost.terminalFont(), zoom: zoom)
            return pane != nil
        }
    }

    /// The blocks stand where the terminal is, while no full-screen program runs; the terminal keeps
    /// its place underneath, so its size still reaches the shell.
    func showShell(for id: String?, terminal: NSView?) {
        for (key, pane) in shells where key != id {
            pane.blocks.isHidden = true
            pane.find.isHidden = true
        }
        guard let id, let terminal, let pane = shells[id] else { return }

        if pane.blocks.superview !== stage {
            for part in [pane.blocks, pane.find] as [NSView] {
                part.translatesAutoresizingMaskIntoConstraints = false
                stage.addSubview(part, positioned: .above, relativeTo: terminal)
            }
            NSLayoutConstraint.activate([
                pane.blocks.leadingAnchor.constraint(equalTo: terminal.leadingAnchor),
                pane.blocks.trailingAnchor.constraint(equalTo: terminal.trailingAnchor),
                pane.blocks.topAnchor.constraint(equalTo: terminal.topAnchor),
                pane.blocks.bottomAnchor.constraint(equalTo: terminal.bottomAnchor),
                pane.find.topAnchor.constraint(equalTo: terminal.topAnchor, constant: 8),
                pane.find.trailingAnchor.constraint(equalTo: terminal.trailingAnchor, constant: -16),
            ])
        }
        let blocks = !pane.screen.fullScreen
        pane.blocks.isHidden = !blocks
        terminal.alphaValue = blocks ? 0 : 1
        if !blocks {
            pane.find.isHidden = true
        }
    }

    /// Cmd+Shift+F: the search over the blocks of the open command line. False when there are none.
    func openFind() -> Bool {
        guard let id = model.sessionId, let pane = shells[id], !pane.blocks.isHidden else { return false }
        pane.find.open()
        return true
    }

    /// Words in a command line asked for a command: Claude Code proposes one under the field.
    func suggest(_ words: String) {
        guard let session = model.session(sessions), session.kind == .shell else { return }
        let shell = shells[session.id]?.shellName ?? ShellLaunch.find().name
        ask(session, ShellAsk.suggest(shell: shell, folder: session.folder, request: words)) { [weak self] answer in
            self?.editor.showSuggestion(words, answer.flatMap { ShellAsk.commandIn($0) })
        }
    }

    /// One question to Claude Code with claude -p, in the limits of the session's environment
    /// (D-360). The answer comes back on the main thread; nil when Claude Code is not there or did
    /// not answer.
    func ask(_ session: TerminalSession, _ question: String, answered: @escaping @MainActor (String?) -> Void) {
        let environment = Store.environments().environments.first { $0.name == session.environment }
        let config = environment?.configDirectories.first ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home)
        guard let claude = ClaudeLauncher.findClaude() else {
            answered(nil)
            return
        }
        Task.detached {
            let answer = ClaudeCliProcess(claude: claude).ask(configDirectory: config, question: question, timeout: 60)
            await MainActor.run { answered(answer) }
        }
    }

    /// The language Claude Code answers in: the language of Aiko.
    static var answerLanguage: String { Strings.language == .russian ? "Russian" : "English" }
}

extension ClaudeCliProcess {
    /// The model of a short question: an explanation or a command (D-358), as on Windows.
    private static let questionModel = "haiku"

    /// One question with claude -p; the answer, or nil when it failed or took longer than timeout.
    func ask(configDirectory: String, question: String, timeout: TimeInterval) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: claude)
        process.arguments = ["-p", question, "--model", Self.questionModel]
        process.currentDirectoryURL = URL(fileURLWithPath: NSTemporaryDirectory())
        var environment = ProcessEnvironment.variables([])
        for name in environment.keys
        where name == "CLAUDECODE" || name == ClaudeConfigFolder.variableName
            || name.hasPrefix("CLAUDE_CODE_") || name.hasPrefix("AIKO_") {
            environment.removeValue(forKey: name)
        }
        if !ClaudeConfigFolder.isDefault(.macOS, configDirectory, NSHomeDirectory()) {
            environment[ClaudeConfigFolder.variableName] = configDirectory
        }
        process.environment = environment
        let output = Pipe()
        process.standardOutput = output
        process.standardError = FileHandle.nullDevice
        process.standardInput = FileHandle.nullDevice

        // The answer is read while it comes, so a long one never fills the pipe and holds the program.
        let reading = Reading(output.fileHandleForReading)
        do {
            try process.run()
        } catch {
            return nil
        }
        let deadline = Date().addingTimeInterval(timeout)
        while process.isRunning && Date() < deadline {
            Thread.sleep(forTimeInterval: 0.05)
        }
        if process.isRunning {
            process.terminate()
            return nil
        }
        let text = reading.all().trimmingCharacters(in: .whitespacesAndNewlines)
        return process.terminationStatus == 0 && !text.isEmpty ? text : nil
    }

    /// The output of a program, gathered on a thread of its own.
    private final class Reading: @unchecked Sendable {
        private var data = Data()
        private let done = DispatchSemaphore(value: 0)

        init(_ handle: FileHandle) {
            DispatchQueue.global().async { [self] in
                data = handle.readDataToEndOfFile()
                done.signal()
            }
        }

        func all() -> String {
            _ = done.wait(timeout: .now() + 5)
            return String(decoding: data, as: UTF8.self)
        }
    }
}
