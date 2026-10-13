import AppKit

// The doors of the terminal need no window, so they run before there is an application at all.
let arguments = CommandLine.arguments
if arguments.contains(PipeChild.argument) {
    // Aiko as a child of itself, in the place of Claude Code: the other end of --try-pipe.
    PipeChild.run(arguments)
}
if arguments.contains("--try-pipe") {
    exit(TerminalCheck.pipe())
}
if let door = arguments.firstIndex(of: "--try-history"), arguments.count > door + 2 {
    exit(TerminalCheck.history(
        arguments[door + 1], sessionId: arguments[door + 2],
        environmentName: arguments.count > door + 3 ? arguments[door + 3] : nil))
}
if let door = arguments.firstIndex(of: "--try-conversation"), arguments.count > door + 1 {
    // "real" takes one short turn with the real Claude Code and spends a little of the limits.
    if arguments[door + 1] == "real", arguments.count > door + 2 {
        exit(TerminalCheck.real(
            arguments[door + 2], environmentName: arguments.count > door + 3 ? arguments[door + 3] : nil))
    }
    exit(TerminalCheck.conversation(arguments[door + 1]))
}

// Aiko has no Dock icon and no menu of its own: everything happens in the status item and the
// card. Info.plist says LSUIElement, and this says the same when the binary is run on its own.
let application = NSApplication.shared
application.setActivationPolicy(.accessory)

let delegate = AikoDelegate()
application.delegate = delegate
application.run()
