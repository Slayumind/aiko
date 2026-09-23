import AikoKit
import AppKit
import Foundation

/// The environments while the settings window is open.
///
/// There is no Save button (D-179): every change comes through `commit`, which writes the file and
/// puts the change into effect outside Aiko right away. What a change means for the list is decided
/// in AikoKit.EnvironmentEdits; this class only does the writing. The twin of EnvironmentsEditor.cs.
@MainActor
final class EnvironmentsEditor {
    private(set) var current = Store.environments()

    /// Called after every change that was saved.
    var onChanged: (() -> Void)?

    /// Installed commands are known to terminals and scripts, so a rename leaves them alone (D-180).
    var commandsInstalled: Bool { CommandFolder.isSetUp }

    /// The line in the log names the change, never the environment: names are the person's words.
    func commit(_ next: EnvironmentSettings, _ what: String) {
        guard next != current else { return }

        let before = current
        current = next
        Store.saveEnvironments(next)

        if CommandFolder.isSetUp && commands(before) != commands(next) {
            CommandFolder.sync(next)
        }

        if !hasBindings(before) && hasBindings(next) {
            addReminderHooks(next)
        }

        if personaFolders(before) != personaFolders(next) {
            PluginSync.personaChanged()
        }

        Log.write("settings: \(what)")
        onChanged?()
    }

    /// Takes Aiko's line out of the environment's folder first, then the environment out of the
    /// list. Answers whether the folder went to the Trash.
    func remove(_ environment: String, toTrash: Bool) -> Bool {
        guard let removed = current.environments.first(where: { $0.name == environment }),
              EnvironmentEdits.canRemove(.macOS, current, environment, Store.home)
        else {
            return false
        }

        for folder in removed.configDirectories {
            _ = ClaudeSettingsFile.removeAiko(folder)
        }

        commit(EnvironmentEdits.remove(.macOS, current, environment, Store.home), "removed an environment")

        // A folder in the Trash took its plugins with it. Only a folder that stayed is worth
        // asking Claude Code about.
        let trashed = toTrash && removed.configDirectories.allSatisfy(Trash.send)
        if !trashed {
            PluginSync.remove(removed.configDirectories, "removed an environment")
        }

        return trashed
    }

    /// Reads the file again after the checklist wrote it.
    func reload() {
        current = Store.environments()
        onChanged?()
    }

    /// Takes back everything Aiko set up for the environments and forgets them. The accounts stay
    /// in their folders.
    func startOver(trashSecond: Bool) {
        let before = current
        for folder in before.environments.flatMap(\.configDirectories) {
            _ = ClaudeSettingsFile.removeAiko(folder)
        }

        EnvironmentSetup.undo()
        current = .empty
        Store.saveEnvironments(current)
        Log.write("settings: started over")

        let trashed = trashSecond
            ? (before.second(.macOS, Store.home)?.configDirectories ?? [])
            : []
        for folder in trashed {
            _ = Trash.send(folder)
        }

        // The persona file stays: the temperament, the face and the skills are kept for next time.
        PluginSync.remove(
            before.environments.flatMap(\.configDirectories).filter { folder in
                !trashed.contains { RealClaude.sameFolder($0, folder) }
            },
            "started over")

        onChanged?()
    }

    private func commands(_ settings: EnvironmentSettings) -> Set<String> {
        Set(settings.environments.map { $0.command.lowercased() })
    }

    private func personaFolders(_ settings: EnvironmentSettings) -> Set<String> {
        Set(settings.environments.filter(\.persona).flatMap(\.configDirectories).map { $0.lowercased() })
    }

    private func hasBindings(_ settings: EnvironmentSettings) -> Bool {
        settings.environments.contains { !$0.projectFolders.isEmpty }
    }

    /// The reminder goes only where the person already let Aiko write its line: a folder they said
    /// "not now" to is not ours to change.
    private func addReminderHooks(_ settings: EnvironmentSettings) {
        guard let bridge = BridgePath.current() else { return }

        for folder in settings.environments.flatMap(\.configDirectories)
        where ClaudeSettingsFile.hasOurLine(folder) {
            _ = ClaudeSettingsFile.addSessionHook(folder, bridge)
        }
    }
}

