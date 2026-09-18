import AppKit

// Aiko has no Dock icon and no menu of its own: everything happens in the status item and the
// card. Info.plist says LSUIElement, and this says the same when the binary is run on its own.
let application = NSApplication.shared
application.setActivationPolicy(.accessory)

let delegate = AikoDelegate()
application.delegate = delegate
application.run()
