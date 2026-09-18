import Testing

@testable import AikoKit

/// Environment 1 in .claude, environment 2 in a folder of its own, commands and project folders.
struct EnvironmentModelTests {
    static let home = #"C:\Users\someone"#

    private static func env(_ name: String, _ folder: String, _ projects: String...) -> AikoEnvironment {
        AikoEnvironment(name, [PlatformConventions.windows.join(home, folder)], projectFolders: projects)
    }

    // ---- the two environments ----

    @Test
    func environment1IsTheOneInClaudeWhateverTheOrder() {
        let settings = EnvironmentSettings([
            Self.env("Personal", ".claude-personal"),
            Self.env("Work", ".claude"),
        ])

        #expect(settings.first(.windows, Self.home)?.name == "Work")
        #expect(settings.second(.windows, Self.home)?.name == "Personal")
        #expect(settings.extras(.windows, Self.home).isEmpty)
    }

    @Test
    func aThirdEnvironmentFromAnOlderListIsAnExtraAndTheRingIsKept() {
        // This machine before the two-environment rule: Personal in the ring, Main, and a dead Work.
        let settings = EnvironmentSettings(
            [
                Self.env("Personal", ".claude-personal"),
                Self.env("Main", ".claude"),
                Self.env("Work", ".claude-work"),
            ],
            ringEnvironment: "Personal")

        #expect(settings.first(.windows, Self.home)?.name == "Main")
        #expect(settings.second(.windows, Self.home)?.name == "Personal")
        #expect(settings.extras(.windows, Self.home).map(\.name) == ["Work"])
    }

    @Test
    func withoutAClaudeEnvironmentThereIsNoSecondOne() {
        let settings = EnvironmentSettings([Self.env("Personal", ".claude-personal")])

        #expect(settings.first(.windows, Self.home) == nil)
        #expect(settings.second(.windows, Self.home) == nil)
        #expect(settings.extras(.windows, Self.home).isEmpty)
    }

    @Test
    func theDefaultEnvironmentFallsBackToEnvironment1() {
        var settings = EnvironmentSettings([
            Self.env("Personal", ".claude-personal"),
            Self.env("Work", ".claude"),
        ])

        #expect(settings.defaultEnvironmentIn(.windows, Self.home)?.name == "Work")

        settings.defaultEnvironment = "Personal"
        #expect(settings.defaultEnvironmentIn(.windows, Self.home)?.name == "Personal")

        settings.defaultEnvironment = "Gone"
        #expect(settings.defaultEnvironmentIn(.windows, Self.home)?.name == "Work")
    }

    // ---- commands ----

    @Test(arguments: [
        ("Work", "aiko-work"),
        ("Personal", "aiko-personal"),
        ("Основная", "aiko-osnovnaya"),
        ("Личная щука", "aiko-lichnaya-shchuka"),
        ("Side project!", "aiko-side-project"),
        ("  ", "aiko-env"),
        ("✨", "aiko-env"),
    ] as [(String, String)])
    func theCommandFollowsTheEnvironmentName(name: String, command: String) {
        #expect(LaunchCommand.fromEnvironmentName(name) == command)
    }

