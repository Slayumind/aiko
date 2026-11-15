import Foundation
import Security

/// The person's own OpenAI key, kept in the Keychain (D-313). The twin of VoiceKey.cs, which keeps
/// it in Windows Credential Manager.
///
/// Never in a file of Aiko, the log or the settings. It is read into memory right before a
/// recording goes to OpenAI and nowhere else; removing Aiko erases it.
enum VoiceKey {
    /// The name it has in Keychain Access, where a person can find and delete it by hand.
    private static let service = "Aiko: OpenAI key for voice input"
    private static let account = "openai"

    static var stored: Bool { load() != nil }

    /// Nil when no key is stored.
    static func load() -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var found: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &found) == errSecSuccess,
              let data = found as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    /// False when the Keychain did not take it.
    static func save(_ key: String) -> Bool {
        remove()
        let item: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            // This Mac only, and only while it is unlocked: the key never travels with a backup.
            kSecAttrAccessible as String: kSecAttrAccessibleWhenUnlockedThisDeviceOnly,
            kSecValueData as String: Data(key.utf8),
        ]
        return SecItemAdd(item as CFDictionary, nil) == errSecSuccess
    }

    static func remove() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(query as CFDictionary)
    }
}
