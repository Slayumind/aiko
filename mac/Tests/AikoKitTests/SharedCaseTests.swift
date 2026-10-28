import Foundation
import Testing

@testable import AikoKit

/// The case files under spec/cases, which the xUnit tests of Aiko.Core read too.
///
/// A rule that is a table of inputs and answers lives there instead of twice in two languages, so
/// it cannot change in one core and stay as it was in the other.
enum SpecCases {
    static func folder(_ name: String) -> URL {
        repository().appendingPathComponent("spec").appendingPathComponent("cases").appendingPathComponent(name)
    }

    static func bytes(_ name: String, _ file: String) -> Data {
        (try? Data(contentsOf: folder(name).appendingPathComponent(file))) ?? Data()
    }

    static func text(_ name: String, _ file: String) -> String {
        (try? String(contentsOf: folder(name).appendingPathComponent(file), encoding: .utf8)) ?? ""
    }

    static func json(_ name: String, _ file: String = "cases.json") -> JsonObject {
        let path = folder(name).appendingPathComponent(file).path
        let text = (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
        return JsonNode.parse(text)?.objectValue ?? JsonObject()
    }

    /// The tests run from .build/, so the repository is the first folder above with the solution in it.
    static func repository() -> URL {
        var folder = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while folder.path != "/"
            && !FileManager.default.fileExists(atPath: folder.appendingPathComponent("Aiko.slnx").path) {
            folder = folder.deletingLastPathComponent()
        }
        return folder
    }
}

/// The rules that live in spec/cases, run against the Swift core. The Windows core runs the same
/// files, so a row added there is a row both cores have to answer.
struct SharedCaseTests {
    // ---- the file one environment reports into ----

    @Test
    func everySnapshotNameCaseAnswersTheSame() {
        let cases = SpecCases.json("snapshot-name")

        #expect(cases["default"]?.stringValue == SnapshotName.default)

        for row in cases["for"]?.arrayValue ?? [] {
            let directory = row["configDirectory"]?.stringValue
            #expect(SnapshotName.forConfigDirectory(directory) == row["name"]?.stringValue)
        }

        for row in cases["clean"]?.arrayValue ?? [] {
            #expect(SnapshotName.clean(row["folderName"]?.stringValue ?? "") == row["name"]?.stringValue)
        }
    }

    // ---- where Aiko keeps its files ----

    @Test
    func everyFolderOfBothLayoutsIsWhereTheCasesSay() {
        let cases = SpecCases.json("folder-layout")

        #expect(cases["appFolderName"]?.stringValue == AikoFolders.appFolderName)
        #expect(cases["installFolderName"]?.stringValue == AikoFolders.installFolderName)
        let snapshotName = cases["snapshotName"]?.stringValue ?? ""

        for platform in cases["platforms"]?.arrayValue ?? [] {
            let settingsBase = platform["settingsBase"]?.stringValue ?? ""
            let localBase = platform["localBase"]?.stringValue ?? ""
            let folders = platform["platform"]?.stringValue == "macos"
                ? AikoFolders.macOS(settingsBase, localBase)
                : AikoFolders.windows(settingsBase, localBase)

            let built = [
                "settingsFolder": folders.settingsFolder,
                "settingsFile": folders.settingsFile,
                "environmentsFile": folders.environmentsFile,
                "personaFile": folders.personaFile,
                "memoryFile": folders.memoryFile,
                "installIdFile": folders.installIdFile,
                "reportedPeriodsFile": folders.reportedPeriodsFile,
                "terminalFile": folders.terminalFile,
                "localFolder": folders.localFolder,
                "snapshotsFolder": folders.snapshotsFolder,
                "snapshotFile": folders.snapshotFile(snapshotName),
                "activityFolder": folders.activityFolder,
                "terminalEventsFolder": folders.terminalEventsFolder,
                "terminalHooksFile": folders.terminalHooksFile,
                "directCacheFolder": folders.directCacheFolder,
                "commandsFolder": folders.commandsFolder,
                "marketplaceFolder": folders.marketplaceFolder,
                "marketplaceFile": folders.marketplaceFile,
                "personaPluginsFolder": folders.personaPluginsFolder,
                "logFile": folders.logFile,
                "voiceModelFile": folders.voiceModelFile,
                "installedAppFolder": folders.installedAppFolder,
            ]

            for member in platform["paths"]?.objectValue?.members ?? [] {
                #expect(built[member.key] == member.value.stringValue, "\(member.key)")
            }
        }
    }

    // ---- the command that starts one environment ----

    @Test
    func everyLaunchCommandCaseAnswersTheSame() {
        let cases = SpecCases.json("launch-command")

        #expect(cases["prefix"]?.stringValue == LaunchCommand.prefix)
        #expect(cases["maxLength"]?.int64Value == Int64(LaunchCommand.maxLength))

        for row in cases["fromEnvironmentName"]?.arrayValue ?? [] {
            #expect(
                LaunchCommand.fromEnvironmentName(row["name"]?.stringValue ?? "")
                    == row["command"]?.stringValue)
        }

        for row in cases["slug"]?.arrayValue ?? [] {
            #expect(LaunchCommand.slug(row["name"]?.stringValue ?? "") == row["slug"]?.stringValue)
        }

