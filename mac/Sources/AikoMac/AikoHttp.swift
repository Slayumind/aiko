import AikoKit
import Foundation

/// The only place in Aiko that makes a URLSession.
///
/// D-005 said unwanted hosts are cut off, not just unused. A guard at the call site would not hold:
/// `URLSession.shared` is one line and the next one would be written the same way. What makes this
/// hold is the test beside it, which reads the source of the app and fails when a session is made
/// anywhere but here.
///
/// The twin of AikoHttp.cs, whose handler refuses the same addresses.
enum AikoHttp {
    /// Answers nil for an address Aiko has no business opening, so a mistake in our own code
    /// cannot become a request.
    static func get(_ address: String, timeout: TimeInterval) async -> (status: Int, body: String)? {
        guard let url = URL(string: address), AllowedHosts.allows(url) else {
            Log.write("blocked an address outside the allowed hosts")
            return nil
        }

        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = timeout
        configuration.timeoutIntervalForResource = timeout
        configuration.httpAdditionalHeaders = [
            "User-Agent": "Aiko/\(AppVersion.current())",
            "Accept": "application/json",
        ]

        let session = URLSession(configuration: configuration)
        defer { session.finishTasksAndInvalidate() }

        do {
            let (data, response) = try await session.data(from: url)
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            return (status, String(data: data, encoding: .utf8) ?? "")
        } catch {
            return nil
        }
    }
}

/// A random value made once on this computer, kept in a file beside the settings, and never sent
/// anywhere. Only a hash of it with today's date leaves the machine, and only when the count is on.
///
/// Deleting the file gives a fresh one, which is the whole undo anybody needs.
enum InstallId {
    private static var path: String { Store.folders.installIdFile }

    static func current() -> String {
        if let kept = try? String(contentsOfFile: path, encoding: .utf8) {
            let trimmed = kept.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }

        let made = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
        do {
            try FileManager.default.createDirectory(
                atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
            try made.write(toFile: path, atomically: true, encoding: .utf8)
            return made
        } catch {
            // Without a file there is no steady value, and a new one every time would count one
            // person many times. Better to send nothing.
            return ""
        }
    }

    static func forget() {
        try? FileManager.default.removeItem(atPath: path)
        try? FileManager.default.removeItem(atPath: Store.folders.reportedPeriodsFile)
        Log.write("privacy: the install id was reset")
    }
}

/// Asks slayumind.org which version is the latest, and, when the user has allowed it, says "one
/// copy of Aiko ran today" while it is there.
///
/// The number comes from the site and the files come from GitHub. Two switches, not one: update
/// checks and the count are separate consents, so with the count off the identifier and the flags
/// are simply not in the request and the server has nothing to write.
///
/// The twin of UpdateClient.cs and UpdateRun.cs. There is no Velopack on macOS, so nothing here
/// downloads or installs: the answer is a version number and a page to open.
enum UpdateRun {
    private static let endpoint = "https://slayumind.org/api/v1/aiko/version"

    static let releasesPage = "https://github.com/Slayumind/aiko/releases/latest"

    static func ask() async -> UpdateInfo {
        let request = build()
        // Short: nobody should wait for a version check, and a silent failure is fine here.
        guard let answer = await AikoHttp.get(request.address, timeout: 2.5) else {
            Log.write("update check: no answer from the site")
            return .unknown
        }

        guard answer.status == 200 else {
            Log.write("update check: the site answered \(answer.status)")
            return .unknown
        }

        if request.week != nil || request.month != nil {
            var reported = ReportedPeriods.read()
            reported.week = request.week ?? reported.week
            reported.month = request.month ?? reported.month
            ReportedPeriods.write(reported)
        }

        return UpdateInfo.fromJson(answer.body)
    }

    /// The day is UTC on both sides: the computer's own day would let one copy send two different
    /// identifiers inside one server day.
    private static func build() -> (address: String, week: String?, month: String?) {
        let version = escape(AppVersion.current())
        let settings = Store.settings()
        let plain = "\(endpoint)?v=\(version)"

        guard settings.sendStats else { return (plain, nil, nil) }

        let today = DateOnly.fromDaysSinceEpoch(
            Int((Date().timeIntervalSince1970 / 86400).rounded(.down)))
        let id = Heartbeat.dailyId(InstallId.current(), today)
        guard !id.isEmpty else { return (plain, nil, nil) }

        let reported = ReportedPeriods.read()
        let week = Heartbeat.firstThisWeek(reported.week, today) ? Heartbeat.weekKey(today) : nil
        let month = Heartbeat.firstThisMonth(reported.month, today) ? Heartbeat.monthKey(today) : nil
        let persona = Store.environments().environments.contains(where: \.persona) ? 1 : 0

        let address = plain
            + "&os=\(escape(AppVersion.system()))"
            + "&day=\(id)&p=\(persona)"
            + (week == nil ? "" : "&w=1")
            + (month == nil ? "" : "&m=1")

        return (address, week, month)
    }

    private static func escape(_ text: String) -> String {
        text.addingPercentEncoding(withAllowedCharacters: .alphanumerics) ?? text
    }
}

/// Which week and which month this copy has already reported. It never leaves the computer: what
/// travels is one bit, and the server adds the bits up. Written only after the site answered.
struct ReportedPeriods: Equatable {
    var week: String?
    var month: String?

    static func read() -> ReportedPeriods {
        guard let text = try? String(contentsOfFile: Store.folders.reportedPeriodsFile, encoding: .utf8),
              let object = JsonNode.parse(text)?.objectValue
        else {
            return ReportedPeriods()
        }

        return ReportedPeriods(week: object["week"]?.stringValue, month: object["month"]?.stringValue)
    }

    static func write(_ reported: ReportedPeriods) {
        let json = "{\"week\":\(quote(reported.week)),\"month\":\(quote(reported.month))}"
        try? FileManager.default.createDirectory(
            atPath: Store.folders.settingsFolder, withIntermediateDirectories: true)
        try? json.write(toFile: Store.folders.reportedPeriodsFile, atomically: true, encoding: .utf8)
    }

    private static func quote(_ value: String?) -> String {
        guard let value else { return "null" }
        return "\"\(value)\""
    }
}
