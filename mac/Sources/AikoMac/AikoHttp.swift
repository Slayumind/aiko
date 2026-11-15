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

    /// A whole file: the parts of an update. Nil for anything but a plain 200, so a GitHub error
    /// page is never mistaken for a zip.
    ///
    /// `progress` hears the download in whole percent, on whatever thread URLSession reports from.
    static func bytes(
        _ address: String, timeout: TimeInterval, progress: (@Sendable (Int) -> Void)? = nil
    ) async -> Data? {
        guard let url = URL(string: address), AllowedHosts.allows(url) else {
            Log.write("blocked an address outside the allowed hosts")
            return nil
        }

        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = timeout
        configuration.timeoutIntervalForResource = timeout
        configuration.httpAdditionalHeaders = ["User-Agent": "Aiko/\(AppVersion.current())"]

        let session = URLSession(
            configuration: configuration, delegate: OnlyAllowedHops(progress: progress),
            delegateQueue: nil)
        defer { session.finishTasksAndInvalidate() }

        do {
            let (data, response) = try await session.data(from: url)
            guard (response as? HTTPURLResponse)?.statusCode == 200 else { return nil }
            return data
        } catch {
            return nil
        }
    }
}

extension AikoHttp {
    /// A large file in pieces, as they come: the speech model of voice input (D-316), 465 MB, which
    /// should not sit in memory whole. `from` asks for the rest of a file that stopped half way.
    /// `receive` hears the status and each piece; a piece it throws on stops the download. Answers
    /// the status, or nil when the address is not allowed, the line broke or it was stopped.
    static func stream(
        _ address: String, from offset: Int64, timeout: TimeInterval,
        receive: @escaping @Sendable (Int, Data) throws -> Void
    ) async -> Int? {
        guard let url = URL(string: address), AllowedHosts.allows(url) else {
            Log.write("blocked an address outside the allowed hosts")
            return nil
        }

        var request = URLRequest(url: url)
        if offset > 0 {
            request.setValue("bytes=\(offset)-", forHTTPHeaderField: "Range")
        }
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 60
        configuration.timeoutIntervalForResource = timeout
        configuration.httpAdditionalHeaders = ["User-Agent": "Aiko/\(AppVersion.current())"]

        let pieces = Pieces(receive: receive)
        let session = URLSession(configuration: configuration, delegate: pieces, delegateQueue: nil)
        defer { session.finishTasksAndInvalidate() }
        return await withTaskCancellationHandler {
            await withCheckedContinuation { done in
                pieces.done = done
                session.dataTask(with: request).resume()
            }
        } onCancel: {
            session.invalidateAndCancel()
        }
    }
}

extension AikoHttp {
    /// A WebSocket with the person's key in its header: voice input through OpenAI (D-317). Answers
    /// the open socket, or the HTTP status OpenAI refused the handshake with (0 when it did not
    /// answer at all). The twin of AikoHttp.Socket on Windows.
    static func socket(_ address: String, bearer: String) async -> (socket: CloudSocket?, refused: Int) {
        guard let url = URL(string: address), AllowedHosts.allows(url) else {
            Log.write("blocked an address outside the allowed hosts")
            return (nil, 0)
        }

        var request = URLRequest(url: url)
        request.setValue("Bearer " + bearer, forHTTPHeaderField: "Authorization")
        request.setValue("Aiko/\(AppVersion.current())", forHTTPHeaderField: "User-Agent")
        let opening = Opening()
        let session = URLSession(configuration: .ephemeral, delegate: opening, delegateQueue: nil)
        let task = session.webSocketTask(with: request)
        let opened = await withCheckedContinuation { done in
            opening.done = done
            task.resume()
        }
        guard opened else {
            let status = (task.response as? HTTPURLResponse)?.statusCode ?? 0
            session.invalidateAndCancel()
            return (nil, status)
        }
        return (CloudSocket(session: session, task: task), 0)
    }
}

/// An open WebSocket: text in and out, then closed.
final class CloudSocket: @unchecked Sendable {
    private let session: URLSession
    private let task: URLSessionWebSocketTask

    fileprivate init(session: URLSession, task: URLSessionWebSocketTask) {
        self.session = session
        self.task = task
    }

    func send(_ text: String) async throws {
        try await task.send(.string(text))
    }

    /// The next message, or nil when the socket closed.
    func receive() async throws -> Data? {
        switch try await task.receive() {
        case .string(let text): return Data(text.utf8)
        case .data(let data): return data
        @unknown default: return nil
        }
    }

    func close() {
        task.cancel(with: .normalClosure, reason: nil)
        session.invalidateAndCancel()
    }
}

