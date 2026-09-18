import AikoKit
import Foundation

/// Runs the real claude command the way a person would from a fresh terminal (D-203).
///
/// Never the claude on PATH: that is Aiko's shim, and in a bound folder it overrides the config
/// folder we ask for. Never with the variables of a Claude Code session either: inside a session
/// Claude Code ignores -y and refuses to run a command source.
///
/// The twin of Plugins/ClaudeCli.cs.
struct ClaudeCliProcess: ClaudeCli {
    let claude: String

    func run(configDirectory: String, arguments: [String], timeout: TimeInterval) -> CliResult {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: claude)
        process.arguments = arguments
        process.currentDirectoryURL = URL(fileURLWithPath: NSTemporaryDirectory())

        var environment = ProcessInfo.processInfo.environment
        for name in environment.keys
        where name == "CLAUDECODE" || name == ClaudeConfigFolder.variableName
            || name.hasPrefix("CLAUDE_CODE_") || name.hasPrefix("AIKO_") {
            environment.removeValue(forKey: name)
        }

        // Environment 1 runs without the variable, like a plain claude and the IDE panel (D-156).
        if !ClaudeConfigFolder.isDefault(.macOS, configDirectory, NSHomeDirectory()) {
            environment[ClaudeConfigFolder.variableName] = configDirectory
        }

        process.environment = environment
        // Both streams go nowhere rather than into a pipe nobody drains: a chatty command that
        // fills a pipe never exits.
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        process.standardInput = FileHandle.nullDevice

        do {
            try process.run()
        } catch {
            return CliResult(exitCode: -1, timedOut: false)
        }

        let deadline = Date().addingTimeInterval(timeout)
        while process.isRunning && Date() < deadline {
            Thread.sleep(forTimeInterval: 0.05)
        }

        if process.isRunning {
            process.terminate()
            return CliResult(exitCode: -1, timedOut: true)
        }

        return CliResult(exitCode: Int(process.terminationStatus), timedOut: false)
    }
}

/// The skills plugin that ships with this copy of Aiko, and its copy in the local marketplace
/// (D-234). Claude Code installs from the copy; the copy is renewed only when a file changed.
///
/// The twin of Plugins/SkillShelf.cs.
enum SkillShelf {
    private static let source: String = {
        let resources = Bundle.main.resourceURL?.path
            ?? (Bundle.main.executablePath.map { ($0 as NSString).deletingLastPathComponent } ?? ".")
        return (resources as NSString)
            .appendingPathComponent(SkillPlugin.folderName)
            .appending("/")
            .appending(SkillCatalog.pluginName)
    }()

    private nonisolated(unsafe) static var found: (plugin: SkillPlugin?, skills: [String])?

    static var shipped: SkillPlugin? { findShipped().plugin }

    static var skills: [String] { findShipped().skills }

    /// Copies the plugin into the marketplace folder when its files differ from the copy there, and
    /// removes every other plugin folder, such as the one-skill plugins of older versions.
    static func copyTo(_ marketplaceFolder: String) -> Bool {
        let target = (marketplaceFolder as NSString).appendingPathComponent(SkillPlugin.folderName)
        var changed = false

        if let plugin = shipped {
            changed = copy(to: (target as NSString).appendingPathComponent(plugin.name))
        }

        let inside = (try? FileManager.default.contentsOfDirectory(atPath: target)) ?? []
        for old in inside where old != shipped?.name {
            try? FileManager.default.removeItem(
                atPath: (target as NSString).appendingPathComponent(old))
        }

        return changed
    }

