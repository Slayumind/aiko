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
