import Foundation

/// The pure part of AikoMarketplace: the name of Aiko's marketplace and the plugin ids under it.
/// The rest of that file is about Windows paths and is not ported yet.
public enum AikoMarketplaceIds {
    public static let name = "aiko"

    public static func pluginId(_ plugin: String) -> String { "\(plugin)@\(name)" }

    public static func isOurs(_ pluginId: String) -> Bool { pluginId.hasSuffix("@" + name) }
}

/// Adding and removing our one line in the Claude Code settings file. Text in, text out:
/// reading and writing the file, and its backup, belong to the app.
///
/// Two rules the spike taught us. First, find our key by parsing the JSON, never by matching
/// text: a search string assembled in the wrong order silently matches nothing. Second, if the
/// user already has a status line, keep it — the bridge calls it and shows its output.
///
/// Every method answers "nothing to change" with nil instead of the bool and the out parameter
/// the Windows core uses.
public struct SettingsJsonPatch: Sendable {
    /// Whether a status line or hook command runs our own bridge. BridgeCommand is not ported yet,
    /// and the answer will differ on macOS anyway, so it comes in from outside.
    public let isAiko: @Sendable (String?) -> Bool

    public init(isAiko: @escaping @Sendable (String?) -> Bool) {
        self.isAiko = isAiko
    }

    public static let statusLineKey = "statusLine"
    public static let wrappedKey = "aikoWrappedStatusLine"
    public static let enabledPluginsKey = "enabledPlugins"
    public static let extraMarketplacesKey = "extraKnownMarketplaces"
    public static let hooksKey = "hooks"
    public static let sessionStartKey = "SessionStart"

    public func addBridge(_ settingsJson: String, _ bridgeCommand: String) -> String? {
        guard !bridgeCommand.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              var root = parseObject(settingsJson) else {
            return nil
        }

        let ours = JsonNode.object(JsonObject([
            ("type", .string("command")),
            ("command", .string(bridgeCommand)),
        ]))

        // A line kept aside that turns out to be ours is rubbish an older Aiko left behind when it
        // failed to recognise itself after a reinstall. Putting it back on removal would hand the
        // user a status line running a bridge that is gone.
        var cleaned = false
        if let alreadyAside = root[SettingsJsonPatch.wrappedKey], isAiko(commandIn(alreadyAside)) {
            root.remove(SettingsJsonPatch.wrappedKey)
            cleaned = true
        }

        if let existing = root[SettingsJsonPatch.statusLineKey], existing != .null {
            let existingCommand = commandIn(existing)
            if isAiko(existingCommand) {
                // Ours already. Same text means there is nothing to do; different text means the
                // install path or the shell changed, and it is replaced rather than kept aside.
                if existingCommand == bridgeCommand && !cleaned {
                    return nil
                }
            } else {
                // Somebody else's status line is kept aside, so removing Aiko can put it back.
                root[SettingsJsonPatch.wrappedKey] = existing
            }
        }

        root[SettingsJsonPatch.statusLineKey] = ours
        return write(root)
    }

    /// Adds the bridge as a SessionStart hook, beside any hooks the person has.
    ///
    /// Hooks are a list of groups, each with its own list of commands. Ours goes in a group of its
    /// own, so a group the person wrote is never edited. A hook of ours with an old path is
    /// replaced, the same way the status line is.
    public func addSessionHook(_ settingsJson: String, _ hookCommand: String) -> String? {
        guard !hookCommand.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              var root = parseObject(settingsJson) else {
            return nil
        }

        if let hooksNode = root[SettingsJsonPatch.hooksKey], hooksNode != .null, hooksNode.objectValue == nil {
            // Something that is not an object. Claude Code would not read it either; not ours to fix.
            return nil
        }

        var hooks = root[SettingsJsonPatch.hooksKey]?.objectValue
        if let existing = hooks?[SettingsJsonPatch.sessionStartKey], existing != .null, existing.arrayValue == nil {
            return nil
        }

        let ours = ourHookCommands(hooks?[SettingsJsonPatch.sessionStartKey]?.arrayValue ?? [])
        if ours.count == 1 && ours[0] == hookCommand {
            return nil
        }

        if hooks == nil {
            hooks = JsonObject()
        }

        var groups = hooks?[SettingsJsonPatch.sessionStartKey]?.arrayValue ?? []
        removeOurHooks(&groups)
        groups.append(.object(JsonObject([
            ("hooks", .array([
                .object(JsonObject([("type", .string("command")), ("command", .string(hookCommand))]))
            ]))
        ])))

        hooks?[SettingsJsonPatch.sessionStartKey] = .array(groups)
        root[SettingsJsonPatch.hooksKey] = .object(hooks!)
        return write(root)
    }