    private static func copy(to target: String) -> Bool {
        let manager = FileManager.default
        let names = (manager.enumerator(atPath: source)?.allObjects as? [String]) ?? []
        let files = names
            .map { (relative: $0, path: (source as NSString).appendingPathComponent($0)) }
            .filter { !isDirectory($0.path) }
            .sorted { $0.relative < $1.relative }

        let contents = files.compactMap { file -> (path: String, content: Data)? in
            guard let data = manager.contents(atPath: file.path) else { return nil }
            return (file.relative, data)
        }

        guard let manifestText = try? String(contentsOfFile: manifestIn(source), encoding: .utf8),
              let manifest = SkillPlugin.versionedManifest(
                  manifestText, SkillPlugin.contentHash(contents))
        else {
            return false
        }

        if let copied = try? String(contentsOfFile: manifestIn(target), encoding: .utf8),
           SkillPlugin.versionOf(copied) == SkillPlugin.versionOf(manifest) {
            return false
        }

        // A new folder moved into place in one step, so Claude Code never copies half a plugin.
        let temporary = target + ".\(ProcessInfo.processInfo.processIdentifier).tmp"
        try? manager.removeItem(atPath: temporary)

        do {
            for file in files {
                let destination = (temporary as NSString).appendingPathComponent(file.relative)
                try manager.createDirectory(
                    atPath: (destination as NSString).deletingLastPathComponent,
                    withIntermediateDirectories: true)
                try manager.copyItem(atPath: file.path, toPath: destination)
            }

            try manifest.write(toFile: manifestIn(temporary), atomically: true, encoding: .utf8)
            try? manager.removeItem(atPath: target)
            try manager.createDirectory(
                atPath: (target as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
            try manager.moveItem(atPath: temporary, toPath: target)
            return true
        } catch {
            Log.write("plugins: could not copy the skills plugin (\(error.localizedDescription))")
            try? manager.removeItem(atPath: temporary)
            return false
        }
    }

    private static func findShipped() -> (plugin: SkillPlugin?, skills: [String]) {
        if let found { return found }

        let manifest = try? String(contentsOfFile: manifestIn(source), encoding: .utf8)
        guard let plugin = SkillPlugin.fromManifest(manifest) else {
            found = (nil, [])
            return found!
        }

        let skillsFolder = (source as NSString).appendingPathComponent(SkillPlugin.skillsFolder)
        let skills = ((try? FileManager.default.contentsOfDirectory(atPath: skillsFolder)) ?? [])
            .filter {
                FileManager.default.fileExists(
                    atPath: (skillsFolder as NSString).appendingPathComponent($0) + "/SKILL.md")
            }
            .sorted()

        found = (plugin, skills)
        return found!
    }

    private static func manifestIn(_ pluginFolder: String) -> String {
        (pluginFolder as NSString).appendingPathComponent(".claude-plugin/plugin.json")
    }

    private static func isDirectory(_ path: String) -> Bool {
        var directory: ObjCBool = false
        return FileManager.default.fileExists(atPath: path, isDirectory: &directory) && directory.boolValue
    }
}

/// Brings every Claude Code folder in line with the persona switches (D-201, D-234).
///
/// What a folder should have is decided in AikoKit.PluginPlan; this reads the files, writes the
/// marketplace and runs claude. All of it happens on one background queue, one folder after
/// another: two claude processes writing the same settings.json would lose a change.
///
/// The twin of Plugins/PluginSync.cs.
enum PluginSync {
    private static let queue = DispatchQueue(label: "org.slayumind.aiko.plugins")

    /// Checks every folder and changes only what differs. Reading three small files per folder is
    /// all it costs when nothing is to be done.
    static func request(_ why: String) {
        enqueue { reconcile(why, updatePersona: false) }
    }

    /// The persona text changed, for example the temperament: installed persona plugins take the
    /// new text for the next session.
    static func personaChanged() {
        enqueue { reconcile("persona changed", updatePersona: true) }
    }

    /// For an environment that is going: its keys already left settings.json, and this takes out
    /// what Claude Code keeps in its own plugin files.
    static func remove(_ folders: [String], _ why: String) {
        enqueue { removeFrom(folders, why) }
    }

    private static func enqueue(_ work: @escaping @Sendable () -> Void) {
        queue.async(execute: work)
    }

    private static func reconcile(_ why: String, updatePersona: Bool) {
        let environments = EnvironmentSettings.fromJson(read(Store.folders.environmentsFile))
        let persona = PersonaSettings.fromJson(read(Store.folders.personaFile) ?? "")
        let skills = SkillShelf.skills

        let folders = environments.environments
            .flatMap { environment in
                environment.configDirectories.map { folder in
                    (folder: folder,
                     desired: PluginPlan.desired(environment, persona: persona, skills: skills),
                     state: state(of: folder))
                }
            }

        // With the persona off everywhere and nothing of ours installed, Aiko writes nothing at all.
        if folders.allSatisfy({
            $0.desired.isEmpty && $0.state.installed.isEmpty && $0.state.enabled.isEmpty
        }) {
            return
        }

        guard let written = writeMarketplace() else { return }

        let plans = folders
            .map { (folder: $0.folder,
                    steps: stepsFor($0.desired, $0.state,
                                    updatePersona: updatePersona || written.marketplaceChanged,
                                    skillsChanged: written.skillsChanged)) }
            .filter { !$0.steps.isEmpty }

        guard !plans.isEmpty else { return }
        guard let claude = ClaudeLauncher.findClaude() else {
            Log.write("plugins: claude not found, nothing changed")
            return
        }

        let cli = ClaudeCliProcess(claude: claude)
        for plan in plans {
            tell(why, plan.folder,
                 PluginReconciler.apply(cli: cli, configDirectory: plan.folder, steps: plan.steps))
        }
    }

    private static func removeFrom(_ folders: [String], _ why: String) {
        let withPlugins = folders
            .filter { FileManager.default.fileExists(atPath: $0) }
            .map { (folder: $0, steps: PluginPlan.removal(state(of: $0))) }
            .filter { !$0.steps.isEmpty }

        guard !withPlugins.isEmpty else { return }
        guard let claude = ClaudeLauncher.findClaude() else {
            Log.write("plugins: \(why): claude not found, the plugins stay turned off")
            return
        }

        let cli = ClaudeCliProcess(claude: claude)
        for plan in withPlugins {
            let results = PluginReconciler.apply(
                cli: cli, configDirectory: plan.folder, steps: plan.steps)

            // The command writes an empty enabledPlugins back into the file Aiko just cleaned.
            _ = ClaudeSettingsFile.tidyAfterPluginRemoval(plan.folder)
            tell(why, plan.folder, results)
        }
    }

    private static func stepsFor(
        _ desired: Set<String>, _ state: PluginState, updatePersona: Bool, skillsChanged: Bool
    ) -> [PluginStep] {
        var steps = PluginPlan.steps(
            desired: desired, state: state, marketplaceFolder: Store.folders.marketplaceFolder)

        let persona = AikoMarketplace.pluginId(PersonaPlugin.name)
        if updatePersona && desired.contains(persona) && state.installed.contains(persona) {
            steps.append(PluginStep(.update, persona))
        }

        // The skills plugin got other files with a new Aiko: installed copies take the new version.
        let skills = AikoMarketplace.pluginId(SkillCatalog.pluginName)
        if skillsChanged && desired.contains(skills) && state.installed.contains(skills) {
            steps.append(PluginStep(.update, skills))
        }

        return steps
    }

    private static func state(of folder: String) -> PluginState {
        PluginState.read(
            settingsJson: read((folder as NSString).appendingPathComponent("settings.json")),
            installedJson: read((folder as NSString).appendingPathComponent("plugins/installed_plugins.json")),
            knownMarketplacesJson: read((folder as NSString).appendingPathComponent("plugins/known_marketplaces.json")))
    }

    /// Written only when the text differs, so the marketplace folder is not touched for nothing.
    private static func writeMarketplace() -> (marketplaceChanged: Bool, skillsChanged: Bool)? {
        guard let bridge = BridgePath.current(),
              let command = AikoMarketplace.personaCommand(.macOS, bridge, Store.folders)
        else {
            Log.write("plugins: no command Claude Code would accept for the bridge, nothing changed")
            return nil
        }

        let skillsChanged = SkillShelf.copyTo(Store.folders.marketplaceFolder)
        let path = Store.folders.marketplaceFile
        let json = AikoMarketplace.json(command, skills: SkillShelf.shipped)
        let before = read(path)

        guard before != json else {
            return (false, skillsChanged)
        }

        do {
            try FileManager.default.createDirectory(
                atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
            try json.write(toFile: path, atomically: true, encoding: .utf8)
        } catch {
            Log.write("plugins: marketplace could not be written (\(error.localizedDescription))")
            return nil
        }

        return (before != nil, skillsChanged)
    }

    private static func tell(
        _ why: String, _ folder: String, _ results: [(step: PluginStep, result: CliResult)]
    ) {
        let failed = results.filter { !$0.result.succeeded }
        let names = failed
            .map { "\($0.step.kind) exit \($0.result.exitCode)\($0.result.timedOut ? " timeout" : "")" }
            .joined(separator: ", ")

        Log.write("plugins: \(why): \((folder as NSString).lastPathComponent): "
            + "\(results.count) steps, \(failed.count) failed"
            + (failed.isEmpty ? "" : " (\(names))"))
    }

    private static func read(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }
}
