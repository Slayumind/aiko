import AppKit

/// The menu Aiko never shows.
///
/// An app that lives in the menu bar has no menu bar of its own, but macOS routes the keyboard
/// shortcuts of text editing through the main menu: with no Edit menu there is no Cut, Copy, Paste
/// or Select All in any field. Windows has them in the text box itself, so nothing like this is
/// needed there.
///
/// The menu also answers how tall the menu bar is, which is the one number that tells the island
/// where the top of the screen ends (RESEARCH, 2026-09-18).
@MainActor
enum AppMenu {
    static func install() {
        guard NSApp.mainMenu == nil else { return }

        let edit = NSMenu(title: "Edit")
        add(edit, "Undo", #selector(UndoManager.undo), "z")
        add(edit, "Redo", #selector(UndoManager.redo), "Z")
        edit.addItem(.separator())
        add(edit, "Cut", #selector(NSText.cut(_:)), "x")
        add(edit, "Copy", #selector(NSText.copy(_:)), "c")
        add(edit, "Paste", #selector(NSText.paste(_:)), "v")
        add(edit, "Select All", #selector(NSText.selectAll(_:)), "a")

        let editItem = NSMenuItem()
        editItem.submenu = edit

        let main = NSMenu()
        main.addItem(editItem)
        NSApp.mainMenu = main
    }

    private static func add(_ menu: NSMenu, _ title: String, _ action: Selector, _ key: String) {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: key)
        menu.addItem(item)
    }
}
