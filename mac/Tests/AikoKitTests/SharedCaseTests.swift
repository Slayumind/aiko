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