    public func hasOurSessionHook(_ settingsJson: String) -> Bool {
        guard let root = parseObject(settingsJson), let groups = sessionStartGroups(root) else {
            return false
        }
        return !ourHookCommands(groups).isEmpty
    }

    public func removeBridge(_ settingsJson: String) -> String? {
        guard var root = parseObject(settingsJson) else {
            return nil
        }

        // The hook goes whenever Aiko leaves a file, with or without a status line of ours in it.
        let hookRemoved = removeSessionHook(&root)

        if !root.contains(SettingsJsonPatch.statusLineKey) && !root.contains(SettingsJsonPatch.wrappedKey) {
            return hookRemoved ? write(root) : nil
        }

        // A line kept aside that is ours was never the user's: it is left over from an older Aiko
        // that did not recognise itself. It is dropped, not restored.
        if let wrapped = root[SettingsJsonPatch.wrappedKey], wrapped != .null, !isAiko(commandIn(wrapped)) {
            root[SettingsJsonPatch.statusLineKey] = wrapped
            root.remove(SettingsJsonPatch.wrappedKey)
        } else {
            root.remove(SettingsJsonPatch.wrappedKey)
            root.remove(SettingsJsonPatch.statusLineKey)
        }

        return write(root)
    }

    /// Takes Aiko's plugins and its marketplace out of the file (D-205). Without these keys Claude
    /// Code loads none of them, even while its own copies of the plugins are still on disk. Other
    /// plugins and marketplaces stay as they are.
    public func removePlugins(_ settingsJson: String) -> String? {
        guard var root = parseObject(settingsJson) else {
            return nil
        }

        var changed = false
        if var enabled = root[SettingsJsonPatch.enabledPluginsKey]?.objectValue {
            for id in enabled.keys where AikoMarketplaceIds.isOurs(id) {
                changed = enabled.remove(id) || changed
            }
            root[SettingsJsonPatch.enabledPluginsKey] = .object(enabled)
        }

        if var marketplaces = root[SettingsJsonPatch.extraMarketplacesKey]?.objectValue {
            changed = marketplaces.remove(AikoMarketplaceIds.name) || changed
            root[SettingsJsonPatch.extraMarketplacesKey] = .object(marketplaces)
        }

        if !changed {
            return nil
        }

        dropIfEmpty(&root, SettingsJsonPatch.enabledPluginsKey)
        dropIfEmpty(&root, SettingsJsonPatch.extraMarketplacesKey)
        return write(root)
    }

    /// "claude plugin uninstall" writes an empty enabledPlugins back after Aiko took its own keys
    /// out. Called only for a folder where it just ran.
    public func dropEmptyPluginKeys(_ settingsJson: String) -> String? {
        guard var root = parseObject(settingsJson) else {
            return nil
        }

        let enabled = dropIfEmpty(&root, SettingsJsonPatch.enabledPluginsKey)
        let marketplaces = dropIfEmpty(&root, SettingsJsonPatch.extraMarketplacesKey)
        if !enabled && !marketplaces {
            return nil
        }

        return write(root)
    }

    /// Anything of Aiko's in the file. The backup lives while this is true.
    public func hasAnyAikoEntries(_ settingsJson: String) -> Bool {
        guard let root = parseObject(settingsJson) else {
            return false
        }

        if isAiko(commandIn(root[SettingsJsonPatch.statusLineKey])) { return true }
        if root.contains(SettingsJsonPatch.wrappedKey) { return true }
        if let groups = sessionStartGroups(root), !ourHookCommands(groups).isEmpty { return true }
        if let enabled = root[SettingsJsonPatch.enabledPluginsKey]?.objectValue,
           enabled.keys.contains(where: AikoMarketplaceIds.isOurs) {
            return true
        }
        if let marketplaces = root[SettingsJsonPatch.extraMarketplacesKey]?.objectValue,
           marketplaces.contains(AikoMarketplaceIds.name) {
            return true
        }
        return false
    }

