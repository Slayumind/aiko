import AikoKit

/// What Aiko puts right once at every start. None of it draws anything: it repairs what an update
/// or a move of the app left stale. The twin of StartupChores.cs, which also repairs stale status
/// lines and clears old activity files; the Mac shell has never done either.
@MainActor
enum StartupChores {
    /// What an update of Aiko brings with it and Claude Code does not pick up by itself.
    static func refreshPluginsAndCommands(_ environments: EnvironmentSettings) {
        // An update can bring a new persona text, and on macOS the app itself moves: the marketplace
        // names the bridge by its full path, so a copy dragged from the disk image into Applications
        // leaves Claude Code with a command that points at nothing. When the persona is off
        // everywhere this reads a few files and does nothing else.
        PluginSync.request("startup")

        // An update brings a new shim. The copies in PATH are refreshed, and only when the person
        // set commands up: the folder never appears on its own.
        if CommandFolder.isSetUp {
            CommandFolder.sync(environments)
        }
    }
}
