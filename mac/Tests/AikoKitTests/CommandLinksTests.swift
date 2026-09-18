import Testing

@testable import AikoKit

struct CommandLinksTests {
    @Test
    func anEmptyFolderGetsOneLinkPerEnvironment() {
        let plan = CommandLinks.plan(
            .windows,
            EnvironmentSettings([
                AikoEnvironment("Work", [#"C:\a"#]),
                AikoEnvironment("Personal", [#"C:\b"#]),
            ]),
            [#"C:\bin\claude.exe"#])

        #expect(plan.toAdd == ["aiko-work.exe", "aiko-personal.exe"])
        #expect(plan.toRemove.isEmpty)
    }

    @Test
    func aRenameSwapsTheOldLinkForANewOne() {
        let plan = CommandLinks.plan(
            .windows,
            EnvironmentSettings([AikoEnvironment("Job", [#"C:\a"#])]),
            [#"C:\bin\claude.exe"#, #"C:\bin\aiko-work.exe"#])

        #expect(plan.toAdd == ["aiko-job.exe"])
        #expect(plan.toRemove == ["aiko-work.exe"])
    }

    @Test
    func nothingToDoWhenTheFolderMatchesAndCaseDoesNotMatter() {
        let plan = CommandLinks.plan(
            .windows,
            EnvironmentSettings([AikoEnvironment("Work", [#"C:\a"#], customCommand: "cc")]),
            [#"C:\bin\CLAUDE.EXE"#, #"C:\bin\CC.exe"#, #"C:\bin\readme.txt"#, #"C:\bin\claude.exe.old"#])

        #expect(plan.isEmpty)
    }
}
