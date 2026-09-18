import Foundation

/// Which page of the settings window is open.
///
/// The keys are the same strings SettingsPanel.xaml.cs uses on Windows, so the tray, the checklist
/// and the self test door ask for a page by the same name on both systems.
public enum SettingsPage: Sendable, Equatable {
    /// An environment, named by its first config folder. The page follows the folder and not the
    /// name, because the name is what the page itself changes.
    case environment(String)

    case checklist
    case folders
    case personality
    case privacy
    case general

    public static let environmentPrefix = "env:"

    public var key: String {
        switch self {
        case .environment(let folder): return SettingsPage.environmentPrefix + folder
        case .checklist: return "setup"
        case .folders: return "folders"
        case .personality: return "personality"
        case .privacy: return "privacy"
        case .general: return "general"
        }
    }

    public static func fromKey(_ key: String) -> SettingsPage? {
        switch key {
        case "setup": return .checklist
        case "folders": return .folders
        case "personality": return .personality
        case "privacy": return .privacy
        case "general": return .general
        default:
            guard key.hasPrefix(environmentPrefix) else { return nil }
            let folder = String(key.dropFirst(environmentPrefix.count))
            return folder.isEmpty ? nil : .environment(folder)
        }
    }
}

/// One clickable row of the settings menu.
public struct SettingsNavItem: Sendable, Equatable {
    public let page: SettingsPage
    public let title: String

    /// The small line under the title: the plan and the command, or how many folders are bound.
    public let note: String?

    public init(page: SettingsPage, title: String, note: String?) {
        self.page = page
        self.title = title
        self.note = note
    }
}

public enum SettingsNavEntry: Sendable, Equatable {
    case label(String)
    case item(SettingsNavItem)

    /// The empty place of environment 2, drawn with a dashed edge.
    case addSecondEnvironment

    case separator
}

/// The menu of the settings window (D-177). The twin of SettingsPanel.BuildNav on Windows.
///
/// The list is decided here so a window does not have to exist to check it: the order of the
/// environments, when the checklist takes the place of the "add a second one" slot, and every
/// small line under a title.
public enum SettingsNav {
    /// Environment 1 first, whatever order the file has: it is always the one in .claude.
    public static func ordered(
        _ settings: EnvironmentSettings, _ platform: PlatformConventions, _ userProfile: String
    ) -> [AikoEnvironment] {
        guard let first = settings.first(platform, userProfile) else {
            return settings.environments
        }

        return [first] + settings.environments.filter { $0 != first }
    }

    /// `plans` is the plan label of each config folder, read from .claude.json by the app.
    public static func rows(
        environments: EnvironmentSettings,
        plans: [String: String],
        sendsStats: Bool,
        checklistOpen: Bool,
        platform: PlatformConventions,
        userProfile: String
    ) -> [SettingsNavEntry] {
        var rows: [SettingsNavEntry] = [.label(Strings.sectionEnvironments)]

        for environment in ordered(environments, platform, userProfile) {
            let folder = environment.configDirectories.first ?? ""
            let plan = plans[folder] ?? ""
            rows.append(.item(SettingsNavItem(
                page: .environment(folder),
                title: environment.name,
                note: plan.isEmpty ? environment.command : "\(plan) · \(environment.command)")))
        }

        if checklistOpen || !environments.hasEnvironments {
            rows.append(.item(SettingsNavItem(page: .checklist, title: Strings.checklistTitle, note: nil)))
        } else if environments.environments.count < EnvironmentSettings.maxEnvironments {
            rows.append(.addSecondEnvironment)
        }

        rows.append(.separator)

        let bound = EnvironmentEdits.bindings(environments).count
        rows.append(.item(SettingsNavItem(
            page: .folders,
            title: Strings.itemFolders,
            note: bound > 0 ? Strings.format(Strings.navBound, bound) : nil)))

        if environments.hasEnvironments {
            let talking = environments.environments.filter(\.persona).count
            rows.append(.item(SettingsNavItem(
                page: .personality,
                title: Strings.navPersonality,
                note: talking > 0 ? Strings.format(Strings.navPersonaOn, talking) : Strings.navPersonaOff)))
        }

        rows.append(.item(SettingsNavItem(
            page: .privacy,
            title: Strings.navPrivacy,
            note: sendsStats ? Strings.navPrivacyStatsOn : Strings.navPrivacyStatsOff)))

        rows.append(.item(SettingsNavItem(page: .general, title: Strings.navGeneral, note: nil)))

        return rows
    }

    /// The page to open when nobody asked for one: the first environment, or the checklist while
    /// nothing is set up.
    public static func firstPage(
        _ settings: EnvironmentSettings, _ platform: PlatformConventions, _ userProfile: String
    ) -> SettingsPage {
        guard let folder = ordered(settings, platform, userProfile).first?.configDirectories.first else {
            return .checklist
        }

        return .environment(folder)
    }
}

/// The size of the settings window. The twin of the numbers in SettingsPanel.xaml.
public enum SettingsLayout {
    public static let width = 720.0
    public static let headerHeight = 44.0
    public static let navWidth = 188.0
    public static let radius = 14.0

    public static let minBodyHeight = 420.0
    public static let maxBodyHeight = 600.0

    /// Room left above and below, so a small screen still shows the header and the window edges.
    public static let roomOnScreen = 160.0

    public static func bodyHeight(screenHeight: Double) -> Double {
        min(max(screenHeight - roomOnScreen, minBodyHeight), maxBodyHeight)
    }
}
