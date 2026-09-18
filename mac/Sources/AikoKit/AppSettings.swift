import Foundation

/// Where Aiko sits: down by the clock, or in a small island at the edge of the screen.
public enum AikoPlace: String, Sendable, Equatable, CaseIterable {
    case tray = "Tray"
    case island = "Island"
}

public enum AikoLanguage: String, Sendable, Equatable, CaseIterable {
    /// Follow the system. The other two are a deliberate choice by the user.
    case system = "System"
    case english = "English"
    case russian = "Russian"
}

/// What the settings window changes. Environments are not here: they live in their own file,
/// because the first run wizard writes them and this window does not.
public struct AppSettings: Sendable, Equatable {
    /// The shape of the file. Nothing reads it yet. It is written from the very first version
    /// because a version number added later says nothing about the files already on disk, and
    /// renaming a field would then have no safe way back.
    /// 2 added meetAikoShown. A file of schema 1 comes from 0.1, so it reads as not shown yet.
    /// 3 split the counting off the update check. A file of schema 2 reads as "not asked, not
    /// sending": the consent given to the old single switch covered a smaller thing than 0.2.1
    /// sends, so it is asked again rather than carried over.
    public static let currentSchema = 3

    public var schemaVersion: Int = currentSchema
    public var place: AikoPlace = .tray

    /// On by default: a tray app that has to be started by hand is no use (decided 2026-09-12).
    public var runAtStartup = true

    /// Off by default: checking asks slayumind.org and that is a connection the user agrees to.
    public var checkUpdates = false

    /// Off by default, and its own switch since 0.2.1. It used to ride on checkUpdates, so turning
    /// off the count meant turning off update checks with it, and there was no way to keep one
    /// without the other.
    public var sendStats = false

    /// Whether the privacy question has been answered at all. Without it, "off" cannot be told
    /// apart from "never asked", and somebody updating from 0.2.0 would never see the question.
    public var privacyAsked = false

    public var language: AikoLanguage = .system

    /// Games, video and presentations take the whole screen, and the island would sit on top.
    public var hideIslandInFullScreen = true

    /// Where the island was left. Kept as an edge and a share along it, so it survives a change
    /// of screen size.
    public var island: IslandPosition = .default

    /// The checklist opens once on "Meet Aiko" for people who update from 0.1 (D-200).
    public var meetAikoShown = false

    public init() {}

    public static let `default` = AppSettings()

    /// A settings file we cannot read is not a reason to stop: Aiko starts with the defaults.
    /// Putting the unreadable file aside before that happens is the store's job, so that the next
    /// save does not quietly write over choices somebody may still want back.
    public static func fromJson(_ json: String?) -> AppSettings {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let root = JsonNode.parse(json)?.objectValue else {
            return .default
        }

        var settings = AppSettings()

        // A word this version has never heard of makes the whole file fall back, the way
        // JsonSerializer throws on an unknown enum member. Better the defaults than half a file.
        if let text = root["place"]?.stringValue {
            guard let place = AikoPlace(rawValue: text) else { return .default }
            settings.place = place
        }
        if let text = root["language"]?.stringValue {
            guard let language = AikoLanguage(rawValue: text) else { return .default }
            settings.language = language
        }

        if let value = root["schemaVersion"]?.int64Value { settings.schemaVersion = Int(value) }
        if let value = root["runAtStartup"]?.boolValue { settings.runAtStartup = value }
        if let value = root["checkUpdates"]?.boolValue { settings.checkUpdates = value }
        if let value = root["sendStats"]?.boolValue { settings.sendStats = value }
        if let value = root["privacyAsked"]?.boolValue { settings.privacyAsked = value }
        if let value = root["hideIslandInFullScreen"]?.boolValue { settings.hideIslandInFullScreen = value }
        if let value = root["meetAikoShown"]?.boolValue { settings.meetAikoShown = value }

        if let object = root["island"]?.objectValue {
            guard let text = object["edge"]?.stringValue, let edge = screenEdge(text) else { return .default }
            settings.island = IslandPosition(edge, object["along"]?.doubleValue ?? 0)
        }

        return settings
    }

    /// The file is stamped with the schema that wrote it, not with the one it was read as.
    /// Without this the number sticks at whatever version first made the file: a settings.json
    /// created by 0.1 keeps saying 1 while holding fields from 0.2.1, and the one thing the
    /// number exists for — telling a future migration what shape the file is in — becomes a lie.
    public func toJson() -> String {
        let root = JsonObject([
            ("schemaVersion", .number(String(AppSettings.currentSchema))),
            ("place", .string(place.rawValue)),
            ("runAtStartup", .bool(runAtStartup)),
            ("checkUpdates", .bool(checkUpdates)),
            ("sendStats", .bool(sendStats)),
            ("privacyAsked", .bool(privacyAsked)),
            ("language", .string(language.rawValue)),
            ("hideIslandInFullScreen", .bool(hideIslandInFullScreen)),
            ("island", .object(JsonObject([
                ("edge", .string(name(island.edge))),
                ("along", .number(numberText(island.along))),
            ]))),
            ("meetAikoShown", .bool(meetAikoShown)),
        ])

        return JsonNode.object(root).toJsonString(indented: true) + "\n"
    }

    private static func screenEdge(_ text: String) -> ScreenEdge? {
        switch text {
        case "Top": return .top
        case "Bottom": return .bottom
        case "Left": return .left
        case "Right": return .right
        default: return nil
        }
    }

    private func name(_ edge: ScreenEdge) -> String {
        switch edge {
        case .top: return "Top"
        case .bottom: return "Bottom"
        case .left: return "Left"
        case .right: return "Right"
        }
    }

    /// The shortest text that reads back as the same double, the way System.Text.Json writes one.
    private func numberText(_ value: Double) -> String {
        if value == value.rounded() && abs(value) < 1e15 {
            return String(Int64(value))
        }
        return String(value)
    }
}