/// Sends a folder to the Trash instead of deleting it, so the person can take it back. The twin of
/// the RecycleBin helper on Windows, which calls SHFileOperation for the same reason.
enum Trash {
    @discardableResult
    static func send(_ folder: String) -> Bool {
        var directory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder, isDirectory: &directory), directory.boolValue
        else {
            return false
        }

        var sent = false
        do {
            try FileManager.default.trashItem(at: URL(fileURLWithPath: folder), resultingItemURL: nil)
            sent = true
        } catch {
            sent = false
        }

        Log.write("trash: \((folder as NSString).lastPathComponent) \(sent ? "sent" : "not sent")")
        return sent
    }
}

/// Everything the settings window is looking at. One object, so a change on any page redraws the
/// menu beside it. The twin of what SettingsPanel.xaml.cs keeps in fields.
@MainActor
final class SettingsState: ObservableObject {
    let editor = EnvironmentsEditor()

    @Published var page: SettingsPage
    @Published var environments: EnvironmentSettings
    @Published var app = Store.settings()
    @Published var persona = Store.persona()
    @Published var saved = false

    /// The checklist keeps its answers while the person looks at other pages, until Finish or
    /// until the window closes.
    @Published var checklist: ChecklistModel?

    /// The words shown at the top of the environment page after the checklist or a removal.
    @Published var note = ""

    /// True while Aiko is removing itself, so the page can wait instead of taking a second press.
    @Published var deleting = false

    /// Set by the menu item that asks for an update check, so the general page asks as it opens.
    @Published var askForUpdate = false

    // ---- the two fields of the environment page ----
    //
    // They live here and not in the view because Esc has to apply what was typed on the way out,
    // and a view that is already going away cannot be asked for anything (D-179).

    @Published var typedName = ""
    @Published var typedCommand = ""
    @Published var nameProblem = NameProblem.none
    @Published var commandProblem = CommandProblem.none

    /// A command Aiko offers after a rename, when the old one no longer follows the name (D-180).
    @Published var suggestion: String?

    private var editingFolder: String?

    /// Read once per folder: .claude.json can be large and the plan does not change while a
    /// window is open.
    private var plans: [String: String] = [:]

    var onClose: () -> Void = {}
    var onQuit: () -> Void = {}
    var onChanged: () -> Void = {}
    var onReopen: () -> Void = {}

    private var savedUntil: Task<Void, Never>?

    init(page: SettingsPage?) {
        let environments = Store.environments()
        self.environments = environments
        self.page = page ?? SettingsNav.firstPage(environments, .macOS, Store.home)

        editor.onChanged = { [weak self] in self?.onEnvironmentsChanged() }

        // Through newChecklist, never ChecklistModel directly: a model made without it has no
        // onFinished, so Finish did all the work and the window stayed on the checklist. That is
        // the path a fresh machine takes, where the window opens on the checklist right away.
        if self.page == .checklist {
            checklist = newChecklist(at: nil)
        }
    }

    var navRows: [SettingsNavEntry] {
        var byFolder: [String: String] = [:]
        for folder in environments.environments.flatMap(\.configDirectories) {
            byFolder[folder] = plan(of: folder)
        }

        return SettingsNav.rows(
            environments: environments,
            plans: byFolder,
            sendsStats: app.sendStats,
            checklistOpen: checklist != nil,
            platform: .macOS,
            userProfile: Store.home)
    }

    /// The plan label of one config folder, read once and kept.
    func plan(of folder: String) -> String {
        if let known = plans[folder] { return known }

        let label = Store.account(in: folder).planLabel
        plans[folder] = label
        return label
    }

    /// Leaving a page applies what was typed on it, the same as leaving a field. The twin of
    /// SettingsPanel.LeaveCurrentPage.
    func show(_ page: SettingsPage) {
        stopEditing()
        note = ""
        self.page = page
    }

