import AikoKit
import Foundation

/// The environment checklist (D-165), a page of the settings window (D-177).
///
/// The model only gathers facts and keeps the answers. Which item is ready, blocked or done, and
/// which one opens next, is decided by WizardChecklist in the core. Nothing outside Aiko's own
/// settings is written before Finish, except the one thing the person asks for right away: signing
/// in, which happens in Claude Code.
///
/// The twin of the fields and the handlers of Settings/ChecklistPage.xaml.cs.
@MainActor
final class ChecklistModel: ObservableObject {
    static let installCommand = ClaudeCodeInstall.command(.macOS)

    private static var firstFolder: String {
        (Store.home as NSString).appendingPathComponent(ClaudeConfigFolder.defaultFolderName)
    }

    /// Going through the checklist again starts from what is set up now, not from nothing.
    private let existing = Store.environments()

    // ---- facts from the disk ----

    @Published private(set) var claudeInstalled = false
    @Published private(set) var firstSignedIn = false
    @Published private(set) var secondFolder: String?
    @Published private(set) var secondSignedIn = false
    @Published private(set) var candidates: [ClaudeFolder] = []

    // ---- answers ----

    @Published var secondSkipped = false
    @Published var accessGranted: Bool?
    @Published var commandsWanted: Bool?
    @Published var foldersVisited = false
    @Published var personaWanted: Bool?
    @Published var statsWanted: Bool?

    @Published var firstName = ""
    @Published var secondName = ""
    @Published var newName = ""
    @Published var projectFolders: [String] = []
    @Published var defaultIndex = 0
    @Published var island = false
    @Published var runAtStartup = true
    @Published var checkUpdates = false

    /// The command in each field, and the one the person typed when it stopped following the name.
    @Published var commandText = ["", ""]
    private var customCommand: [String?] = [nil, nil]

    // ---- what the page is showing ----

    @Published var opened: ChecklistItem?
    @Published var creating = false
    @Published var firstWaiting = false
    @Published var secondWaiting = false
    @Published var copied = false
    @Published var accessResult: String?

    /// Raised after Finish wrote the environments, with the words for the page that comes next.
    var onFinished: ((String) -> Void)?

    private var waiter: Waiter?

    /// `openFirst` is the item to open: the second environment when the person asked to add one,
    /// and any item for a screenshot. Nil opens the next item that needs an answer.
    init(openFirst: ChecklistItem?) {
        gatherFacts()
        fillFromExisting()
        open(openFirst ?? WizardChecklist.nextToOpen(facts))
    }

    // ---- facts ----

    func gatherFacts() {
        claudeInstalled = ClaudeLauncher.findClaude() != nil
        firstSignedIn = signedIn(Self.firstFolder)
        if let secondFolder {
            secondSignedIn = signedIn(secondFolder)
        }

        candidates = ClaudeFolders.find()
            .filter { !ClaudeConfigFolder.isDefault(.macOS, $0.fullPath, Store.home) && $0.hasCredentials }
            .sorted { ($0.lastUsed ?? .distantPast) > ($1.lastUsed ?? .distantPast) }
    }

    var facts: WizardFacts {
        var facts = WizardFacts()
        facts.claudeInstalled = claudeInstalled
        facts.firstSignedIn = firstSignedIn
        facts.secondSignedIn = secondFolder != nil && secondSignedIn
        facts.secondSkipped = secondSkipped
        facts.accessGranted = accessGranted
        facts.commandsWanted = commandsWanted
        facts.foldersVisited = foldersVisited
        facts.boundFolders = projectFolders.count
        facts.personaWanted = personaWanted
        facts.statsWanted = statsWanted
        return facts
    }

    var words: ChecklistWords {
        var words = ChecklistWords()
        words.secondName = secondNameText
        words.commands = commandsInUse
        words.boundFolders = projectFolders.count
        words.island = island
        return words
    }

    var firstNameText: String {
        let trimmed = firstName.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? Strings.environmentPlainName : trimmed
    }

