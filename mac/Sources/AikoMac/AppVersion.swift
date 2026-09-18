import Foundation
import ServiceManagement

/// The version of the running Aiko.
///
/// Two places compare it, the settings window and the update check, and they have to agree:
/// comparing two differently shaped version strings is how a copy decides it is up to date when it
/// is not. So `current` is always three numbers, and the commit is only for people to read.
///
/// The twin of AppVersion.cs. Windows reads the assembly; here the numbers come from Info.plist,
/// which Scripts/make-app.sh fills from the same Directory.Build.props.
enum AppVersion {
    static func current() -> String {
        let text = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? ""
        let parts = text.split(separator: ".").prefix(3).map(String.init)
        return parts.count == 3 ? parts.joined(separator: ".") : (text.isEmpty ? "unknown" : text)
    }

    /// Three numbers and the short commit the build came from, like 0.3.0+42cb34b. Between releases
    /// the version does not change (D-236), so this is what tells two builds apart in a bug report.
    static func withCommit() -> String {
        guard let commit = Bundle.main.infoDictionary?["AikoCommit"] as? String, !commit.isEmpty else {
            return current()
        }

        return "\(current())+\(commit.prefix(7))"
    }

    /// For the diagnostics, in the same shape as `Windows 10.0.26200` on the other system.
    static func system() -> String {
        let version = ProcessInfo.processInfo.operatingSystemVersion
        return "macOS \(version.majorVersion).\(version.minorVersion).\(version.patchVersion)"
    }
}

/// Starting with the machine. Windows writes one value under the user's Run key; macOS has a
/// service of its own for it, which needs no helper app and no administrator rights.
///
/// It only works for an app in a bundle. A binary run straight from .build has none, and then the
/// switch says off and saying yes writes nothing but a line in the log.
enum LoginItem {
    static func isEnabled() -> Bool {
        SMAppService.mainApp.status == .enabled
    }

    static func set(_ enabled: Bool) {
        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
        } catch {
            Log.write("could not change the login item: \(error.localizedDescription)")
        }
    }
}
