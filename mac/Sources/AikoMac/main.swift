import AppKit

// Aiko starts with no Dock icon: it lives in the menu bar or on the island, and takes a Dock icon
// only while its window is open (D-425). Info.plist says LSUIElement, and this says the same when
// the binary is run on its own.
let application = NSApplication.shared
application.setActivationPolicy(.accessory)

let delegate = AikoDelegate()
application.delegate = delegate
application.run()