    var secondNameText: String {
        secondName.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var commandCount: Int { secondFolder == nil ? 1 : 2 }

    var commandsInUse: [String] {
        Array(commandText.prefix(commandCount)).filter { !$0.isEmpty }
    }

    var configFolders: [String] {
        secondFolder == nil ? [Self.firstFolder] : [Self.firstFolder, secondFolder!]
    }

    var environmentNames: [String] {
        [firstNameText, secondNameText].filter { !$0.isEmpty }
    }

    var canFinish: Bool { WizardChecklist.canFinish(facts) }

    var progress: (done: Int, total: Int) { WizardChecklist.progress(facts) }

    func state(of item: ChecklistItem) -> ItemState { WizardChecklist.stateOf(item, facts) }

    private func fillFromExisting() {
        firstName = existing.first(.macOS, Store.home)?.name ?? Strings.environmentPlainName

        // What is already in place counts as answered, so adding a second environment asks only
        // about the second environment.
        if existing.hasEnvironments {
            accessGranted = existing.environments.flatMap(\.configDirectories)
                .allSatisfy(ClaudeSettingsFile.hasOurLine) ? true : nil
            commandsWanted = CommandFolder.isSetUp ? true : nil
        }

        if let second = existing.second(.macOS, Store.home), let folder = second.configDirectories.first {
            choose(second: folder, named: second.name)
            projectFolders = second.projectFolders

            // The default list starts at the default in use, not at environment 1.
            defaultIndex = existing.defaultEnvironmentIn(.macOS, Store.home) == second ? 1 : 0
        }

        let app = Store.settings()

        // A persona that is on stays on. Someone who has seen this item before already had their
        // chance to say yes, so it does not ask again by opening itself.
        personaWanted = existing.environments.contains(where: \.persona) ? true
            : (existing.hasEnvironments && app.meetAikoShown ? false : nil)

        // Answered once is answered: going through the checklist again does not reopen the consent.
        statsWanted = app.privacyAsked ? app.sendStats : nil

        island = app.place == .island
        runAtStartup = existing.hasEnvironments ? LoginItem.isEnabled() : app.runAtStartup
        checkUpdates = app.checkUpdates

        fillCommandFields()
    }

    // ---- the rows ----

    /// One item open at a time, so the window stays the size of what is being done.
    func open(_ item: ChecklistItem?) {
        opened = item

        if item == .projectFolders {
            foldersVisited = true
        }

        if item == .install && !claudeInstalled {
            wait(Waiter.forClaude { [weak self] in
                self?.claudeInstalled = true
                self?.moveOn()
            })
        }
    }

    func toggle(_ item: ChecklistItem) {
        open(opened == item ? nil : item)
    }

    /// After an item gets its answer, the next open one unfolds by itself.
    func moveOn() {
        gatherFacts()
        fillCommandFields()
        open(WizardChecklist.nextToOpen(facts))
    }

    /// Stops waiting for Claude Code or a sign-in. The page lives on while the person looks at
    /// other pages, so this is called when the window closes, not when the page hides.
    func close() {
        waiter?.stop()
        waiter = nil
    }

    private func wait(_ next: Waiter) {
        waiter?.stop()
        waiter = next
    }

    // ---- environment 1 ----

    func signInFirst() {
        guard ClaudeLauncher.open(configFolder: Self.firstFolder, workingDirectory: Store.home) else {
            return
        }

        firstWaiting = true
        wait(Waiter.forSignIn(Self.firstFolder) { [weak self] in
            self?.firstWaiting = false
            self?.moveOn()
        })
    }

    var firstAccountLine: String {
        let account = Store.account(in: Self.firstFolder)
        return [account.email, account.planLabel]
            .compactMap { $0 }
            .filter { !$0.isEmpty }
            .joined(separator: " · ")
    }

    // ---- environment 2 ----

    func choose(second folder: String, named name: String) {
        secondFolder = folder
        secondSkipped = false
        creating = false
        secondSignedIn = signedIn(folder)
        secondName = existing.environments.first { $0.holds(folder) }?.name ?? name
        fillCommandFields()
    }

    /// The folder name is shown while typing, so nobody is surprised by .claude-osnovnaya later.
    var newFolderNote: String {
        let name = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { return "" }

        let folder = ClaudeInstall.newConfigFolder(
            .macOS, environmentName: name, userProfile: Store.home,
            folderExists: { FileManager.default.fileExists(atPath: $0) })
        return Strings.format(Strings.folderIs, (folder as NSString).lastPathComponent)
    }

    func createSecond() {
        let name = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { return }

        let folder = ClaudeInstall.newConfigFolder(
            .macOS, environmentName: name, userProfile: Store.home,
            folderExists: { FileManager.default.fileExists(atPath: $0) })

        do {
            try FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        } catch {
            Log.write("checklist: could not create \((folder as NSString).lastPathComponent)")
            return
        }

        choose(second: folder, named: name)
        guard ClaudeLauncher.open(configFolder: folder, workingDirectory: Store.home) else { return }

        secondWaiting = true
        wait(Waiter.forSignIn(folder) { [weak self] in
            self?.secondWaiting = false
            self?.moveOn()
        })
    }

    func skipSecond() {
        secondSkipped = true
        secondFolder = nil
        moveOn()
    }

    func suggestedName(for folder: ClaudeFolder) -> String {
        EnvironmentScan.suggestName(folder.folderName, Strings.environmentPlainName)
    }

    // ---- project folders ----

    func addProject() {
        guard let picked = FolderPicker.pick(title: Strings.pickProject),
              !projectFolders.contains(where: { RealClaude.sameFolder($0, picked) })
        else {
            return
        }

        projectFolders.append(picked)
    }

    var explicitWins: String {
        let commands = commandsInUse
        guard commands.count == 2, !secondNameText.isEmpty else { return "" }
        return Strings.format(Strings.explicitWins, commands[0], firstNameText)
    }

    // ---- access ----

    var accessLine: String {
        guard let bridge = BridgePath.current() else { return Strings.wizardBridgeNotFound }
        return ClaudeSettingsFile.lineFor(bridge)
    }

    var filesToChange: [String] { configFolders.map(ClaudeSettingsFile.pathIn) }

    func grantAccess() {
        guard BridgePath.current() != nil else {
            accessResult = Strings.accessNoBridge
            return
        }

        accessGranted = true
        moveOn()
    }

    // ---- commands ----

    /// One field per environment. The command follows the name until the person types their own,
    /// and "Use the name" hands it back to the name (D-166).
    func fillCommandFields() {
        let names = [firstNameText, secondNameText]
        for index in 0..<2 {
            let wanted = customCommand[index] ?? LaunchCommand.fromEnvironmentName(names[index])
            if commandText[index] != wanted {
                commandText[index] = wanted
            }
        }
    }

    func commandTyped(_ index: Int) {
        let names = [firstNameText, secondNameText]
        let byName = LaunchCommand.fromEnvironmentName(names[index])
        customCommand[index] = commandText[index] == byName ? nil : commandText[index]
    }

    func followsTheName(_ index: Int) -> Bool { customCommand[index] == nil }

    func useTheName(_ index: Int) {
        customCommand[index] = nil
        fillCommandFields()
    }

    func problem(_ index: Int) -> CommandProblem {
        let others = (0..<commandCount).filter { $0 != index }.map { commandText[$0] }
        return LaunchCommand.check(commandText[index], others)
    }

    var commandsAreGood: Bool {
        (0..<commandCount).allSatisfy { problem($0) == .none }
    }

    // ---- finish ----

    func finish() {
        guard canFinish else { return }

        let settings = buildSettings()
        Store.saveEnvironments(settings)

        let problems = accessGranted == true ? EnvironmentSetup.applyAccess(settings) : []
        let commands = commandsWanted == true ? EnvironmentSetup.applyCommands(settings) : nil
        applyPlace()

        // Installs the persona plugin when it was turned on here. With the persona off everywhere
        // it only reads a few files.
        PluginSync.request("checklist finished")

        Log.write("checklist finished: \(settings.environments.count) environments, "
            + "access=\(describe(accessGranted)), commands=\(describe(commandsWanted)), "
            + "bound=\(projectFolders.count), persona=\(describe(personaWanted)), "
            + "stats=\(describe(statsWanted))")

        close()
        onFinished?(doneNote(problems, commands))
    }

    /// One short paragraph for the environment page that opens after Finish.
    private func doneNote(_ problems: [PatchProblem], _ commands: String?) -> String {
        var parts = [Strings.setupDone]
        if let commands {
            parts.append(Strings.format(Strings.doneCommands, commands))
        }

        if problems.isEmpty {
            parts.append(accessGranted == true
                ? Strings.wizardDoneFirstNumbers
                : Strings.wizardDoneNoAccess)
        } else {
            var seen: [PatchProblem] = []
            for problem in problems where !seen.contains(problem) { seen.append(problem) }
            parts.append(seen.map(ClaudeSettingsFile.words).joined(separator: " "))
        }

        return parts.joined(separator: " ")
    }

    private func buildSettings() -> EnvironmentSettings {
        func keep(_ name: String, _ folder: String, _ index: Int, _ projects: [String]) -> AikoEnvironment {
            let before = existing.environments.first { $0.holds(folder) }
            return AikoEnvironment(
                name,
                [folder],
                directMode: before?.directMode ?? false,
                // "Meet Aiko" turns it on in environment 1. "Later" never turns off what is on.
                persona: (before?.persona ?? false) || (index == 0 && personaWanted == true),
                customCommand: customCommand[index],
                projectFolders: projects)
        }

        // The checklist only edits the folders of environment 2. Those of environment 1 stay as
        // they were: a live run lost the personal projects binding here.
        let firstFolders = existing.first(.macOS, Store.home)?.projectFolders ?? []
        var environments = [keep(firstNameText, Self.firstFolder, 0, firstFolders)]
        if let secondFolder {
            environments.append(keep(secondNameText, secondFolder, 1, projectFolders))
        }

        // A default that was written down stays written down, under its current name.
        let defaultName = defaultIndex > 0 && environments.count > 1
            ? environments[1].name
            : (existing.defaultEnvironment == nil ? nil : environments[0].name)

        // The ring keeps the environment the person put there, when it is still in the list.
        let ring = environments.contains { $0.name == existing.ringEnvironment }
            ? existing.ringEnvironment
            : environments[0].name

        return EnvironmentSettings(environments, ringEnvironment: ring, defaultEnvironment: defaultName)
    }

    private func applyPlace() {
        LoginItem.set(runAtStartup)

        var app = Store.settings()
        app.place = island ? .island : .tray
        app.runAtStartup = runAtStartup
        app.checkUpdates = checkUpdates
        app.sendStats = statsWanted == true
        // The question was put, whichever way it was answered. Leaving it unanswered is not
        // possible: the checklist does not finish until the item has a state.
        app.privacyAsked = statsWanted != nil
        app.meetAikoShown = true
        Store.saveSettings(app)
    }

    private func signedIn(_ folder: String) -> Bool {
        Store.isSignedIn(folder)
    }

    private func describe(_ answer: Bool?) -> String {
        answer.map { $0 ? "true" : "false" } ?? "none"
    }
}
