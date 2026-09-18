import Foundation

// Claude Code runs this on every status line update and waits for its output, so two rules hold
// above everything else: be quick, and never fail. Whatever goes wrong, the exit code is 0 and the
// user's own status line still gets printed.
//
// The file is the whole program: the decisions are in AikoKit, this reads stdin, touches the disk
// and starts a process.
exit(Bridge.run(Array(CommandLine.arguments.dropFirst())))
