import Foundation

// Somebody typed claude or aiko-work and is waiting for Claude Code. Two rules hold above the rest:
// start the real Claude Code whatever happens here, and never start another shim.
exit(Shim.run())
