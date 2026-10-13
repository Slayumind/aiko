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
                model: row["model"]?.stringValue)

            let plan = ClaudeLaunchPlan.forRequest(platform, request, inheritedVariables: Self.strings(row["inherited"]))
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
    func everyModelIdGetsTheSameName() {
        for row in SpecCases.json("model-name")["names"]?.arrayValue ?? [] {
            #expect(ModelName.display(row["id"]?.stringValue) == row["name"]?.stringValue)
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

    private static func text(_ value: String?) -> JsonNode {
        value.map(JsonNode.string) ?? .null
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
        case .ready(let sessionId, let model, let mode, let version):
            return node([
                ("kind", .string("ready")), ("sessionId", .string(sessionId)), ("model", .string(model)),
                ("mode", modeName(mode)), ("version", .string(version)),
            ])
        case .assistant(let parent, let blocks, let error):
            return node([
                ("kind", .string("assistant")), ("parent", text(parent)), ("error", text(error)),
                ("blocks", .array(blocks.map(described))),
            ])
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
                    ])
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
            ])
        case .reply(let requestId, let ok, let mode):
            return node([
                ("kind", .string("reply")), ("requestId", .string(requestId)), ("ok", .bool(ok)),
                ("mode", modeName(mode)),
            ])
        case .status(let activity, let mode):
            return node([("kind", .string("status")), ("activity", text(activity)), ("mode", modeName(mode))])
        case .compacted:
            return node([("kind", .string("compacted"))])
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
        case .result(let isError, let subtype, let terminalReason, let words):
            return node([
                ("kind", .string("result")), ("isError", .bool(isError)), ("subtype", .string(subtype)),
                ("terminalReason", .string(terminalReason)), ("text", .string(words)),
            ])
        case .unreadable:
            return node([("kind", .string("unreadable"))])
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
        case .thinking:
            return node([("type", .string("thinking"))])
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
