import Testing

@testable import AikoKit

struct EnvironmentSettingsTests {
    static let twoEnvironments = """
        {
          "ringEnvironment": "Personal",
          "environments": [
            { "name": "Personal", "configDirectories": ["C:\\\\Users\\\\slayu\\\\.claude-personal"], "directMode": false },
            { "name": "Work", "configDirectories": ["C:\\\\Users\\\\slayu\\\\.claude"], "directMode": true }
          ]
        }
        """

    @Test
    func readsEnvironmentsWithTheirFoldersAndDirectMode() {
        let settings = EnvironmentSettings.fromJson(Self.twoEnvironments)

        #expect(settings.environments.count == 2)
        #expect(settings.environments[0].name == "Personal")
        #expect(!settings.environments[0].directMode)
        #expect(settings.environments[1].directMode)
        #expect(settings.environments[1].configDirectories == [#"C:\Users\slayu\.claude"#])
    }

    @Test
    func theRingShowsTheChosenEnvironmentAndTheDotTheOtherOne() {
        let settings = EnvironmentSettings.fromJson(Self.twoEnvironments)

        #expect(settings.ring?.name == "Personal")
        #expect(settings.dot?.name == "Work")
    }

    @Test
    func aClickSwapsTheRingAndTheDot() {
        let swapped = EnvironmentSettings.fromJson(Self.twoEnvironments).swapRing()

        #expect(swapped.ring?.name == "Work")
        #expect(swapped.dot?.name == "Personal")
    }

    @Test
    func withOneEnvironmentThereIsNoDotAndNothingToSwap() {
        let settings = EnvironmentSettings.fromJson(
            #"{ "environments": [ { "name": "Personal", "configDirectories": ["C:\\x"] } ] }"#)

        #expect(settings.ring?.name == "Personal")
        #expect(settings.dot == nil)
        #expect(settings.swapRing().ring?.name == "Personal")
    }

    @Test
    func anUnknownRingNameFallsBackToTheFirstEnvironment() {
        let settings = EnvironmentSettings.fromJson(
            """
            {
              "ringEnvironment": "Renamed away",
              "environments": [ { "name": "Personal", "configDirectories": ["C:\\\\x"] } ]
            }
            """)

        #expect(settings.ring?.name == "Personal")
    }

    @Test
    func environmentsWithoutANameOrAFolderAreDropped() {
        let settings = EnvironmentSettings.fromJson(
            """
            {
              "environments": [
                { "name": "", "configDirectories": ["C:\\\\x"] },
                { "name": "No folders", "configDirectories": [] },
                { "name": "Good", "configDirectories": ["C:\\\\x"] }
              ]
            }
            """)

        #expect(settings.environments.count == 1)
        #expect(settings.environments[0].name == "Good")
    }

    @Test
    func whatIsWrittenCanBeReadBack() {
        let settings = EnvironmentSettings.fromJson(Self.twoEnvironments).swapRing()

        let again = EnvironmentSettings.fromJson(settings.toJson())

        #expect(again.ring?.name == "Work")
        #expect(again.environments.count == 2)
        #expect(again.environments.first { $0.name == "Work" }?.directMode == true)
    }

    @Test(arguments: ["", "not json", "{}", #"{ "environments": [] }"#])
    func anythingUnusableMeansNoEnvironmentsYet(json: String) {
        let settings = EnvironmentSettings.fromJson(json)

        #expect(!settings.hasEnvironments)
        #expect(settings.ring == nil)
    }

    @Test
    func aFileFromBeforeThePersonaReadsWithThePersonaOff() {
        let settings = EnvironmentSettings.fromJson(Self.twoEnvironments)

        #expect(settings.environments.allSatisfy { !$0.persona })
    }

    @Test
    func thePersonaFlagSurvivesATripThroughTheFile() {
        var settings = EnvironmentSettings.fromJson(Self.twoEnvironments)
        settings.environments[0].persona = true

        let json = settings.toJson()
        let back = EnvironmentSettings.fromJson(json)

        #expect(back.environments[0].persona)
        #expect(!back.environments[1].persona)
        #expect(json.contains("\"schemaVersion\": \(EnvironmentSettings.currentSchema)"))
    }
}
