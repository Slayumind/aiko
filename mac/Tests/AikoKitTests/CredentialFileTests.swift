import Foundation
import Testing

@testable import AikoKit

struct CredentialFileTests {
    static let token = "sk-ant-oat01-example-token-value-for-tests-only"

    static let valid = """
        {
          "claudeAiOauth": {
            "accessToken": "\(token)",
            "refreshToken": "sk-ant-ort01-refresh-we-never-touch",
            "expiresAt": 1789200000000,
            "subscriptionType": "max"
          },
          "mcpOAuth": { "some-server": { "accessToken": "not ours" } }
        }
        """

    @Test
    func readsTheTokenTheExpiryAndThePlan() {
        let credential = CredentialFile.parse(Self.valid)

        #expect(credential?.accessToken == Self.token)
        #expect(credential?.expiresAt == Date(timeIntervalSince1970: 1_789_200_000))
        #expect(credential?.subscriptionType == "max")
    }

    @Test
    func knowsWhenTheTokenHasExpired() {
        let credential = CredentialFile.parse(Self.valid)!

        #expect(credential.isExpiredAt(credential.expiresAt.adding(seconds: 1)))
        #expect(!credential.isExpiredAt(credential.expiresAt.adding(seconds: -1)))
    }

    @Test
    func theTokenNeverAppearsInText() {
        let text = String(describing: CredentialFile.parse(Self.valid)!)

        #expect(!text.contains(Self.token))
        #expect(text.contains("hidden"))
    }

    @Test
    func aMissingPlanReadsAsUnknown() {
        let json = #"{ "claudeAiOauth": { "accessToken": "abc", "expiresAt": 1789200000000 } }"#

        #expect(CredentialFile.parse(json)?.subscriptionType == "unknown")
    }

    @Test(arguments: [
        "",
        "not json",
        "{}",
        #"{ "claudeAiOauth": {} }"#,
        #"{ "claudeAiOauth": { "accessToken": "", "expiresAt": 1 } }"#,
        #"{ "claudeAiOauth": { "accessToken": "abc" } }"#,
    ])
    func anythingElseIsSimplyNotACredentialFile(json: String) {
        #expect(CredentialFile.parse(json) == nil)
    }
}