        for row in cases["check"]?.arrayValue ?? [] {
            let taken = (row["takenByOthers"]?.arrayValue ?? []).compactMap(\.stringValue)
            #expect(
                LaunchCommand.check(row["command"]?.stringValue ?? "", taken)
                    == Self.problem(row["problem"]?.stringValue))
        }
    }

    // ---- which environment a start of Claude Code belongs to ----

    @Test
    func everyShimCaseDecidesTheSame() {
        let cases = SpecCases.json("shim-launch")
        let userProfile = cases["userProfile"]?.stringValue ?? ""
        let platform: PlatformConventions = cases["platform"]?.stringValue == "macos" ? .macOS : .windows

        for group in cases["groups"]?.arrayValue ?? [] {
            let settings = EnvironmentSettings(
                (group["environments"]?.arrayValue ?? []).map(Self.environmentIn))

            for row in group["cases"]?.arrayValue ?? [] {
                let plan = ShimLaunch.decide(
                    platform,
                    invokedAs: row["invokedAs"]?.stringValue ?? "",
                    workingDirectory: row["workingDirectory"]?.stringValue ?? "",
                    settings: settings,
                    userProfile: userProfile)

                let where_ = "\(group["name"]?.stringValue ?? ""): \(row["invokedAs"]?.stringValue ?? "")"
                #expect(plan.environment?.name == row["environment"]?.stringValue, "\(where_)")
                #expect(plan.variable == Self.variable(row["variable"]?.stringValue), "\(where_)")
                #expect(plan.configDirectory == row["configDirectory"]?.stringValue, "\(where_)")
                #expect(plan.isExplicit == (row["explicit"]?.boolValue ?? false), "\(where_)")
            }
        }
    }

    // ---- what a hook says a session is doing ----

    @Test
    func everyHookEventMeansTheSameActivity() {
        let cases = SpecCases.json("hook-events")
        let session = cases["session"]?.stringValue ?? ""

        for row in cases["events"]?.arrayValue ?? [] {
            let json = row["json"]?.stringValue ?? ""
            let activity = row["activity"]?.stringValue
            let expected = activity.map { HookEvent(sessionId: session, activity: Self.activity($0)) }

            #expect(HookEvent.fromJson(json) == expected, "\(json)")
        }

        for row in cases["badRecords"]?.arrayValue ?? [] {
            #expect(ActivityRecord.fromJson(row.stringValue ?? "") == nil)
        }
    }

    // ---- which face an event brings ----

    @Test
    func everyMoodCaseBringsTheSameFace() {
        let cases = SpecCases.json("tray-mood")
        let now = Date(timeIntervalSince1970: 1_789_128_000)

        func record(_ node: JsonNode?) -> ActivityRecord? {
            guard let node, node.stringValue == nil, let activity = node["activity"]?.stringValue else {
                return nil
            }
            let secondsAgo = node["secondsAgo"]?.doubleValue ?? 0
            return ActivityRecord(
                environment: "claude",
                activity: Self.activity(activity),
                at: now.addingTimeInterval(-secondsAgo))
        }

        for row in cases["forActivity"]?.arrayValue ?? [] {
            let face = row["face"]?.stringValue
            #expect(TrayMood.forActivity(record(row["before"]), record(row["now"])!, now)
                == face.map(Self.face), "\(row["now"]?["activity"]?.stringValue ?? "")")
        }

        for row in cases["forLimit"]?.arrayValue ?? [] {
            let face = row["face"]?.stringValue
            let before = row["before"]?.int64Value.map(Int.init)
            let after = row["after"]?.int64Value.map(Int.init)

            #expect(TrayMood.forLimit(before, after) == face.map(Self.face), "\(String(describing: before)) → \(String(describing: after))")
        }
    }

    private static func activity(_ name: String) -> SessionActivity {
        switch name {
        case "Working": return .working
        case "Waiting": return .waiting
        case "Done": return .done
        case "Error": return .error
        case "OutOfLimit": return .outOfLimit
        default: return .ended
        }
    }

    private static func face(_ name: String) -> AikoFace {
        switch name {
        case "Working": return .working
        case "Waiting": return .waiting
        case "Done": return .done
        case "Error": return .error
        case "Asleep": return .asleep
        case "Tired": return .tired
        default: return .fresh
        }
    }

    // ---- what Aiko remembers about the person ----

    @Test
    func everyMemoryKeyCaseAnswersTheSame() {
        let cases = SpecCases.json("memory-keys")

        #expect(cases["maxKeys"]?.int64Value == Int64(MemoryRules.maxKeys))
        #expect(cases["maxTail"]?.int64Value == Int64(MemoryRules.maxKeyTail))
        #expect(cases["minStemLength"]?.int64Value == Int64(MemoryRules.minStemLength))
        #expect(cases["maxStemTail"]?.int64Value == Int64(MemoryRules.maxStemTail))

        let nameLike = cases["nameLike"]?.arrayValue ?? []
        #expect(!nameLike.isEmpty)
        for row in nameLike {
            let key = row["key"]?.stringValue ?? ""

            #expect(MemoryRules.isNameLike(key) == row["nameLike"]?.boolValue, "\(key)")
        }

        let clean = cases["clean"]?.arrayValue ?? []
        #expect(!clean.isEmpty)
        for row in clean {
            let cleaned = MemoryRules.cleanKeys(Self.strings(row["keys"]))

            #expect(cleaned.kept == Self.strings(row["kept"]))
            #expect(cleaned.rejected == Self.strings(row["rejected"]))
        }

        let matches = cases["matches"]?.arrayValue ?? []
        #expect(!matches.isEmpty)
        for row in matches {
            let key = row["key"]?.stringValue ?? ""
            let prompt = row["prompt"]?.stringValue ?? ""

            #expect(MemoryRules.keyMatches(key, prompt) == row["matches"]?.boolValue, "\(key) in: \(prompt)")
        }
    }

    @Test
    func everyMemoryNudgeCaseAnswersTheSame() {
        let rows = SpecCases.json("memory-nudge")["cases"]?.arrayValue ?? []
        #expect(!rows.isEmpty)

        for row in rows {
            let prompt = row["prompt"]?.stringValue

            #expect(MemoryNudge.applies(prompt) == row["nudge"]?.boolValue, "\(prompt ?? "")")
        }
    }

    @Test
    func everyMemoryLimitCaseIsSavedOrRefusedTheSame() {
        let cases = SpecCases.json("memory-limits")
        let today = DateOnly(2026, 10, 11)

        #expect(cases["maxLength"]?.int64Value == Int64(MemoryRules.maxLength))

        let rows = cases["cases"]?.arrayValue ?? []
        #expect(!rows.isEmpty)
        for row in rows {
            let text = Self.repeated(row, "text")
            let meaning = Self.repeated(row, "meaning")
            let keys = Self.strings(row["keys"])
            let length = Int(row["length"]?.int64Value ?? -1)
            let saved = row["saved"]?.boolValue

            #expect(MemoryRules.lengthOf(text, meaning, keys) == length)
            #expect(MemoryRules.fits(length) == saved, "\(length)")

            let edit = MemoryEdits.remember(
                .empty, MemoryRequest(category: "you", text: text, meaning: meaning, keys: keys), today)

            #expect(edit.changed == saved, "\(length): \(edit.reply)")
            #expect(edit.memory.facts.count == (saved == true ? 1 : 0))
        }
    }

    private static func repeated(_ row: JsonNode, _ name: String) -> String {
        String(repeating: row[name]?.stringValue ?? "", count: Int(row[name + "Times"]?.int64Value ?? 0))
    }

    // ---- what a program is called on this system, and every path built from that ----

    @Test
    func everyPlatformAnswersItsOwnPaths() {
        for block in SpecCases.json("platform-paths")["platforms"]?.arrayValue ?? [] {
            let name = block["platform"]?.stringValue ?? ""
            let platform = Self.platformIn(block)

            #expect(platform.executableName(block["programName"]?.stringValue ?? "")
                == block["executableName"]?.stringValue, "\(name)")
            #expect(String(platform.pathListSeparator) == block["pathListSeparator"]?.stringValue, "\(name)")
            #expect(String(platform.directorySeparator) == block["directorySeparator"]?.stringValue, "\(name)")
            #expect(platform.localDataVariable == block["localDataVariable"]?.stringValue, "\(name)")

            let files = block["commandFiles"]
            #expect(CommandLinks.shimFileName(platform) == files?["shim"]?.stringValue, "\(name)")
            #expect(CommandLinks.fileNameFor(platform, files?["command"]?.stringValue ?? "")
                == files?["commandFile"]?.stringValue, "\(name)")

            let seen = block["seen"]
            #expect(RealClaude.formatSeen(platform, Self.strings(seen?["folders"]))
                == seen?["formatted"]?.stringValue, "\(name)")
            #expect(RealClaude.parseSeen(platform, seen?["written"]?.stringValue)
                == Self.strings(seen?["parsed"]), "\(name)")

            let real = block["realClaude"]
            let onDisk = Set(Self.strings(real?["files"]))
            #expect(RealClaude.find(platform, real?["path"]?.stringValue, Self.strings(real?["seen"]),
                                    { onDisk.contains($0) })
                == real?["found"]?.stringValue, "\(name)")

            for row in block["bridge"]?.arrayValue ?? [] {
                #expect(BridgeCommand.isAiko(platform, row["command"]?.stringValue)
                    == (row["ours"]?.boolValue ?? false), "\(name): \(row["command"]?.stringValue ?? "")")
            }

            let shell = block["shell"]
            let call = ClaudeShellLookup.callFor(platform, nil, "line")
            #expect(ClaudeShellLookup.shellFor(platform, nil) == Self.shell(shell?["kind"]?.stringValue), "\(name)")
            #expect(call.fileName == shell?["fileName"]?.stringValue, "\(name)")
            #expect(call.arguments == Self.strings(shell?["arguments"]), "\(name)")

            let install = block["install"]
            let installed = Set(Self.strings(install?["files"]))
            #expect(ClaudeInstall.find(
                platform,
                freshPath: install?["freshPath"]?.stringValue ?? "",
                commandFolder: install?["commandFolder"]?.stringValue ?? "",
                userProfile: install?["home"]?.stringValue ?? "",
                exists: { installed.contains($0) }) == install?["found"]?.stringValue, "\(name)")

            let made = block["newConfigFolder"]
            #expect(ClaudeInstall.newConfigFolder(
                platform,
                environmentName: made?["environmentName"]?.stringValue ?? "",
                userProfile: made?["home"]?.stringValue ?? "",
                folderExists: { _ in false }) == made?["folder"]?.stringValue, "\(name)")

            let credentials = block["credentials"]
            #expect(ClaudeInstall.credentialsPathIn(platform, credentials?["configFolder"]?.stringValue ?? "")
                == credentials?["path"]?.stringValue, "\(name)")

            let userPath = block["userPath"]
            let folder = userPath?["folder"]?.stringValue ?? ""
            let added = UserPathList.addToFront(platform, userPath?["before"]?.stringValue, folder)
            #expect(added == userPath?["added"]?.stringValue, "\(name)")
            #expect(UserPathList.remove(platform, added, folder) == userPath?["removed"]?.stringValue, "\(name)")
            #expect(UserPathList.contains(platform, userPath?["holds"]?.stringValue, folder), "\(name): holds")

            let forShell = block["bridgeFor"]
            #expect(BridgeCommand.forPath(forShell?["bridge"]?.stringValue,
                                          Self.shell(forShell?["shell"]?.stringValue))
                == forShell?["command"]?.stringValue, "\(name)")

            let persona = block["personaCommand"]
            let folders = AikoFolders(
                platform,
                persona?["settingsBase"]?.stringValue ?? "",
                persona?["localBase"]?.stringValue ?? "")
            #expect(AikoMarketplace.personaCommand(platform, persona?["bridge"]?.stringValue ?? "", folders)
                == persona?["command"]?.stringValue, "\(name)")
        }
    }

    /// The two systems Aiko ships on are taken as they are; a block with conventions of its own
    /// describes a system that is neither, and the core has to follow the description it is given.
    private static func platformIn(_ block: JsonNode) -> PlatformConventions {
        guard let made = block["conventions"] else {
            return block["platform"]?.stringValue == "windows" ? .windows : .macOS
        }

        let fallback = made["fallbackShell"]
        return PlatformConventions(
            executableSuffix: made["executableSuffix"]?.stringValue ?? "",
            pathListSeparator: Character(made["pathListSeparator"]?.stringValue ?? ":"),
            directorySeparator: Character(made["directorySeparator"]?.stringValue ?? "/"),
            localDataVariable: made["localDataVariable"]?.stringValue,
            fallbackShell: ShellProgram(
                shell(fallback?["kind"]?.stringValue),
                fallback?["fileName"]?.stringValue ?? "",
                strings(fallback?["argumentsBeforeCommand"])))
    }

    private static func shell(_ name: String?) -> ClaudeShell {
        switch name {
        case "PowerShell": return .powerShell
        case "Zsh": return .zsh
        default: return .gitBash
        }
    }

    private static func strings(_ node: JsonNode?) -> [String] {
        (node?.arrayValue ?? []).compactMap(\.stringValue)
    }

    // ---- the terminal: what Claude Code says and what Aiko answers ----

    @Test
    func everyLaunchOfAConversationIsPlannedTheSame() {
        let cases = SpecCases.json("launch-plan")
        let home = cases["userProfile"]?.stringValue ?? ""
        let platform: PlatformConventions = cases["platform"]?.stringValue == "macos" ? .macOS : .windows

        for row in cases["cases"]?.arrayValue ?? [] {
            let request = LaunchRequest(
                configDirectory: row["configDirectory"]?.stringValue ?? "",
                userProfile: home,
                environmentName: row["environment"]?.stringValue ?? "",
                sessionId: row["sessionId"]?.stringValue ?? "",
                started: row["started"]?.boolValue ?? false,
                hooksFile: row["hooksFile"]?.stringValue ?? "")

            let plan = ClaudeLaunchPlan.forRequest(platform, request, inheritedVariables: Self.strings(row["inherited"]))
            let name = row["name"]?.stringValue ?? ""

            #expect(plan.arguments == Self.strings(row["arguments"]), "\(name)")
            #expect(
                plan.variables == (row["variables"]?.arrayValue ?? []).map {
                    VariableChange($0["name"]?.stringValue ?? "", $0["value"]?.stringValue)
                },
                "\(name)")
        }

        for row in cases["shells"]?.arrayValue ?? [] {
            let plan = ClaudeLaunchPlan.forShell(
                platform, configDirectory: row["configDirectory"]?.stringValue ?? "", userProfile: home,
                inheritedVariables: Self.strings(row["inherited"]))
            let name = row["name"]?.stringValue ?? ""

            #expect(plan.arguments.isEmpty, "\(name)")
            #expect(
                plan.variables == (row["variables"]?.arrayValue ?? []).map {
                    VariableChange($0["name"]?.stringValue ?? "", $0["value"]?.stringValue)
                },
                "\(name)")
        }
    }

    @Test
    func everyTitleOfATerminalIsReadTheSame() {
        let cases = SpecCases.json("terminal-title")

        for row in cases["reads"]?.arrayValue ?? [] {
            let reader = TitleReader()
            let titles = (row["pieces"]?.arrayValue ?? []).map { reader.read($0.stringValue ?? "") }
            #expect(titles == (row["titles"]?.arrayValue ?? []).map(\.stringValue), "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["long"]?["rows"]?.arrayValue ?? [] {
            let length = Int(row["length"]?.int64Value ?? 0)
            let title = TitleReader().read("\u{1B}]0;" + String(repeating: "x", count: length) + "\u{07}")
            #expect((title != nil) == (row["title"]?.boolValue ?? false), "\(length)")
        }

        for row in cases["screen"]?.arrayValue ?? [] {
            #expect(
                TerminalScreen.hasText(row["output"]?.stringValue ?? "") == row["text"]?.boolValue,
                "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["claude"]?.arrayValue ?? [] {
            let parsed = ClaudeTitle.parse(row["title"]?.stringValue)
            let text = row["title"]?.stringValue ?? ""
            #expect(parsed?.working == row["working"]?.boolValue, "\(text)")
            #expect(parsed?.sessionName == row["name"]?.stringValue, "\(text)")
        }
    }

    @Test
    func everyHookOfTheTerminalSaysTheSame() {
        let cases = SpecCases.json("terminal-events")
        let now = cases["now"]?.int64Value ?? 0

        func kind(_ node: JsonNode?) -> TerminalEventKind? {
            let name = node?["kind"]?.stringValue?.lowercased()
            return TerminalEventKind.allCases.first { $0.rawValue.lowercased() == name }
        }

        for row in cases["hooks"]?.arrayValue ?? [] {
            let input = row["input"]
            let found = TerminalEvent.fromHook(input?.stringValue ?? input?.toJsonString(escaping: .relaxed), now: now)
            let expected = row["event"].flatMap { node -> TerminalEvent? in
                guard let kind = kind(node) else { return nil }
                return TerminalEvent(kind: kind, detail: node["detail"]?.stringValue ?? "", at: now)
            }
            #expect(found == expected, "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["files"]?.arrayValue ?? [] {
            #expect(TerminalEvent.fileName(row["id"]?.stringValue) == row["file"]?.stringValue)
        }

        for row in cases["saved"]?.arrayValue ?? [] {
            let read = TerminalEvent.fromJson(Self.fileIn(row["file"]))
            let expected = row["event"].flatMap { node -> TerminalEvent? in
                guard let kind = kind(node), let at = node["at"]?.int64Value else { return nil }
                return TerminalEvent(kind: kind, detail: node["detail"]?.stringValue ?? "", at: at)
            }
            #expect(read == expected)
            if let read {
                #expect(TerminalEvent.fromJson(read.toJson()) == read)
            }
        }

        func named(_ text: String?) -> TerminalEventKind? {
            TerminalEventKind.allCases.first { $0.rawValue.lowercased() == text?.lowercased() }
        }

        for row in cases["typed"]?.arrayValue ?? [] {
            #expect(
                TerminalPulse.isTyped(row["input"]?.stringValue ?? "") == row["typed"]?.boolValue,
                "\(row["name"]?.stringValue ?? "")")
        }

        for row in cases["replaces"]?.arrayValue ?? [] {
            let written = named(row["written"]?.stringValue).map { TerminalEvent(kind: $0, detail: "", at: 1) }
            let next = TerminalEvent(kind: named(row["next"]?.stringValue) ?? .using, detail: "", at: 2)
            #expect(next.replaces(written) == row["replaces"]?.boolValue, "\(row["written"]?.stringValue ?? "nil") then \(next.kind)")
        }

        for row in cases["pulse"]?.arrayValue ?? [] {
            let title = row["title"]?.stringValue.map { ClaudeTitle(working: $0 == "working", name: "name") }
            let happened = row["event"].flatMap { node -> TerminalEvent? in
                guard let kind = kind(node), let at = node["at"]?.int64Value else { return nil }
                return TerminalEvent(kind: kind, detail: "", at: at)
            }
            let activity = TerminalPulse.of(
                title, happened, seenAt: row["seenAt"]?.int64Value ?? 0, answeredAt: row["answeredAt"]?.int64Value ?? 0)
            #expect(activity.rawValue == row["activity"]?.stringValue, "\(row["name"]?.stringValue ?? "")")
        }
    }

    private static func conversation(_ row: JsonNode?) -> Conversation {
        Conversation(
            id: row?["id"]?.stringValue ?? "", environment: row?["environment"]?.stringValue ?? "",
            folder: row?["folder"]?.stringValue ?? "", name: row?["name"]?.stringValue ?? "",
            firstMessage: row?["firstMessage"]?.stringValue ?? "", lastUsed: row?["lastUsed"]?.int64Value ?? 0,
            source: ConversationSource(rawValue: row?["source"]?.stringValue ?? "") ?? .other)
    }

    @Test
    func everyConversationFileIsReadTheSame() {
        for row in SpecCases.json("conversation-file")["cases"]?.arrayValue ?? [] {
            let read = ConversationFile.read(
                id: "c1", environment: "Aiko", head: row["head"]?.stringValue ?? "", tail: row["tail"]?.stringValue ?? "",
                lastUsed: 1000)
            let expected = row["conversation"].flatMap { node -> Conversation? in
                guard node.objectValue != nil else { return nil }
                return Conversation(
                    id: "c1", environment: "Aiko", folder: node["folder"]?.stringValue ?? "", name: node["name"]?.stringValue ?? "",
                    firstMessage: node["firstMessage"]?.stringValue ?? "", lastUsed: 1000,
                    source: ConversationSource(rawValue: node["source"]?.stringValue ?? "") ?? .other)
            }
            #expect(read == expected, "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func everyPaletteOfSessionsFindsTheSame() {
        let cases = SpecCases.json("session-search")
        let sessions = TerminalSessions.fromJson(cases["file"]?.toJsonString(escaping: .relaxed))
        let conversations = (cases["conversations"]?.arrayValue ?? []).map { Self.conversation($0) }

        func marked(_ parts: [TextPart]) -> String {
            parts.map { $0.match ? "[\($0.text)]" : $0.text }.joined()
        }

        func described(_ hits: [SearchHit]) -> JsonNode {
            .array(hits.map { hit in
                var row = JsonObject()
                row["id"] = .string(hit.id)
                row["environment"] = .string(hit.environment)
                row["folder"] = .string(hit.folder)
                row["name"] = .string(marked(hit.name))
                if !hit.snippet.isEmpty {
                    row["snippet"] = .string(marked(hit.snippet))
                }
                if let source = hit.source {
                    row["source"] = .string(source.rawValue)
                }
                return .object(row)
            })
        }

        for row in cases["queries"]?.arrayValue ?? [] {
            let found = SessionSearch.find(sessions, conversations, query: row["query"]?.stringValue ?? "")
            let name = row["name"]?.stringValue ?? ""
            #expect(described(found.open) == row["open"], "\(name), open")
            #expect(described(found.closed) == row["closed"], "\(name), closed")
            #expect(described(found.outside) == row["outside"], "\(name), outside")
        }
    }

    @Test
    func everyListMatchesAndPicksFoldersTheSame() {
        let cases = SpecCases.json("text-match")

        func described(_ parts: [TextPart]?) -> JsonNode {
            guard let parts else { return .null }
            return .array(parts.map { .array([.string($0.text), .bool($0.match)]) })
        }

        for row in cases["parts"]?.arrayValue ?? [] {
            let found = described(TextMatch.parts(row["text"]?.stringValue ?? "", row["query"]?.stringValue ?? ""))
            #expect(found == row["parts"], "\(row["text"]?.stringValue ?? "")")
        }

        let sessions = TerminalSessions.fromJson(cases["sessions"]?.toJsonString(escaping: .relaxed))
        for row in cases["folders"]?.arrayValue ?? [] {
            let others = (row["others"]?.arrayValue ?? []).map {
                ($0.arrayValue?[0].stringValue ?? "", $0.arrayValue?[1].int64Value ?? 0)
            }
            let hits = FolderPick.find(
                sessions, environment: row["environment"]?.stringValue ?? "", query: row["query"]?.stringValue ?? "",
                others: others)
            let found = JsonNode.array(hits.map { .array([.string($0.folder), described($0.name), .string($0.parent)]) })
            #expect(found == row["hits"], "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func everyFileMentionOffersTheSameFiles() {
        let cases = SpecCases.json("file-mention")
        let paths = Self.strings(cases["paths"])

        for row in cases["queries"]?.arrayValue ?? [] {
            let text = row["text"]?.stringValue ?? ""
            let found = FileMention.query(text, caret: Int(row["caret"]?.int64Value ?? 0))
            #expect(found?.from == row["from"]?.int64Value.map(Int.init), "\(text)")
            #expect(found?.text == row["query"]?.stringValue, "\(text)")
        }

        for row in cases["matches"]?.arrayValue ?? [] {
            #expect(FileMention.matches(paths, query: row["query"]?.stringValue ?? "") == Self.strings(row["files"]))
        }

        for row in cases["lit"]?.arrayValue ?? [] {
            let lit = FileMention.lit(row["path"]?.stringValue ?? "", query: row["query"]?.stringValue ?? "")
            #expect(lit.start == Int(row["start"]?.int64Value ?? -2))
            #expect(lit.length == Int(row["length"]?.int64Value ?? -2))
        }
        for row in cases["dropped"]?.arrayValue ?? [] {
            let dropped = FileMention.dropped(
                row["field"]?.stringValue ?? "", caret: Int(row["caret"]?.int64Value ?? 0),
                paths: (row["paths"]?.arrayValue ?? []).compactMap(\.stringValue))
            #expect(dropped.text == row["result"]?.stringValue)
            #expect(dropped.caret == Int(row["resultCaret"]?.int64Value ?? -1))
        }
        for row in cases["chosen"]?.arrayValue ?? [] {
            let text = row["text"]?.stringValue ?? ""
            let caret = Int(row["caret"]?.int64Value ?? 0)
            guard let query = FileMention.query(text, caret: caret) else {
                Issue.record("no query in \(text)")
                continue
            }
            let (result, at) = FileMention.chosen(text, query: query, caret: caret, path: row["path"]?.stringValue ?? "")
            #expect(result == row["result"]?.stringValue)
            #expect(at == Int(row["resultCaret"]?.int64Value ?? -1))
        }
    }

    @Test
    func everyLimitBadgeNamesTheSameLimit() {
        let now = Date(timeIntervalSince1970: 1_791_892_800)

        for row in SpecCases.json("limit-badge")["badges"]?.arrayValue ?? [] {
            let snapshot = LimitSnapshot(
                environment: "claude",
                source: .statusLine,
                receivedAt: now.addingTimeInterval(-(row["ageSeconds"]?.doubleValue ?? 0)),
                windows: (row["windows"]?.arrayValue ?? []).map {
                    LimitWindow(
                        kind: $0["kind"]?.stringValue == "sevenDay" ? .sevenDay : .fiveHour,
                        percent: Int($0["percent"]?.doubleValue ?? 0),
                        resetsAt: now.addingTimeInterval($0["resetsInSeconds"]?.doubleValue ?? 0))
                })

            var expected: LimitBadge?
            if let badge = row["badge"]?.objectValue {
                expected = LimitBadge(
                    kind: badge["kind"]?.stringValue == "sevenDay" ? .sevenDay : .fiveHour,
                    percent: Int(badge["percent"]?.doubleValue ?? 0),
                    tone: Self.tone(badge["tone"]?.stringValue))
            }

            #expect(LimitBadge.forSnapshot(snapshot, now) == expected, "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func everyLeftoverOfABridgeIsFoundTheSameWay() {
        let now = Date(timeIntervalSince1970: 1_792_152_000)
        for row in SpecCases.json("snapshot-leftovers")["files"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            let age = Double(row["age"]?.int64Value ?? 0)
            let stale = SnapshotLeftovers.stale([(name: name, written: now.addingTimeInterval(-age))], now: now)
            #expect(stale.contains(name) == row["stale"]?.boolValue, "\(name)")
        }
    }

    private static func number(_ value: Int) -> JsonNode {
        .number(String(value))
    }

    private static func int(_ node: JsonNode?) -> Int {
        Int(node?.doubleValue ?? 0)
    }

    @Test
    func everySessionGetsTheSameName() {
        let cases = SpecCases.json("session-name")

        for row in cases["defaults"]?.arrayValue ?? [] {
            #expect(
                SessionName.defaultName(row["folder"]?.stringValue ?? "", taken: Self.strings(row["taken"]))
                    == row["name"]?.stringValue)
        }

        for row in cases["names"]?.arrayValue ?? [] {
            #expect(SessionName.clean(row["input"]?.stringValue) == row["name"]?.stringValue)
        }

        for row in cases["folders"]?.arrayValue ?? [] {
            let input = row["input"]?.stringValue ?? ""
            #expect(SessionName.folderPath(input) == row["path"]?.stringValue)
            #expect(SessionName.folderName(input) == row["name"]?.stringValue)
            #expect(SessionName.parentName(input) == row["parent"]?.stringValue)
        }

        for row in cases["fromMessages"]?.arrayValue ?? [] {
            let name = SessionName.fromMessage(row["message"]?.stringValue ?? "", fallback: row["fallback"]?.stringValue ?? "")
            #expect(name == row["name"]?.stringValue)
        }
    }

    @Test
    func everyConversationFileIsFoundInTheSamePlace() {
        for row in SpecCases.json("transcript-path")["paths"]?.arrayValue ?? [] {
            let folder = row["folder"]?.stringValue ?? ""

            #expect(TranscriptPath.folderName(folder) == row["folderName"]?.stringValue)
            #expect(TranscriptPath.isCut(folder) == row["cut"]?.boolValue)
            #expect(
                TranscriptPath.forSession(
                    Self.platformIn(row), configDirectory: row["config"]?.stringValue ?? "",
                    workingDirectory: folder, sessionId: row["id"]?.stringValue ?? "")
                    == row["path"]?.stringValue)
        }
    }

    @Test
    func everyListOfSessionsChangesTheSame() {
        for row in SpecCases.json("terminal-sessions")["scenarios"]?.arrayValue ?? [] {
            let described = Self.sessionsDescribed(row)

            for part in ["saved", "groups", "toOpen"] {
                #expect(
                    described[part] == row[part],
                    "\(row["name"]?.stringValue ?? ""), \(part): \(described[part]?.toJsonString(escaping: .relaxed) ?? "")")
            }
        }
    }

    /// The file of a case: an object or a list as it is, a text as a broken file, null as no file.
    private static func fileIn(_ file: JsonNode?) -> String? {
        switch file {
        case .none, .some(.null): return nil
        case .some(.string(let text)): return text
        case .some(let other): return other.toJsonString(escaping: .relaxed)
        }
    }

    private static func sessionsDescribed(_ row: JsonNode) -> JsonNode {
        var sessions = TerminalSessions.fromJson(fileIn(row["file"]))

        for step in row["steps"]?.arrayValue ?? [] {
            func word(_ key: String) -> String { step[key]?.stringValue ?? "" }

            switch step["do"]?.stringValue {
            case "add":
                sessions = sessions.add(
                    id: word("id"), environment: word("environment"), folder: word("folder"), name: word("name"),
                    now: step["now"]?.int64Value ?? 0, kind: step["kind"]?.stringValue == "shell" ? .shell : .claude)
            case "rename": sessions = sessions.rename(word("id"), word("name"))
            case "titled": sessions = sessions.titled(word("id"), word("name"))
            case "reopen": sessions = sessions.reopen(word("id"), now: step["now"]?.int64Value ?? 0)
            case "found": sessions = sessions.found(Self.conversation(step["conversation"]), now: step["now"]?.int64Value ?? 0)
            case "renameEnvironment": sessions = sessions.renameEnvironment(word("from"), word("to"))
            case "started": sessions = sessions.markStarted(word("id"))
            case "used": sessions = sessions.used(word("id"), now: step["now"]?.int64Value ?? 0)
            case "opened": sessions = sessions.opened(word("id"))
            case "close": sessions = sessions.close(word("id"))
            case "placed":
                sessions = sessions.placed(WindowPlace(
                    left: step["left"]?.doubleValue ?? 0, top: step["top"]?.doubleValue ?? 0,
                    width: step["width"]?.doubleValue ?? 0, height: step["height"]?.doubleValue ?? 0,
                    maximized: step["maximized"]?.boolValue == true))
            default: Issue.record("unknown step")
            }
        }

        let environment = row["environment"]?.stringValue ?? ""
        return node([
            ("saved", JsonNode.parse(sessions.toJson()) ?? .null),
            ("groups", .array(sessions.groups(environment).map { group in
                node([
                    ("folder", .string(group.folder)), ("name", .string(group.name)),
                    ("sessions", .array(group.sessions.map { .string($0.name) })),
                ])
            })),
            ("toOpen", text(sessions.toOpen(environment)?.id)),
        ])
    }

    @Test
    func everyFileLinkIsFoundTheSame() {
        let cases = SpecCases.json("file-link")

        func line(_ row: JsonNode) -> Int? {
            row["line"]?.doubleValue.map { Int($0) }
        }

        for row in cases["files"]?.arrayValue ?? [] {
            let expected = row["path"]?.stringValue.map { FileLink(path: $0, line: line(row)) }

            #expect(FileLink.parse(row["text"]?.stringValue) == expected, "\(row["text"]?.stringValue ?? "nil")")
        }

        for row in cases["targets"]?.arrayValue ?? [] {
            let expected: LinkTarget
            switch row["kind"]?.stringValue {
            case "web": expected = .web(row["url"]?.stringValue ?? "")
            case "file": expected = .file(FileLink(path: row["path"]?.stringValue ?? "", line: line(row)))
            default: expected = .none
            }

            #expect(LinkTarget.of(row["href"]?.stringValue) == expected, "\(row["href"]?.stringValue ?? "nil")")
        }
    }

    private static func tone(_ name: String?) -> LimitTone {
        switch name {
        case "normal": return .normal
        case "caution": return .caution
        case "critical": return .critical
        default: return .unknown
        }
    }

    // ---- the terminal: its window ----

    @Test
    func everyListOfTheTerminalWindowHasTheSameRows() {
        let cases = SpecCases.json("terminal-window")
        let sessions = TerminalSessions.fromJson(cases["sessions"]?.toJsonString(escaping: .relaxed))

        for row in cases["rows"]?.arrayValue ?? [] {
            let described = Self.describedRows(row, sessions)

            for part in ["groups", "waiting"] {
                #expect(
                    described[part] == row[part],
                    "\(row["name"]?.stringValue ?? ""), \(part): \(described[part]?.toJsonString(escaping: .relaxed) ?? "")")
            }
        }
    }

    @Test
    func everyTerminalWindowOpensAtTheSamePlace() {
        let cases = SpecCases.json("terminal-window")

        for row in cases["places"]?.arrayValue ?? [] {
            let saved = row["saved"]?.arrayValue.map { numbers in
                WindowPlace(
                    left: numbers[0].doubleValue ?? 0, top: numbers[1].doubleValue ?? 0, width: numbers[2].doubleValue ?? 0,
                    height: numbers[3].doubleValue ?? 0, maximized: numbers[4].boolValue ?? false)
            }
            let screens = (row["screens"]?.arrayValue ?? []).map { node in
                let screen = (node.arrayValue ?? []).map { $0.doubleValue ?? 0 }
                return ScreenArea(left: screen[0], top: screen[1], width: screen[2], height: screen[3])
            }
            let place = WindowPlace.fit(saved, screens)
            let sizes: [JsonNode] = [place.left, place.top, place.width, place.height].map(Self.whole)
            let described = JsonNode.array(sizes + [JsonNode.bool(place.maximized)])

            #expect(
                described == row["place"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }

        for row in cases["narrow"]?.arrayValue ?? [] {
            let pair = row.arrayValue ?? []
            #expect(WindowPlace.isNarrow(pair[0].doubleValue ?? 0) == pair[1].boolValue)
        }

        for row in cases["days"]?.arrayValue ?? [] {
            let parts = row.arrayValue ?? []
            let day = SessionRows.dayOf(
                parts[0].int64Value ?? 0, parts[1].int64Value ?? 0, offsetMinutes: Int(parts[2].int64Value ?? 0))
            #expect(String(describing: day) == parts[3].stringValue, "\(row.toJsonString(escaping: .relaxed))")
        }
    }

    @Test
    func everyTerminalWindowChoosesTheSame() {
        let cases = SpecCases.json("terminal-window")

        for row in cases["windows"]?.arrayValue ?? [] {
            let sessions = TerminalSessions.fromJson(cases["sessions"]?.toJsonString(escaping: .relaxed))
            let described = Self.describedWindow(row, sessions)

            #expect(
                described == row["steps"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
    }

    /// The cases hold whole numbers, and a number is compared by its text.
    private static func whole(_ value: Double) -> JsonNode {
        .number(String(Int64(value)))
    }

    private static func describedRows(_ row: JsonNode, _ sessions: TerminalSessions) -> JsonNode {
        let environment = row["environment"]?.stringValue ?? ""

        func live(_ id: String) -> LiveTerminal? {
            guard let terminal = row["live"]?[id] else {
                return nil
            }
            return LiveTerminal(
                activity: TerminalActivity(rawValue: terminal["activity"]?.stringValue ?? "") ?? .idle,
                line: terminal["line"]?.stringValue ?? "")
        }

        return node([
            ("groups", .array(SessionRows.of(sessions, environment, live: live).map { group in
                node([
                    ("name", .string(group.name)),
                    ("rows", .array(group.rows.map { one in
                        node([
                            ("id", .string(one.id)),
                            ("state", .string(String(describing: one.state))),
                            ("line", .string(one.line)),
                            ("lastUsed", .number(String(one.lastUsed))),
                        ])
                    })),
                ])
            })),
            ("waiting", number(SessionRows.waiting(sessions, environment, live: live))),
        ])
    }

    private static func describedWindow(_ row: JsonNode, _ start: TerminalSessions) -> JsonNode {
        var sessions = start
        var window = TerminalWindowModel.opened(sessions, strings(row["environments"]))
        var steps: [JsonNode] = []

        func note() {
            steps.append(node([
                ("environment", .string(window.environment)),
                ("session", text(window.session(sessions)?.id)),
                ("view", .string(String(describing: window.view(sessions)))),
            ]))
        }

        note()
        for action in row["actions"]?.arrayValue ?? [] {
            let pair = strings(action)
            let what = pair[1]
            switch pair[0] {
            case "environment": window = window.toEnvironment(what, sessions)
            case "session": window = window.toSession(what, sessions)
            case "next": window = window.toNeighbour(Int(what) ?? 0, sessions)
            default:
                sessions = sessions.close(what)
                window = window.after(sessions)
            }
            note()
        }

        return .array(steps)
    }

    private static func text(_ value: String?) -> JsonNode {
        value.map(JsonNode.string) ?? .null
    }

    /// The answers to questions are named only where there are some, so older cases stay as they are.
    private static func node(_ pairs: [(String, JsonNode)]) -> JsonNode {
        .object(JsonObject(pairs))
    }

    /// A stream event as the case files write it down, with the keys in the order of the files.
    private static func environmentIn(_ node: JsonNode) -> AikoEnvironment {
        AikoEnvironment(
            node["name"]?.stringValue ?? "",
            (node["configDirectories"]?.arrayValue ?? []).compactMap(\.stringValue),
            customCommand: node["command"]?.stringValue,
            projectFolders: (node["projectFolders"]?.arrayValue ?? []).compactMap(\.stringValue))
    }

    private static func problem(_ name: String?) -> CommandProblem {
        switch name {
        case "None": return .none
        case "Empty": return .empty
        case "TooLong": return .tooLong
        case "BadCharacters": return .badCharacters
        case "Reserved": return .reserved
        case "Taken": return .taken
        default: return .none
        }
    }

    private static func variable(_ name: String?) -> ConfigVariable {
        switch name {
        case "Set": return .set
        case "Clear": return .clear
        default: return .keep
        }
    }
}
