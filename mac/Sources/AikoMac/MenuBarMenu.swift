import AikoKit
import AppKit

/// What a person can pick in the menu of the icon. The names are those of TrayCommand on Windows.
/// Windows has one more, None, for a menu closed with nothing picked: there the menu answers with
/// a value, and here a closed menu simply calls nobody.
enum TrayCommand {
    case swapRing
    case settings
    case refresh
    case updates
    case exit
}

/// The menu under a right click on the menu bar icon. It builds the menu and says what was picked;
/// what each command does is the shell's business. The twin of TrayMenu.cs.
@MainActor
final class MenuBarMenu: NSObject {
    private var picked: ((TrayCommand) -> Void)?

    /// otherEnvironment is the one the ring does not show. With a single environment it is nil and
    /// the menu leaves the item out rather than showing one that does nothing.
    func build(otherEnvironment: String?, picked: @escaping (TrayCommand) -> Void) -> NSMenu {
        self.picked = picked

        let menu = NSMenu()
        menu.autoenablesItems = false

        // The click on the icon opens the card, so swapping the ring needs a home. Here it says
        // which environment it would show, which the click never did.
        if let other = otherEnvironment {
            add(menu, Strings.format(Strings.menuShowInRing, other), #selector(onSwapRing))
            menu.addItem(.separator())
        }

        add(menu, Strings.settings, #selector(onSettings))
        add(menu, Strings.menuRefresh, #selector(onRefresh))
        add(menu, Strings.checkForUpdates, #selector(onCheckUpdates))
        menu.addItem(.separator())
        add(menu, Strings.quitAiko, #selector(onQuit))
        return menu
    }

    private func add(_ menu: NSMenu, _ title: String, _ action: Selector) {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self
        item.isEnabled = true
        menu.addItem(item)
    }

    @objc private func onSwapRing() { picked?(.swapRing) }
    @objc private func onSettings() { picked?(.settings) }
    @objc private func onRefresh() { picked?(.refresh) }
    @objc private func onCheckUpdates() { picked?(.updates) }
    @objc private func onQuit() { picked?(.exit) }
}