/// Tells whether the handshake of a socket went through. A redirect is never followed.
private final class Opening: NSObject, URLSessionWebSocketDelegate, @unchecked Sendable {
    var done: CheckedContinuation<Bool, Never>?

    func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didOpenWithProtocol protocol: String?) {
        done?.resume(returning: true)
        done = nil
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        done?.resume(returning: false)
        done = nil
    }

    func urlSession(
        _ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
        newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void
    ) {
        completionHandler(nil)
    }
}

/// The pieces of one streamed download, and the same check of every redirect as OnlyAllowedHops.
private final class Pieces: NSObject, URLSessionDataDelegate, @unchecked Sendable {
    private let receive: @Sendable (Int, Data) throws -> Void
    private var status = 0
    private var failed = false
    var done: CheckedContinuation<Int?, Never>?

    init(receive: @escaping @Sendable (Int, Data) throws -> Void) {
        self.receive = receive
    }

    func urlSession(
        _ session: URLSession, dataTask: URLSessionDataTask, didReceive response: URLResponse,
        completionHandler: @escaping (URLSession.ResponseDisposition) -> Void
    ) {
        status = (response as? HTTPURLResponse)?.statusCode ?? 0
        completionHandler(.allow)
    }

    func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive data: Data) {
        do {
            try receive(status, data)
        } catch {
            failed = true
            dataTask.cancel()
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        done?.resume(returning: error == nil && !failed ? status : nil)
        done = nil
    }

    func urlSession(
        _ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
        newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void
    ) {
        guard AllowedHosts.allows(request.url) else {
            Log.write("a redirect went outside the allowed hosts and was not followed")
            completionHandler(nil)
            return
        }
        completionHandler(request)
    }
}

/// Checks the address of every redirect before it is followed.
///
/// Without this the list of allowed hosts would only cover the first request: GitHub answers a
/// download with a redirect to its file store, and URLSession follows a redirect anywhere.
///
/// It also watches how much of the file has arrived, when somebody asked to hear it. The session
/// counts the bytes anyway, so this adds no second way of downloading.
final class OnlyAllowedHops: NSObject, URLSessionTaskDelegate, @unchecked Sendable {
    private let progress: (@Sendable (Int) -> Void)?
    private var watching: NSKeyValueObservation?

    init(progress: (@Sendable (Int) -> Void)? = nil) {
        self.progress = progress
    }

    func urlSession(_ session: URLSession, didCreateTask task: URLSessionTask) {
        guard let progress else { return }

        watching = task.observe(\.countOfBytesReceived) { task, _ in
            // The size is unknown until the file store answers, and stays unknown when it does
            // not say. No number is better than a made-up one.
            let whole = task.countOfBytesExpectedToReceive
            guard whole > 0 else { return }
            progress(Int(task.countOfBytesReceived * 100 / whole))
        }
    }

    func urlSession(
        _ session: URLSession,
        task: URLSessionTask,
        willPerformHTTPRedirection response: HTTPURLResponse,
        newRequest request: URLRequest,
        completionHandler: @escaping (URLRequest?) -> Void
    ) {
        guard AllowedHosts.allows(request.url) else {
            Log.write("a redirect went outside the allowed hosts and was not followed")
            completionHandler(nil)
            return
        }

        completionHandler(request)
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

    /// When the site last answered in this run of the app. The General page shows it, so "up to
    /// date" comes with "as of when".
    private(set) static nonisolated(unsafe) var lastAnswered: Date?

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

        lastAnswered = Date()

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
    /// The query itself is CountQuery of the core, the same rule and rows as on Windows.
    private static func build() -> (address: String, week: String?, month: String?) {
        let version = AppVersion.current()
        let plain = endpoint + CountQuery.query(
            version: version, system: "", day: nil, persona: false, firstOfWeek: false, firstOfMonth: false)

        guard Store.settings().sendStats else { return (plain, nil, nil) }

        let today = DateOnly.fromDaysSinceEpoch(
            Int((Date().timeIntervalSince1970 / 86400).rounded(.down)))
        let id = Heartbeat.dailyId(InstallId.current(), today)
        guard !id.isEmpty else { return (plain, nil, nil) }

        let reported = ReportedPeriods.read()
        let week = Heartbeat.firstThisWeek(reported.week, today) ? Heartbeat.weekKey(today) : nil
        let month = Heartbeat.firstThisMonth(reported.month, today) ? Heartbeat.monthKey(today) : nil

        let address = endpoint + CountQuery.query(
            version: version,
            system: AppVersion.system(),
            day: id,
            persona: Store.environments().environments.contains(where: \.persona),
            firstOfWeek: week != nil,
            firstOfMonth: month != nil)

        return (address, week, month)
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
