import Testing

@testable import AikoKit

/// The changes the settings window makes, each saved the moment it is made.
struct EnvironmentEditsTests {
    static let home = #"C:\Users\someone"#

    private static func env(_ name: String, _ folder: String, _ projects: String...) -> AikoEnvironment {
        AikoEnvironment(name, [PlatformConventions.windows.join(home, folder)], projectFolders: projects)
    }

    /// The author's machine on 14 September: Aiko in .claude, Work in .claude-work.
    private func twoEnvironments() -> EnvironmentSettings {
        var aiko = Self.env("Aiko", ".claude", #"C:\Users\someone\Desktop\personal projects"#)
        aiko.customCommand = "aiko"

        return EnvironmentSettings(
            [aiko, Self.env("Work", ".claude-work", #"C:\Users\someone\Desktop\kodland"#)],
            ringEnvironment: "Aiko",
            defaultEnvironment: "Work")
    }

    // ---- names and commands ----

    @Test
    func aRenameKeepsAnInstalledCommandAndSuggestsTheNewOne() {
        let renamed = EnvironmentEdits.rename(
            twoEnvironments(), "Work", "Work · Kodland", commandsInstalled: true)
        let work = renamed.settings.environments[1]

        #expect(work.name == "Work · Kodland")
        #expect(work.command == "aiko-work")
        #expect(renamed.suggestedCommand == "aiko-work-kodland")
    }

    @Test
    func beforeTheCommandsAreInstalledTheCommandFollowsTheName() {
        let renamed = EnvironmentEdits.rename(twoEnvironments(), "Work", "Job", commandsInstalled: false)

        #expect(renamed.settings.environments[1].command == "aiko-job")
        #expect(renamed.suggestedCommand == nil)
    }

    @Test
    func aRenameThatMakesTheSameCommandSuggestsNothing() {
        let renamed = EnvironmentEdits.rename(twoEnvironments(), "Work", "WORK", commandsInstalled: true)

        #expect(renamed.settings.environments[1].command == "aiko-work")
        #expect(renamed.settings.environments[1].customCommand == nil)
        #expect(renamed.suggestedCommand == nil)
    }

    @Test
    func theRingAndTheDefaultFollowARenamedEnvironment() {
        var settings = EnvironmentEdits.rename(twoEnvironments(), "Work", "Job", commandsInstalled: true).settings
        settings = EnvironmentEdits.rename(settings, "Aiko", "Personal", commandsInstalled: true).settings

        #expect(settings.ringEnvironment == "Personal")
        #expect(settings.defaultEnvironment == "Job")
        #expect(settings.environments[0].command == "aiko")
    }

    @Test(arguments: [
        ("", NameProblem.empty),
        ("   ", NameProblem.empty),
        ("aiko", NameProblem.taken),
        ("Work", NameProblem.none),
        ("Kodland", NameProblem.none),
    ] as [(String, NameProblem)])
    func namesAreChecked(wanted: String, problem: NameProblem) {
        #expect(EnvironmentEdits.checkName(twoEnvironments(), "Work", wanted) == problem)
    }

    @Test
    func aNameThatDoesNotPassTheCheckChangesNothing() {
        let settings = twoEnvironments()

        #expect(EnvironmentEdits.rename(settings, "Work", "AIKO", commandsInstalled: true).settings == settings)
        #expect(
            EnvironmentEdits.rename(
                settings, "Work", String(repeating: "a", count: 41), commandsInstalled: true).settings == settings)
    }

    @Test
    func takingTheSuggestionSetsTheCommandToTheNameAgain() {
        let renamed = EnvironmentEdits.rename(twoEnvironments(), "Work", "Kodland", commandsInstalled: true)
        let settings = EnvironmentEdits.setCommand(renamed.settings, "Kodland", renamed.suggestedCommand!)

        #expect(settings.environments[1].command == "aiko-kodland")
        #expect(settings.environments[1].customCommand == nil)
    }

    @Test
    func aCommandAnotherEnvironmentUsesIsRefused() {
        let settings = twoEnvironments()

        #expect(EnvironmentEdits.checkCommand(settings, "Work", "aiko") == .taken)
        #expect(EnvironmentEdits.setCommand(settings, "Work", "aiko") == settings)
        #expect(EnvironmentEdits.checkCommand(settings, "Aiko", "aiko") == .none)
    }

    @Test
    func theChecklistStartsFromTheCommandInUseOnceCommandsAreInstalled() {
        var work = AikoEnvironment("Work · Kodland", [#"C:\x"#])

        #expect(EnvironmentEdits.startingCommand(work, commandsInstalled: true) == "aiko-work-kodland")
        #expect(EnvironmentEdits.startingCommand(work, commandsInstalled: false) == nil)

        work.customCommand = "cc"
        #expect(EnvironmentEdits.startingCommand(work, commandsInstalled: false) == "cc")
        #expect(EnvironmentEdits.startingCommand(nil, commandsInstalled: true) == nil)
    }

    // ---- folders ----

    @Test
    func bindingsAreOneListSortedByFolder() {
        #expect(
            EnvironmentEdits.bindings(twoEnvironments()) == [
                FolderBinding(#"C:\Users\someone\Desktop\kodland"#, "Work"),
                FolderBinding(#"C:\Users\someone\Desktop\personal projects"#, "Aiko"),
            ])
    }

    @Test
    func bindingAFolderToTheOtherEnvironmentMovesIt() {
        // The live bug: the environment picked for a folder never reached the folder.
        let settings = EnvironmentEdits.bind(
            twoEnvironments(), #"c:\users\someone\desktop\KODLAND\"#, "Aiko")

        #expect(settings.environments[1].projectFolders.isEmpty)
        #expect(settings.environments[0].projectFolders.count == 2)
        #expect(
            EnvironmentEdits.bindings(settings)
                .filter { $0.environment == "Aiko" && $0.folder.hasSuffix(#"KODLAND\"#) }.count == 1)
    }

    @Test
    func bindingToAnUnknownEnvironmentChangesNothing() {
        let settings = twoEnvironments()

        #expect(EnvironmentEdits.bind(settings, #"D:\x"#, "Gone") == settings)
    }

    @Test
    func unbindingTakesTheFolderAway() {
        let settings = EnvironmentEdits.unbind(twoEnvironments(), #"C:\Users\someone\Desktop\kodland"#)

        #expect(EnvironmentEdits.bindings(settings).count == 1)
    }

    @Test
    func theDefaultIsChangedOnlyToAnEnvironmentThatExists() {
        let settings = twoEnvironments()

        #expect(EnvironmentEdits.setDefault(settings, "Aiko").defaultEnvironment == "Aiko")
        #expect(EnvironmentEdits.setDefault(settings, "Gone") == settings)
    }

    @Test
    func aNewFolderGoesToTheEnvironmentThatIsNotTheDefault() {
        var settings = twoEnvironments()

        #expect(EnvironmentEdits.forNewBinding(.windows, settings, Self.home)?.name == "Aiko")

        settings.defaultEnvironment = nil
        #expect(EnvironmentEdits.forNewBinding(.windows, settings, Self.home)?.name == "Work")
    }

    // ---- direct mode and removing ----

    @Test
    func directModeChangesForOneEnvironment() {
        let settings = EnvironmentEdits.setDirectMode(twoEnvironments(), "Work", true)

        #expect(settings.environments[1].directMode)
        #expect(!settings.environments[0].directMode)
    }

    @Test
    func thePersonaIsSwitchedInOneEnvironmentAndKeptThroughOtherEdits() {
        let settings = EnvironmentEdits.setPersona(twoEnvironments(), "Work", true)
        let renamed = EnvironmentEdits.rename(settings, "Work", "Kodland", commandsInstalled: false).settings

        #expect(settings.environments[1].persona)
        #expect(!settings.environments[0].persona)
        #expect(renamed.environments[1].persona)
    }

    @Test
    func switchingThePersonaOfAnUnknownEnvironmentChangesNothing() {
        let settings = twoEnvironments()

        #expect(EnvironmentEdits.setPersona(settings, "Nobody", true) == settings)
    }

    @Test
    func theEnvironmentInClaudeCannotBeRemoved() {
        let settings = twoEnvironments()

        #expect(!EnvironmentEdits.canRemove(.windows, settings, "Aiko", Self.home))
        #expect(EnvironmentEdits.remove(.windows, settings, "Aiko", Self.home) == settings)
    }

    @Test
    func removingAnEnvironmentTakesItsBindingsAndTheDefaultWithIt() {
        var before = twoEnvironments()
        before.ringEnvironment = "Work"

        let settings = EnvironmentEdits.remove(.windows, before, "Work", Self.home)

        #expect(settings.environments.map(\.name) == ["Aiko"])
        #expect(settings.defaultEnvironment == nil)
        #expect(settings.ringEnvironment == nil)
        #expect(settings.defaultEnvironmentIn(.windows, Self.home)?.name == "Aiko")
        #expect(EnvironmentEdits.bindings(settings).count == 1)
    }
}
