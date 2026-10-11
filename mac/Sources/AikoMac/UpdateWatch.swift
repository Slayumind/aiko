import AikoKit
import AppKit

/// Asks slayumind.org once a day whether a newer Aiko exists, and says that one copy ran — each
/// when the user has said it may. Two switches, one request (D-228). The twin of UpdateWatch.cs.
///
/// Until 0.4.0 the Mac asked only when the button in settings was pressed. The switches promise
/// "once a day", and a count that waits for a button counts almost nobody.
///
/// Nothing pops up. The answer goes into the tooltip of the icon or the island and into the
/// settings window.
@MainActor
final class UpdateWatch {
    /// Not at once: the first minutes after login belong to the machine, not to us.
    private static let firstCheck: TimeInterval = 2 * 60

    private static let betweenChecks: TimeInterval = 24 * 60 * 60

    /// A plain timer does not count the hours a MacBook sleeps, so a day could take a week. The
    /// system keeps this one, runs it when the machine is awake and picks a quiet moment for it.
    private let daily = NSBackgroundActivityScheduler(identifier: "org.slayumind.aiko.update-check")

    private var first: Timer?
    private var asking = false
    private var stopped = false

    /// The newer version, once one is known. Nil while Aiko is the latest, while the answer has
    /// not come yet, and whenever the check could not be made.
    private(set) var newerVersion: String?

    var found: (() -> Void)?

    init() {
        first = Timer.scheduledTimer(withTimeInterval: Self.firstCheck, repeats: false) { _ in
            MainActor.assumeIsolated { self.onTurn() }
        }

        daily.repeats = true
        daily.interval = Self.betweenChecks
        daily.tolerance = 60 * 60
        daily.qualityOfService = .utility
        daily.schedule { [weak self] finished in
            // The request is short and has its own timeout, so the system is not kept waiting.
            Task { @MainActor in self?.onTurn() }
            finished(.finished)
        }
    }

    func stop() {
        stopped = true
        first?.invalidate()
        first = nil
        daily.invalidate()
        found = nil
    }

    private func onTurn() {
        first = nil
        Task { await check() }
    }

    private func check() async {
        let settings = Store.settings()

        // Either switch is a reason to make the request, and the request is the same one. With
        // update checks off and the count on, the version in the answer is simply not read: the
        // switch promises that Aiko does not go looking for a new version, and it does not.
        guard !stopped, !asking, settings.checkUpdates || settings.sendStats else { return }

        asking = true
        defer { asking = false }

        let info = await UpdateRun.ask()

        guard !stopped, settings.checkUpdates else { return }

        // Only a definite "there is a newer one" is worth showing. "Could not check" says
        // nothing about the version, and saying nothing is the honest answer.
        let newer = info.compareWith(AppVersion.current()) == .available ? info.latest : nil

        guard newer != newerVersion else { return }

        newerVersion = newer
        Log.write(newer.map { "update check: \($0) is out" } ?? "update check: nothing newer")
        found?()
    }
}