    /// Opens the checklist page, at one item when asked. Answers already given stay.
    func openChecklist(at item: ChecklistItem? = nil) {
        if checklist == nil {
            checklist = newChecklist(at: item)
        } else if let item {
            checklist?.open(item)
        }

        show(.checklist)
    }

    /// Called when the window closes: what was typed is applied, and the checklist stops waiting
    /// for anything outside Aiko.
    func leave() {
        stopEditing()
        checklist?.close()
        savedUntil?.cancel()
    }

    // ---- the name and the command, applied on Enter or when the field is left ----

    func startEditing(_ folder: String) {
        guard let environment = environments.environments.first(where: { $0.holds(folder) }) else {
            return
        }

        editingFolder = folder
        typedName = environment.name
        typedCommand = environment.command
        nameProblem = .none
        commandProblem = .none
        suggestion = nil
    }

    func stopEditing() {
        commitName()
        commitCommand()
        editingFolder = nil
    }

    func commitName() {
        guard let folder = editingFolder,
              let environment = environments.environments.first(where: { $0.holds(folder) })
        else {
            return
        }

        let wanted = typedName.trimmingCharacters(in: .whitespacesAndNewlines)
        let problem = wanted == environment.name
            ? NameProblem.none
            : EnvironmentEdits.checkName(environments, environment.name, wanted)
        nameProblem = problem

        guard problem == .none, wanted != environment.name else { return }

        let renamed = EnvironmentEdits.rename(
            environments, environment.name, wanted, commandsInstalled: editor.commandsInstalled)
        suggestion = renamed.suggestedCommand
        editor.commit(renamed.settings, "renamed an environment")
    }

    func commitCommand() {
        guard let folder = editingFolder,
              let environment = environments.environments.first(where: { $0.holds(folder) })
        else {
            return
        }

        let wanted = typedCommand.trimmingCharacters(in: .whitespacesAndNewlines)
        guard wanted != environment.command else {
            commandProblem = .none
            return
        }

        let problem = EnvironmentEdits.checkCommand(environments, environment.name, wanted)
        commandProblem = problem
        guard problem == .none else { return }

        suggestion = nil
        editor.commit(
            EnvironmentEdits.setCommand(environments, environment.name, wanted), "changed a command")
    }


    func saveApp(_ next: AppSettings) {
        guard next != app else { return }
        app = next
        Store.saveSettings(next)
        showSaved()
    }

    func savePersona(_ next: PersonaSettings) {
        guard next != persona else { return }
        persona = next
        Store.savePersona(next)
        showSaved()
    }

    func showSaved() {
        saved = true
        savedUntil?.cancel()
        savedUntil = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 1_600_000_000)
            guard !Task.isCancelled else { return }
            self?.saved = false
        }

        onChanged()
    }

    /// Everything Aiko set up goes, and the checklist opens from the start, in this same window.
    func startOver(trashSecond: Bool) {
        checklist?.close()
        checklist = nil
        editor.startOver(trashSecond: trashSecond)
        openChecklist()
    }

    /// Aiko takes everything back and goes. Claude Code may take seconds to answer, so the work
    /// happens off this thread and the page waits with a line; then Aiko quits, whether or not it
    /// managed to put itself in the Bin.
    func deleteAiko() {
        guard !deleting else { return }
        deleting = true
        checklist?.close()
        checklist = nil

        Task.detached {
            let outcome = Uninstall.everything()
            await MainActor.run { [weak self] in
                if outcome == .appStays {
                    NSWorkspace.shared.activateFileViewerSelecting([Bundle.main.bundleURL])
                }
                self?.onQuit()
            }
        }
    }

    func showNoteOnFirstEnvironment(_ text: String) {
        page = SettingsNav.firstPage(environments, .macOS, Store.home)
        note = text
    }

    private func newChecklist(at item: ChecklistItem?) -> ChecklistModel {
        let model = ChecklistModel(openFirst: item)
        model.onFinished = { [weak self] note in
            guard let self else { return }
            self.checklist = nil
            self.editor.reload()
            self.showNoteOnFirstEnvironment(note)
        }
        return model
    }

    private func onEnvironmentsChanged() {
        environments = editor.current
        plans = [:]
        showSaved()
    }
}
