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
    func everyStreamLineBecomesTheSameEvent() {
        for row in SpecCases.json("stream-events")["events"]?.arrayValue ?? [] {
            let line = row["raw"]?.stringValue ?? row["line"]?.toJsonString(escaping: .relaxed) ?? ""
            let described = Self.described(StreamEvent.fromLine(line))

            #expect(
                described == row["event"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
    }

    @Test
    func everyHostLineIsWrittenTheSame() {
        for row in SpecCases.json("host-lines")["lines"]?.arrayValue ?? [] {
            let action = row["action"] ?? .null
            let id = action["requestId"]?.stringValue ?? ""
            let input = action["input"]?.objectValue ?? JsonObject()

            let line: String
            switch action["kind"]?.stringValue {
            case "userMessage":
                line = HostLine.userMessage(
                    action["text"]?.stringValue ?? "",
                    images: (action["images"]?.arrayValue ?? []).map {
                        AttachedImage(
                            mediaType: $0["mediaType"]?.stringValue ?? "",
                            base64: $0["base64"]?.stringValue ?? "")
                    })
            case "allow":
                line = HostLine.allow(id, input: input)
            case "allowEdits":
                line = HostLine.allowEdits(id, input: input)
            case "allowAlways":
                line = HostLine.allowAlways(id, input: input, rule: action["rule"]?.objectValue ?? JsonObject())
            case "deny":
                line = HostLine.deny(id, message: action["message"]?.stringValue ?? "")
            case "answer":
                line = HostLine.answer(
                    id,
                    input: input,
                    answers: (action["answers"]?.arrayValue ?? []).map {
                        (question: $0["question"]?.stringValue ?? "", answer: $0["answer"]?.stringValue ?? "")
                    })
            case "interrupt":
                line = HostLine.interrupt(id)
            case "setMode":
                line = HostLine.setMode(id, Self.mode(action["mode"]) ?? .manual)
            case "setModel":
                line = HostLine.setModel(id, action["model"]?.stringValue ?? "")
            case "initialize":
                line = HostLine.initialize(id)
            default:
                line = "unknown action"
            }

            #expect(line == row["line"]?.stringValue)
        }
    }

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
                mode: Self.mode(row["mode"]) ?? .manual,
                model: row["model"]?.stringValue,
                agentTodos: row["agentTodos"]?.boolValue ?? false)

            let plan = ClaudeLaunchPlan.forRequest(platform, request, inheritedVariables: Self.strings(row["inherited"]))
            let name = row["name"]?.stringValue ?? ""

            #expect(plan.arguments == Self.strings(row["arguments"]), "\(name)")
            #expect(
                plan.variables == (row["variables"]?.arrayValue ?? []).map {
                    VariableChange($0["name"]?.stringValue ?? "", $0["value"]?.stringValue)
                },
                "\(name)")
        }

        for row in cases["commands"]?.arrayValue ?? [] {
            let plan = ClaudeLaunchPlan.forCommands(
                platform, configDirectory: row["configDirectory"]?.stringValue ?? "", userProfile: home,
                inheritedVariables: Self.strings(row["inherited"]))
            let name = row["name"]?.stringValue ?? ""

            #expect(plan.arguments == Self.strings(row["arguments"]), "\(name)")
            #expect(
                plan.variables == (row["variables"]?.arrayValue ?? []).map {
                    VariableChange($0["name"]?.stringValue ?? "", $0["value"]?.stringValue)
                },
                "\(name)")
        }
    }

    @Test
    func everySlashMenuShowsTheSameCommands() {
        let cases = SpecCases.json("slash-menu")
        let commands = (cases["commands"]?.arrayValue ?? []).map {
            SlashCommand(
                name: $0["name"]?.stringValue ?? "",
                description: $0["description"]?.stringValue ?? "",
                argumentHint: $0["argumentHint"]?.stringValue ?? "",
                aliases: Self.strings($0["aliases"]),
                builtin: $0["builtin"]?.boolValue ?? false)
        }

        for row in cases["queries"]?.arrayValue ?? [] {
            let text = row["text"]?.stringValue ?? ""
            #expect(SlashMenu.query(text, caret: Int(row["caret"]?.int64Value ?? 0)) == row["query"]?.stringValue, "\(text)")
        }

        for row in cases["matches"]?.arrayValue ?? [] {
            let query = row["query"]?.stringValue ?? ""
            let found = SlashMenu.matches(commands, query: query)
            #expect(found.map(\.command.name) == Self.strings(row["names"]), "\(query)")
            let marks = (row["marks"]?.arrayValue ?? []).map { mark in
                (mark.arrayValue ?? []).map { Int($0.int64Value ?? -1) }
            }
            #expect(found.map { [$0.matchStart, $0.matchLength] } == marks, "\(query)")
            #expect(SlashMenu.lightsFirst(found) == (row["lit"]?.boolValue ?? false), "\(query)")
        }

        for row in cases["chosen"]?.arrayValue ?? [] {
            guard let command = commands.first(where: { $0.name == row["name"]?.stringValue }) else { continue }
            #expect(SlashMenu.chosen(command) == row["text"]?.stringValue)
        }
    }

    @Test
    func everyShellCommandIsReadAndWrittenTheSame() {
        let cases = SpecCases.json("shell-command")

        for row in cases["of"]?.arrayValue ?? [] {
            #expect(ShellCommand.of(row["text"]?.stringValue ?? "") == row["command"]?.stringValue)
        }
        for row in cases["message"]?.arrayValue ?? [] {
            let text = ShellCommand.message(
                row["command"]?.stringValue ?? "", output: row["output"]?.stringValue ?? "",
                exitCode: Int(row["exitCode"]?.int64Value ?? 0))
            #expect(text == row["text"]?.stringValue)
        }
        for row in cases["read"]?.arrayValue ?? [] {
            let read = ShellCommand.read(row["text"]?.stringValue ?? "")
            #expect(read?.command == row["command"]?.stringValue)
            #expect(read?.output == row["output"]?.stringValue)
        }
        for row in cases["outputs"]?.arrayValue ?? [] {
            #expect(ShellCommand.output(row["text"]?.stringValue ?? "") == row["output"]?.stringValue)
        }
    }

    @Test
    func everyFeedJoinsTheSameStepsAndCutsTheSameOutput() {
        let cases = SpecCases.json("feed-entries")

        for feed in cases["feeds"]?.arrayValue ?? [] {
            let items = (feed["items"]?.arrayValue ?? []).enumerated().map { at, item in
                Self.feedItem(item, id: at + 1)
            }
            let rows = FeedEntries.of(items, busy: feed["busy"]?.boolValue ?? false).map(Self.described)
            #expect(rows == (feed["rows"]?.arrayValue ?? []).map(Self.expectedRow), "\(feed["name"]?.stringValue ?? "")")
        }

        for row in cases["outputs"]?.arrayValue ?? [] {
            let count = Int(row["lines"]?.int64Value ?? 0)
            let text = (1...max(1, count)).map { "line \($0)" }.joined(separator: "\n") + "\n"
            let (shown, hidden) = LongOutput.cut(text)
            #expect(StepLine.lines(shown).count == Int(row["shown"]?.int64Value ?? -1))
            #expect(hidden == Int(row["hidden"]?.int64Value ?? -1))
        }
    }

    private static func feedItem(_ item: JsonNode, id: Int) -> FeedItem {
        if item["other"] != nil {
            return FeedItem(id: id, content: .answer(text: "words", streaming: false))
        }
        if let thought = item["thought"]?.stringValue {
            return FeedItem(id: id, content: .thought(text: thought))
        }
        if let waiting = item["waiting"]?.stringValue {
            return FeedItem(id: id, content: .ownerMessage(text: waiting, queued: true, attachments: []))
        }

        let kind: StepKind = switch item["kind"]?.stringValue {
        case "read": .read
        case "search": .search
        case "edit": .edit
        case "run": .run
        default: .other
        }
        let state: StepState = switch item["state"]?.stringValue {
        case "running": .running
        case "failed": .failed
        default: .done
        }
        let line = StepLine(
            kind: kind, label: "", subject: item["subject"]?.stringValue ?? "", added: 0, removed: 0,
            hidden: item["hidden"]?.boolValue ?? false)
        return FeedItem(id: id, content: .step(FeedStep(
            toolUseId: "toolu_\(id)", line: line, state: state, result: "", images: [], progress: "")))
    }

    /// A row as a plain string, so both sides compare without a JSON writer.
    private static func described(_ entry: FeedEntry) -> String {
        if case .thought(_, let open) = entry {
            return "thought \(entry.id) \(open)"
        }
        guard case .run(let run) = entry else {
            return "one \(entry.id)"
        }
        let (names, more) = run.subjects()
        return "run \(run.steps.indices.map { String(run.items[$0].id) }.joined(separator: ",")) \(run.kind) \(run.state) "
            + "\(names.joined(separator: ",")) +\(more)"
    }

    private static func expectedRow(_ row: JsonNode) -> String {
        if let one = row["one"]?.int64Value {
            return "one \(one)"
        }
        if let thought = row["thought"]?.int64Value {
            return "thought \(thought) \(row["open"]?.boolValue ?? false)"
        }
        let ids = (row["run"]?.arrayValue ?? []).map { String($0.int64Value ?? 0) }.joined(separator: ",")
        let names = (row["names"]?.arrayValue ?? []).compactMap(\.stringValue).joined(separator: ",")
        return "run \(ids) \(row["kind"]?.stringValue ?? "") \(row["state"]?.stringValue ?? "") \(names) +\(row["more"]?.int64Value ?? -1)"
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
    func everyPermissionModeHasTheSameNames() {
        let cases = SpecCases.json("permission-mode")

        #expect(PermissionModes.all.map(PermissionModes.name) == Self.strings(cases["order"]))

        for row in cases["modes"]?.arrayValue ?? [] {
            guard let mode = Self.mode(row["mode"]) else {
                Issue.record("unknown mode in the cases")
                continue
            }

            #expect(PermissionModes.name(mode) == row["name"]?.stringValue)
            #expect(PermissionModes.flagValue(mode) == row["flag"]?.stringValue)
            #expect(PermissionModes.requestValue(mode) == row["request"]?.stringValue)
            #expect(PermissionModes.next(mode) == Self.mode(row["next"]))
        }

        for row in cases["fromStream"]?.arrayValue ?? [] {
            #expect(PermissionModes.fromStream(row["value"]?.stringValue) == Self.mode(row["mode"]))
        }

        for row in cases["defaults"]?.arrayValue ?? [] {
            let settings = (row["settings"]?.arrayValue ?? []).map(\.stringValue)
            #expect(
                PermissionModes.defaultMode(settings) == Self.mode(row["mode"]),
                "\(row["name"]?.stringValue ?? "")")
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

    @Test
    func everyModelIdGetsTheSameName() {
        for row in SpecCases.json("model-name")["names"]?.arrayValue ?? [] {
            #expect(ModelName.display(row["id"]?.stringValue) == row["name"]?.stringValue)
        }
    }

    @Test
    func everyModelLabelWaitsTheSameWay() {
        for row in SpecCases.json("model-name")["labels"]?.arrayValue ?? [] {
            let label = ModelName.label(asked: row["asked"]?.stringValue, known: row["known"]?.stringValue)
            #expect(label.text == row["text"]?.stringValue)
            #expect(label.waiting == row["waiting"]?.boolValue)
        }
    }

    @Test
    func everyChangeOfModelGivesTheSameFullId() {
        for row in SpecCases.json("model-name")["switches"]?.arrayValue ?? [] {
            #expect(ModelName.fromSwitch(row["text"]?.stringValue) == row["id"]?.stringValue)
        }
    }

    @Test
    func everyMarkdownTextIsReadIntoTheSameBlocks() {
        for row in SpecCases.json("markdown")["texts"]?.arrayValue ?? [] {
            let described = JsonNode.array(Markdown.parse(row["markdown"]?.stringValue).map(Self.describedBlock))

            #expect(
                described == row["blocks"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
    }

    @Test
    func everyEditShowsTheSameDiff() {
        for row in SpecCases.json("edit-diff")["diffs"]?.arrayValue ?? [] {
            let diff = EditDiff.forTool(row["tool"]?.stringValue ?? "", row["input"]?.objectValue ?? JsonObject())
            let lines: JsonNode = diff.map { found in
                .array(found.lines.map { .array([.string(String(describing: $0.side)), .string($0.text)]) })
            } ?? .null
            let more: JsonNode = diff.map { .number(String($0.more)) } ?? .null

            #expect(lines == row["lines"], "\(row["name"]?.stringValue ?? "")")
            #expect(more == row["more"], "\(row["name"]?.stringValue ?? "")")
        }
    }

    @Test
    func everyStepGetsTheSameLine() {
        for row in SpecCases.json("step-line")["steps"]?.arrayValue ?? [] {
            let tool = row["tool"]?.stringValue ?? ""
            let described = Self.describedStep(StepLine.forTool(tool, row["input"]?.objectValue ?? JsonObject()))

            #expect(described == row["line"], "\(tool): \(described.toJsonString(escaping: .relaxed))")
        }
    }

    @Test
    func everyRequestBecomesTheSameCard() {
        for row in SpecCases.json("request-card")["cards"]?.arrayValue ?? [] {
            guard let card = Self.cardIn(row) else {
                Issue.record("not a request: \(row["name"]?.stringValue ?? "")")
                continue
            }
            let described = Self.describedCard(card)

            #expect(
                described == row["card"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")

            for choice in row["choices"]?.arrayValue ?? [] {
                #expect(card.lineFor(Self.choiceIn(choice)) == choice["line"]?.stringValue)
            }
        }
    }

    @Test
    func everyConversationEndsInTheSameState() {
        for row in SpecCases.json("conversation")["scenarios"]?.arrayValue ?? [] {
            let (state, written) = Self.played(row)
            let described = Self.describedState(state)
            let name = row["name"]?.stringValue ?? ""

            #expect(described == row["state"], "\(name): \(described.toJsonString(escaping: .relaxed))")
            #expect(written == Self.strings(row["written"]), "\(name)")
        }
    }

    private static func cardIn(_ row: JsonNode) -> RequestCard? {
        guard case .request(let request) = StreamEvent.fromLine(row["request"]?.toJsonString(escaping: .relaxed)) else {
            return nil
        }
        return RequestCard.from(request)
    }

    private static func choiceIn(_ choice: JsonNode) -> CardChoice {
        switch choice["choice"]?.stringValue {
        case "second": return .second
        case "deny": return .deny(choice["message"]?.stringValue ?? "")
        case "answers": return .answers((choice["picked"]?.arrayValue ?? []).map { strings($0) })
        default: return .allow
        }
    }

    /// A scenario of the conversation cases, step by step.
    private static func played(_ scenario: JsonNode) -> (Conversation, [String]) {
        var state = Conversation.empty()
        var written: [String] = []

        // A command the host is asked to run shows among the lines as "shell: <command>", and one
        // it is asked to stop as "stop shell".
        func take(_ outcome: Outcome) {
            state = outcome.state
            written += outcome.written
            if outcome.stopShell {
                written.append("stop shell")
            }
            if let run = outcome.shell {
                written.append("shell: " + run.command)
            }
        }

        for step in scenario["steps"]?.arrayValue ?? [] {
            if let raw = step["raw"]?.stringValue {
                take(state.on(StreamEvent.fromLine(raw)))
            } else if let line = step["line"] {
                take(state.on(StreamEvent.fromLine(line.toJsonString(escaping: .relaxed))))
            } else {
                switch step["do"]?.stringValue {
                case "send":
                    take(state.send(
                        step["text"]?.stringValue ?? "",
                        images: (step["images"]?.arrayValue ?? []).map {
                            AttachedImage(
                                mediaType: $0["mediaType"]?.stringValue ?? "",
                                base64: $0["base64"]?.stringValue ?? "")
                        }))
                case "choose":
                    take(state.choose(step["requestId"]?.stringValue ?? "", choiceIn(step)))
                case "interrupt":
                    take(state.interrupt())
                case "setMode":
                    take(state.setMode(mode(step["mode"]) ?? .manual))
                case "markRead":
                    state = state.markRead()
                case "exit":
                    state = state.processExited()
                case "shellEnded":
                    let running = state.items.last { item in
                        if case .shell(_, .running, _, _) = item.content { return true }
                        return false
                    }
                    take(state.shellEnded(
                        running?.id ?? -1,
                        output: step["output"]?.stringValue ?? "",
                        exitCode: Int(step["exitCode"]?.int64Value ?? 0)))
                default:
                    Issue.record("unknown step")
                }
            }
        }

        return (state, written)
    }

    private static func number(_ value: Int) -> JsonNode {
        .number(String(value))
    }

    private static func describedStep(_ line: StepLine) -> JsonNode {
        node([
            ("kind", .string(String(describing: line.kind))), ("label", .string(line.label)),
            ("subject", .string(line.subject)), ("added", number(line.added)), ("removed", number(line.removed)),
            ("hidden", .bool(line.hidden)),
        ])
    }

    private static func describedCard(_ card: RequestCard) -> JsonNode {
        node([
            ("kind", .string(String(describing: card.kind))), ("second", .string(String(describing: card.second))),
            ("rule", .string(card.ruleText)),
            ("step", describedStep(card.step)),
            ("questions", .array(card.questions.map { question in
                node([
                    ("text", .string(question.text)), ("header", .string(question.header)),
                    ("multi", .bool(question.multiSelect)),
                    ("options", .array(question.options.map {
                        node([("label", .string($0.label)), ("description", .string($0.description))]
                            + ($0.preview.isEmpty ? [] : [("preview", .string($0.preview))]))
                    })),
                ])
            })),
            ("plan", .string(card.plan)),
        ])
    }

    private static func describedState(_ state: Conversation) -> JsonNode {
        node([
            ("activity", .string(String(describing: state.activity))),
            ("mode", .string(PermissionModes.name(state.mode))), ("model", .string(state.model)),
            ("session", .string(state.sessionId)), ("tasks", number(state.tasks)), ("unread", .bool(state.unread)),
            ("compacting", .bool(state.compacting)), ("limits", number(state.limits.count)),
            ("items", .array(state.items.map(describedItem))),
        ] + describedTodos(state) + describedContext(state))
    }

    /// The to-do list and the context are named only where a conversation has them, so older
    /// cases stay as they are.
    private static func describedTodos(_ state: Conversation) -> [(String, JsonNode)] {
        state.todos.isEmpty ? [] : [("todos", .array(state.todos.map {
            node([("id", .string($0.id)), ("text", .string($0.text)), ("doing", .string($0.doing)), ("state", .string($0.state.rawValue))])
        }))]
    }

    private static func describedContext(_ state: Conversation) -> [(String, JsonNode)] {
        guard let percent = state.contextPercent else { return [] }
        return [("context", node([
            ("tokens", number(state.contextTokens)), ("window", number(state.contextWindow)),
            ("percent", number(percent)), ("level", .string(ContextGauge.level(percent).rawValue)),
        ]))]
    }

    @Test
    func everyContextGaugeReadsTheSame() {
        let cases = SpecCases.json("context-gauge")
        for row in cases["percent"]?.arrayValue ?? [] {
            let percent = ContextGauge.percent(tokens: Self.int(row["tokens"]), window: Self.int(row["window"]))
            #expect(percent == row["percent"]?.doubleValue.map { Int($0) }, "\(row)")
        }
        for row in cases["level"]?.arrayValue ?? [] {
            #expect(ContextGauge.level(Self.int(row["percent"])).rawValue == row["level"]?.stringValue)
        }
        for row in cases["window"]?.arrayValue ?? [] {
            let windows = (row["windows"]?.objectValue?.members ?? []).map { ModelWindow(model: $0.key, window: Self.int($0.value)) }
            #expect(
                ContextGauge.windowFor(model: row["model"]?.stringValue ?? "", windows: windows, known: Self.int(row["known"]))
                    == Self.int(row["window"]))
        }
    }

    private static func int(_ node: JsonNode?) -> Int {
        Int(node?.doubleValue ?? 0)
    }

    private static func describedItem(_ item: FeedItem) -> JsonNode {
        switch item.content {
        case .ownerMessage(let words, let queued, let attachments):
            return node([("owner", .string(words)), ("images", number(attachments.count)), ("queued", .bool(queued))])
        case .answer(let words, let streaming):
            return node([("answer", .string(words)), ("streaming", .bool(streaming))])
        case .step(let step):
            return node([
                ("step", describedStep(step.line)), ("state", .string(String(describing: step.state))),
                ("result", .string(step.result)), ("images", number(step.images.count)),
                ("progress", .string(step.progress)),
            ] + answered(step.answers))
        case .card(let request, let state):
            return node([
                ("card", .string(String(describing: request.kind))), ("state", .string(String(describing: state))),
                ("requestId", .string(request.requestId)), ("second", .string(String(describing: request.second))),
            ])
        case .notice(let kind, let words, let size):
            var pairs: [(String, JsonNode)] = [("notice", .string(String(describing: kind))), ("text", .string(words))]
            if let size {
                pairs += [("before", .number(String(size.before))), ("after", .number(String(size.after)))]
            }
            return node(pairs)
        case .thought(let text):
            return node([("thought", .string(text))])
        case .shell(let command, let state, let output, let exitCode):
            return node([
                ("shell", .string(command)), ("state", .string(String(describing: state))),
                ("output", .string(output)), ("exitCode", .number(String(exitCode))),
            ])
        }
    }

    @Test
    func everyConversationIsRunTheSame() {
        for row in SpecCases.json("conversation-runner")["scenarios"]?.arrayValue ?? [] {
            let described = Self.runDescribed(row)

            for part in ["starts", "written", "log", "activity", "items", "running"] {
                #expect(
                    described[part] == row[part],
                    "\(row["name"]?.stringValue ?? ""), \(part): \(described[part]?.toJsonString(escaping: .relaxed) ?? "")")
            }
        }
    }

    /// A scenario of the runner cases, played against a scripted Claude Code.
    private static func runDescribed(_ row: JsonNode) -> JsonNode {
        guard let talk = TalkScript.parse(row.toJsonString(escaping: .relaxed)) else {
            return .null
        }

        let claude = ScriptedClaude(talk.replies)
        let log = SpokenLog()
        let runner = ConversationRunner(
            .macOS,
            RunnerSetup(
                executable: "claude", workingDirectory: "/work", configDirectory: "/accounts/.claude-work",
                userProfile: "/home/someone", environmentName: "Work", sessionId: "id-1",
                started: row["started"]?.boolValue ?? false, mode: mode(row["mode"]) ?? .manual, model: nil,
                inheritedVariables: []),
            newProcess: claude.newProcess,
            post: { work in work() },
            log: log)

        talk.play(runner)

        return node([
            ("starts", .array(claude.starts.map { start in
                node([
                    ("resumed", .bool(start.arguments.contains("--resume"))),
                    ("mode", .string(start.arguments[(start.arguments.firstIndex(of: "--permission-mode") ?? 0) + 1])),
                ])
            })),
            ("written", .array(claude.written.map { .string($0) })),
            ("log", .array(log.lines.map { .string($0) })),
            ("activity", .string(String(describing: runner.state.activity))),
            ("items", number(runner.state.items.count)),
            ("running", .bool(runner.isRunning)),
        ])
    }

    /// The log of the terminal as words, so a case file can list what was said.
    private final class SpokenLog: TerminalLog {
        var lines: [String] = []

        func started(_ environment: String, resumed: Bool) {
            lines.append("started \(environment) \(resumed ? "resumed" : "new")")
        }

        func startFailed(_ environment: String) {
            lines.append("start failed \(environment)")
        }

        func exited(_ environment: String, exitCode: Int, linesRead: Int, linesWritten: Int) {
            lines.append("exited \(environment) code \(exitCode) read \(linesRead) written \(linesWritten)")
        }

        func stopped(_ environment: String, linesRead: Int, linesWritten: Int) {
            lines.append("stopped \(environment) read \(linesRead) written \(linesWritten)")
        }
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

    @Test
    func everyConversationFileIsReadIntoTheSameFeed() {
        for row in SpecCases.json("transcript")["transcripts"]?.arrayValue ?? [] {
            let described = Self.transcriptDescribed(row)

            #expect(
                described == row["state"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
    }

    @Test
    func everyFormOfANewSessionOffersTheSame() {
        let cases = SpecCases.json("new-session")
        let settings = EnvironmentSettings((cases["environments"]?.arrayValue ?? []).map(Self.environmentIn))

        for row in cases["forms"]?.arrayValue ?? [] {
            let described = Self.formDescribed(row, settings, Self.platformIn(.object(cases)))

            for part in ["folders", "names", "elsewhere", "model", "headers"] {
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
                    model: step["model"]?.stringValue, mode: mode(step["mode"]) ?? .manual,
                    now: step["now"]?.int64Value ?? 0)
            case "rename": sessions = sessions.rename(word("id"), word("name"))
            case "renameEnvironment": sessions = sessions.renameEnvironment(word("from"), word("to"))
            case "setMode": sessions = sessions.setMode(word("id"), mode(step["mode"]) ?? .manual)
            case "setModel": sessions = sessions.setModel(word("id"), step["model"]?.stringValue)
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

    private static func transcriptDescribed(_ row: JsonNode) -> JsonNode {
        let lines = (row["lines"]?.arrayValue ?? []).map { line in
            line.stringValue ?? line.toJsonString(escaping: .relaxed)
        }
        let state = Transcript.read(lines, mode: .plan)

        return node([
            ("activity", .string(String(describing: state.activity))),
            ("mode", .string(PermissionModes.name(state.mode))),
            ("busy", .bool(state.isBusy)),
            ("nextId", number(state.nextId)),
            ("items", .array(state.items.map(describedItem))),
        ])
    }

    private static func formDescribed(
        _ row: JsonNode, _ settings: EnvironmentSettings, _ platform: PlatformConventions
    ) -> JsonNode {
        let sessions = TerminalSessions.fromJson(fileIn(row["sessions"]))
        guard let environment = settings.environments.first(where: { $0.name == row["environment"]?.stringValue })
        else {
            return .null
        }

        let folders = NewSessionChoices.folders(sessions, environment) { bound in
            strings(row["inside"]?[bound])
        }

        return node([
            ("folders", .array(folders.map { choice in
                node([
                    ("path", .string(choice.path)), ("name", .string(choice.name)),
                    ("source", .string(String(describing: choice.source))),
                ])
            })),
            ("names", .array(strings(row["nameFor"]).map {
                .string(NewSessionChoices.defaultName(sessions, environment.name, $0))
            })),
            ("elsewhere", .array(strings(row["elsewhereFor"]).map {
                text(NewSessionChoices.boundElsewhere(platform, $0, environment, settings))
            })),
            ("model", .string(NewSessionChoices.defaultModel(sessions, environment.name))),
            ("headers", .array(NewSessionChoices.headers(folders).map { header in
                node([
                    ("before", .number(String(header.before))),
                    ("group", .string(String(describing: header.group))),
                ])
            })),
        ])
    }

    @Test
    func everyAnswerIsCutIntoTheSamePieces() {
        for row in SpecCases.json("answer-segments")["answers"]?.arrayValue ?? [] {
            let segments = AnswerSegments.of(Markdown.parse(row["markdown"]?.stringValue))

            let described = JsonNode.array(segments.map { segment in
                switch segment {
                case .prose(let blocks):
                    return Self.node([("kind", .string("prose")), ("blocks", .number(String(blocks.count)))])
                case .code(let language, _):
                    return Self.node([("kind", .string("code")), ("language", .string(language))])
                case .table(_, _, let rows):
                    return Self.node([("kind", .string("table")), ("rows", .number(String(rows.count)))])
                }
            })

            #expect(
                described == row["segments"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
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

    /// Markdown blocks as the case files write them down, with the keys in the order of the files.
    private static func describedBlock(_ block: MarkdownBlock) -> JsonNode {
        switch block {
        case .paragraph(let inlines):
            return node([("type", .string("paragraph")), ("inlines", describedInlines(inlines))])
        case .heading(let level, let inlines):
            return node([
                ("type", .string("heading")), ("level", .number(String(level))),
                ("inlines", describedInlines(inlines)),
            ])
        case .code(let language, let words):
            return node([("type", .string("code")), ("language", .string(language)), ("text", .string(words))])
        case .list(let ordered, let start, let items):
            return node([
                ("type", .string("list")), ("ordered", .bool(ordered)), ("start", .number(String(start))),
                ("items", .array(items.map { .array($0.map(describedBlock)) })),
            ])
        case .quote(let blocks):
            return node([("type", .string("quote")), ("blocks", .array(blocks.map(describedBlock)))])
        case .table(let columns, let header, let rows):
            return node([
                ("type", .string("table")),
                ("columns", .array(columns.map { .string(columnName($0)) })),
                ("header", .array(header.map(describedInlines))),
                ("rows", .array(rows.map { .array($0.map(describedInlines)) })),
            ])
        case .rule:
            return node([("type", .string("rule"))])
        }
    }

    private static func describedInlines(_ inlines: [MarkdownInline]) -> JsonNode {
        .array(inlines.map { inline in
            switch inline {
            case .text(let value): return node([("text", .string(value))])
            case .code(let value): return node([("code", .string(value))])
            case .strong(let inner): return node([("strong", describedInlines(inner))])
            case .emphasis(let inner): return node([("emphasis", describedInlines(inner))])
            case .strike(let inner): return node([("strike", describedInlines(inner))])
            case .link(let target, let inner):
                return node([("link", .string(target)), ("inlines", describedInlines(inner))])
            }
        })
    }

    private static func columnName(_ column: ColumnAlign) -> String {
        switch column {
        case .none: return "none"
        case .left: return "left"
        case .center: return "center"
        case .right: return "right"
        }
    }

    private static func mode(_ node: JsonNode?) -> PermissionMode? {
        PermissionModes.all.first { PermissionModes.name($0) == node?.stringValue }
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
    func everyConversationLooksTheSameInTheList() {
        for row in SpecCases.json("conversation")["scenarios"]?.arrayValue ?? [] {
            let described = Self.describedGlance(TalkGlance.of(Self.played(row).0))

            #expect(
                described == row["glance"],
                "\(row["name"]?.stringValue ?? ""): \(described.toJsonString(escaping: .relaxed))")
        }
    }

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
    func everyFifthProcessStopsTheSameSession() {
        for row in SpecCases.json("terminal-window")["caps"]?.arrayValue ?? [] {
            let running = (row["running"]?.arrayValue ?? []).map { node in
                let talk = node.arrayValue ?? []
                return RunningTalk(
                    id: talk[0].stringValue ?? "", busy: talk[1].boolValue ?? false, lastUsed: talk[2].int64Value ?? 0)
            }

            #expect(
                ProcessCap.toStop(running, starting: row["starting"]?.stringValue ?? "") == row["stop"]?.stringValue,
                "\(row["name"]?.stringValue ?? "")")
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

    private static func describedGlance(_ glance: TalkGlance) -> JsonNode {
        node([
            ("activity", .string(String(describing: glance.activity))),
            ("line", .string(String(describing: glance.line))),
            ("kind", .string(String(describing: glance.kind))),
            ("label", .string(glance.label)),
            ("text", .string(glance.text)),
        ])
    }

    private static let activities: [String: TalkActivity] = [
        "idle": .idle, "working": .working, "waiting": .waiting, "done": .done, "failed": .failed, "signedOut": .signedOut,
    ]

    private static let glanceLines: [String: GlanceLine] = [
        "none": .none, "request": .request, "question": .question, "plan": .plan, "step": .step, "answer": .answer,
        "stoppedAt": .stoppedAt, "signIn": .signIn,
    ]

    private static let stepKinds: [String: StepKind] = [
        "read": .read, "search": .search, "edit": .edit, "run": .run, "agent": .agent, "web": .web, "other": .other,
    ]

    private static func describedRows(_ row: JsonNode, _ sessions: TerminalSessions) -> JsonNode {
        let environment = row["environment"]?.stringValue ?? ""

        func live(_ id: String) -> LiveTalk? {
            guard let talk = row["live"]?[id] else {
                return nil
            }
            return LiveTalk(
                glance: TalkGlance(
                    activity: activities[talk["activity"]?.stringValue ?? ""] ?? .idle,
                    line: glanceLines[talk["line"]?.stringValue ?? ""] ?? GlanceLine.none,
                    kind: stepKinds[talk["kind"]?.stringValue ?? ""] ?? .other,
                    label: talk["label"]?.stringValue ?? "",
                    text: talk["text"]?.stringValue ?? ""),
                running: talk["running"]?.boolValue ?? false)
        }

        return node([
            ("groups", .array(SessionRows.of(sessions, environment, live: live).map { group in
                node([
                    ("name", .string(group.name)),
                    ("rows", .array(group.rows.map { one in
                        node([
                            ("id", .string(one.id)),
                            ("state", .string(String(describing: one.state))),
                            ("line", text(one.glance.map { String(describing: $0.line) })),
                            ("text", text(one.glance?.text)),
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
            case "form": window = window.withForm(what == "open")
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
    private static func answered(_ answers: [QuestionAnswer]) -> [(String, JsonNode)] {
        answers.isEmpty ? [] : [("answers", .array(answers.map {
            node([("header", .string($0.header)), ("answer", .string($0.answer))])
        }))]
    }

    private static func node(_ pairs: [(String, JsonNode)]) -> JsonNode {
        .object(JsonObject(pairs))
    }

    private static func modeName(_ mode: PermissionMode?) -> JsonNode {
        text(mode.map(PermissionModes.name))
    }

    /// A stream event as the case files write it down, with the keys in the order of the files.
    private static func described(_ event: StreamEvent) -> JsonNode {
        switch event {
        case .ready(let sessionId, let model, let mode):
            return node([
                ("kind", .string("ready")), ("sessionId", .string(sessionId)), ("model", .string(model)),
                ("mode", modeName(mode)),
            ])
        case .assistant(let parent, let blocks, let error, let contextTokens):
            // The size of the context is named only where the line has one, so older cases stay.
            return node([
                ("kind", .string("assistant")), ("parent", text(parent)), ("error", text(error)),
                ("blocks", .array(blocks.map(described))),
            ] + (contextTokens > 0 ? [("contextTokens", number(contextTokens))] : []))
        case .user(let parent, let words, let results, let isReplay, let isSynthetic):
            return node([
                ("kind", .string("user")), ("parent", text(parent)), ("text", .string(words)),
                ("replay", .bool(isReplay)), ("synthetic", .bool(isSynthetic)),
                ("results", .array(results.map { result in
                    node([
                        ("id", .string(result.toolUseId)), ("error", .bool(result.isError)),
                        ("text", .string(result.text)),
                        ("images", .array(result.images.map {
                            node([("mediaType", .string($0.mediaType)), ("data", .string($0.data))])
                        })),
                    ] + answered(result.answers))
                })),
            ])
        case .textDelta(let parent, let words):
            return node([("kind", .string("textDelta")), ("parent", text(parent)), ("text", .string(words))])
        case .request(let request):
            return node([
                ("kind", .string("request")), ("requestId", .string(request.requestId)),
                ("tool", .string(request.toolName)), ("toolUseId", .string(request.toolUseId)),
                ("input", .object(request.input)), ("description", .string(request.description)),
                ("suggestsAcceptEdits", .bool(request.suggestsAcceptEdits)),
                ("needsAnswer", .bool(request.needsAnswer)),
                ("agent", text(request.agentId)), ("rule", .string(request.ruleText)),
            ])
        case .tasks(let count):
            return node([("kind", .string("tasks")), ("count", .number(String(count)))])
        case .taskProgress(let toolUseId, let description):
            return node([
                ("kind", .string("taskProgress")), ("toolUseId", .string(toolUseId)),
                ("description", .string(description)),
            ])
        case .taskStarted(let toolUseId):
            return node([("kind", .string("taskStarted")), ("toolUseId", .string(toolUseId))])
        case .taskEnded(let toolUseId, let how):
            return node([("kind", .string("taskEnded")), ("toolUseId", .string(toolUseId)), ("how", .string(how))])
        case .commands(let requestId, let list, let models):
            var first: JsonNode = .null
            if let command = list.first {
                first = node([
                    ("description", .string(command.description)), ("argumentHint", .string(command.argumentHint)),
                    ("aliases", .array(command.aliases.map { .string($0) })), ("builtin", .bool(command.builtin)),
                ])
            }
            return node([
                ("kind", .string("commands")), ("requestId", .string(requestId)),
                ("names", .array(list.map { .string($0.name) })), ("first", first),
                ("models", .array(models.map { .string("\($0.value)=\($0.displayName)") })),
            ])
        case .reply(let requestId, let ok, let mode):
            return node([
                ("kind", .string("reply")), ("requestId", .string(requestId)), ("ok", .bool(ok)),
                ("mode", modeName(mode)),
            ])
        case .status(let activity, let mode):
            return node([("kind", .string("status")), ("activity", text(activity)), ("mode", modeName(mode))])
        case .compacted(let before, let after):
            return node([("kind", .string("compacted")), ("before", .number(String(before))), ("after", .number(String(after)))])
        case .rateLimit(let windows):
            return node([
                ("kind", .string("rateLimit")),
                ("windows", .array(windows.map {
                    node([
                        ("kind", .string($0.kind == .fiveHour ? "fiveHour" : "sevenDay")),
                        ("percent", .number(String($0.percent))),
                        ("resetsAt", .number(String(Int64($0.resetsAt.timeIntervalSince1970)))),
                    ])
                })),
            ])
        case .result(let isError, let subtype, let terminalReason, let words, let windows):
            return node([
                ("kind", .string("result")), ("isError", .bool(isError)), ("subtype", .string(subtype)),
                ("terminalReason", .string(terminalReason)), ("text", .string(words)),
            ] + (windows.isEmpty ? [] : [("contextWindows", node(windows.map { ($0.model, number($0.window)) }))]))
        case .ignored:
            return node([("kind", .string("ignored"))])
        }
    }

    private static func described(_ block: AssistantBlock) -> JsonNode {
        switch block {
        case .text(let value):
            return node([("type", .string("text")), ("text", .string(value))])
        case .toolUse(let id, let name, let input):
            return node([
                ("type", .string("toolUse")), ("id", .string(id)), ("name", .string(name)),
                ("input", .object(input)),
            ])
        case .thinking(let words):
            return words.isEmpty
                ? node([("type", .string("thinking"))])
                : node([("type", .string("thinking")), ("text", .string(words))])
        }
    }

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