    @Test
    func renamingChangesTheCommandUntilThePersonTypesTheirOwn() {
        var work = AikoEnvironment("Work", [#"C:\x"#])
        work.name = "Job"

        #expect(work.command == "aiko-job")

        work.customCommand = "cc-work"
        #expect(work.command == "cc-work")
    }

    @Test
    func aLongNameMakesACommandWithinTheLimit() {
        let command = LaunchCommand.fromEnvironmentName(String(repeating: "a", count: 80))

        #expect(command.count <= LaunchCommand.maxLength)
        #expect(LaunchCommand.check(command, []) == .none)
    }

    @Test(arguments: [
        ("aiko-work", CommandProblem.none),
        ("cc_2", CommandProblem.none),
        ("", CommandProblem.empty),
        ("Aiko-Work", CommandProblem.badCharacters),
        ("aiko work", CommandProblem.badCharacters),
        ("-work", CommandProblem.badCharacters),
        ("claude", CommandProblem.reserved),
        ("aiko-personal", CommandProblem.taken),
    ] as [(String, CommandProblem)])
    func commandsAreChecked(command: String, problem: CommandProblem) {
        #expect(LaunchCommand.check(command, ["aiko-personal"]) == problem)
    }

    // ---- project folders ----

    @Test
    func aBoundFolderCoversEverythingInsideIt() {
        let settings = EnvironmentSettings([
            Self.env("Work", ".claude"),
            Self.env("Personal", ".claude-personal", #"D:\personal"#),
        ])

        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"D:\personal\aiko\src"#, settings: settings,
                userProfile: Self.home)?.name == "Personal")
        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"d:\PERSONAL"#, settings: settings,
                userProfile: Self.home)?.name == "Personal")
    }

    @Test
    func aFolderWhoseNameOnlyStartsTheSameIsNotInside() {
        let settings = EnvironmentSettings([
            Self.env("Work", ".claude"),
            Self.env("Personal", ".claude-personal", #"D:\work"#),
        ])

        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"D:\workshop"#, settings: settings,
                userProfile: Self.home)?.name == "Work")
    }

    @Test
    func theDeeperBindingWins() {
        let settings = EnvironmentSettings([
            Self.env("Work", ".claude", #"D:\work"#),
            Self.env("Personal", ".claude-personal", #"D:\work\side-project\"#),
        ])

        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"D:\work\side-project\app"#, settings: settings,
                userProfile: Self.home)?.name == "Personal")
        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"D:\work\client"#, settings: settings,
                userProfile: Self.home)?.name == "Work")
    }

    @Test
    func anUnboundFolderRunsInTheDefaultEnvironment() {
        let settings = EnvironmentSettings(
            [Self.env("Work", ".claude"), Self.env("Personal", ".claude-personal", #"D:\personal"#)],
            defaultEnvironment: "Personal")

        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"C:\temp"#, settings: settings,
                userProfile: Self.home)?.name == "Personal")
    }

    @Test
    func aDriveRootBindingCoversTheWholeDrive() {
        let settings = EnvironmentSettings([
            Self.env("Work", ".claude"),
            Self.env("Personal", ".claude-personal", #"E:\"#),
        ])

        #expect(
            ProjectBinding.environmentFor(
                .windows, workingDirectory: #"E:\anything"#, settings: settings,
                userProfile: Self.home)?.name == "Personal")
    }

    // ---- the file ----

    @Test
    func commandsFoldersAndTheDefaultSurviveASave() {
        var personal = Self.env("Personal", ".claude-personal", #"D:\personal"#)
        personal.customCommand = "cc"
        let settings = EnvironmentSettings(
            [Self.env("Work", ".claude"), personal], defaultEnvironment: "Personal")

        let again = EnvironmentSettings.fromJson(settings.toJson())

        #expect(again.defaultEnvironment == "Personal")
        #expect(again.environments[1].command == "cc")
        #expect(again.environments[1].projectFolders == [#"D:\personal"#])
        #expect(again.environments[0].customCommand == nil)
        #expect(settings.toJson().contains("\"schemaVersion\": \(EnvironmentSettings.currentSchema)"))
    }

    @Test
    func aFileOfSchema1ReadsWithNothingNewSet() {
        let old = """
            {
              "schemaVersion": 1,
              "ringEnvironment": "Personal",
              "environments": [
                { "name": "Personal", "configDirectories": ["C:\\\\Users\\\\someone\\\\.claude-personal"], "directMode": false },
                { "name": "Main", "configDirectories": ["C:\\\\Users\\\\someone\\\\.claude"], "directMode": true }
              ]
            }
            """

        let settings = EnvironmentSettings.fromJson(old)

        #expect(settings.environments.count == 2)
        #expect(settings.environments.allSatisfy { $0.projectFolders.isEmpty })
        #expect(settings.environments[0].command == "aiko-personal")
        #expect(settings.defaultEnvironment == nil)
        #expect(settings.environments[1].directMode)
    }
}
