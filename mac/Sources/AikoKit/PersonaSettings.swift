import Foundation

/// How loud Aiko is in a session (D-196). Guardrails hold on every level (D-195).
public enum Temperament: String, Sendable, Equatable, CaseIterable {
    case quiet = "Quiet"
    case normal = "Normal"
    case bright = "Bright"
    case musou = "Musou"
}

/// Aiko herself: one temperament for all environments (D-196). Whether she is on in
/// an environment is a flag of that environment, not stored here.
public struct PersonaSettings: Sendable, Equatable {
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public static let currentSchema = 1

    public var schemaVersion: Int
    public var temperament: Temperament

    public init(
        schemaVersion: Int = PersonaSettings.currentSchema,
        temperament: Temperament = .normal
    ) {
        self.schemaVersion = schemaVersion
        self.temperament = temperament
    }

    public static let `default` = PersonaSettings()

    /// Same rule as AppSettings: a file we cannot read, or a value from a newer Aiko, means the
    /// defaults for the whole file rather than half of it applied.
    public static func fromJson(_ json: String) -> PersonaSettings {
        if json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return `default`
        }

        guard let file = JsonNode.parse(json)?.objectValue else {
            return `default`
        }

        var settings = PersonaSettings()

        if let version = file["schemaVersion"] {
            guard case .number = version, let number = version.int64Value else { return `default` }
            settings.schemaVersion = Int(number)
        }
        if let value = file["temperament"] {
            guard let temperament = named(value, Temperament.allCases) else { return `default` }
            settings.temperament = temperament
        }

        return settings
    }

    public func toJson() -> String {
        let object = JsonObject([
            ("schemaVersion", .number(String(schemaVersion))),
            ("temperament", .string(temperament.rawValue)),
        ])
        return JsonNode.object(object).toJsonString(indented: true) + "\n"
    }

    /// A choice is written as a word and read back either way: JsonStringEnumConverter takes the
    /// name whatever its case, and the number of the value as well.
    private static func named<T: RawRepresentable & CaseIterable>(_ value: JsonNode, _ cases: [T]) -> T?
    where T.RawValue == String {
        if let text = value.stringValue {
            return cases.first { $0.rawValue.lowercased() == text.lowercased() }
        }
        if let number = value.int64Value, number >= 0, Int(number) < cases.count {
            return cases[Int(number)]
        }
        return nil
    }
}