    /// Whether the status line in this file is ours, whatever path or shell it names.
    public func hasOurLine(_ settingsJson: String) -> Bool {
        guard let root = parseObject(settingsJson), let line = root[SettingsJsonPatch.statusLineKey] else {
            return false
        }
        return isAiko(commandIn(line))
    }

    /// The output style the person picked for themselves, if any. While the persona is on, its style
    /// wins over this one, and the settings page has to say so (D-207). Aiko never changes the key.
    public static func userOutputStyle(_ settingsJson: String) -> String? {
        guard let root = JsonNode.parse(settingsJson)?.objectValue,
              let name = root["outputStyle"]?.stringValue,
              !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              name.lowercased() != "default",
              !isPersonaStyle(name) else {
            return nil
        }
        return name
    }

    private static func isPersonaStyle(_ name: String) -> Bool {
        name.lowercased() == PersonaPrompt.styleName.lowercased()
            || name.lowercased() == "\(PersonaPlugin.name):\(PersonaPrompt.styleName)".lowercased()
    }

    /// What the bridge should call after doing its own work, if anything.
    public static func readWrappedCommand(_ settingsJson: String) -> String? {
        JsonNode.parse(settingsJson)?.objectValue?[wrappedKey]?.objectValue?["command"]?.stringValue
    }

    @discardableResult
    private func dropIfEmpty(_ root: inout JsonObject, _ key: String) -> Bool {
        guard let object = root[key]?.objectValue, object.isEmpty else {
            return false
        }
        return root.remove(key)
    }

    private func sessionStartGroups(_ root: JsonObject) -> [JsonNode]? {
        root[SettingsJsonPatch.hooksKey]?.objectValue?[SettingsJsonPatch.sessionStartKey]?.arrayValue
    }

    private func ourHookCommands(_ groups: [JsonNode]) -> [String] {
        groups
            .compactMap(\.objectValue)
            .flatMap { $0[SettingsJsonPatch.hooksKey]?.arrayValue ?? [] }
            .compactMap { commandIn($0) }
            .filter { isAiko($0) }
    }

    /// Takes our commands out of every group, then drops the groups, the SessionStart list and the
    /// hooks object that are left empty because of it. A group the person wrote keeps everything else.
    private func removeSessionHook(_ root: inout JsonObject) -> Bool {
        guard var groups = sessionStartGroups(root), !ourHookCommands(groups).isEmpty else {
            return false
        }

        removeOurHooks(&groups)

        var hooks = root[SettingsJsonPatch.hooksKey]!.objectValue!
        if groups.isEmpty {
            hooks.remove(SettingsJsonPatch.sessionStartKey)
        } else {
            hooks[SettingsJsonPatch.sessionStartKey] = .array(groups)
        }

        if hooks.isEmpty {
            root.remove(SettingsJsonPatch.hooksKey)
        } else {
            root[SettingsJsonPatch.hooksKey] = .object(hooks)
        }

        return true
    }

    private func removeOurHooks(_ groups: inout [JsonNode]) {
        var kept: [JsonNode] = []
        for group in groups {
            guard var object = group.objectValue, let commands = object[SettingsJsonPatch.hooksKey]?.arrayValue else {
                kept.append(group)
                continue
            }

            let had = commands.count
            let left = commands.filter { !isAiko(commandIn($0)) }
            if left.isEmpty && had > 0 {
                continue
            }

            object[SettingsJsonPatch.hooksKey] = .array(left)
            kept.append(.object(object))
        }
        groups = kept
    }

    /// The command a status line object runs, or nil when the node is not such an object.
    private func commandIn(_ statusLine: JsonNode?) -> String? {
        statusLine?.objectValue?["command"]?.stringValue
    }

    private func parseObject(_ json: String) -> JsonObject? {
        guard !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return JsonNode.parse(json)?.objectValue
    }

    private func write(_ root: JsonObject) -> String {
        JsonNode.object(root).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }
}
