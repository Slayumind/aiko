import AikoKit
import AppKit

/// Turns session and limit changes into a face for two seconds (D-199, D-211). Which face and for
/// how long is decided in AikoKit's TrayMood; this only listens and keeps the one short timer.
///
/// The twin of Tray/TrayMoodPlayer.cs. With the persona off everywhere nothing is watched at all,
/// and the timer runs only while a face is showing.
@MainActor
final class MoodPlay {
    private var activity: ActivityWatch?
    private var timer: Timer?

    /// The names of the environments whose sessions may bring a face, and the snapshot names of
    /// their folders. Kept lower cased, the way the Windows set ignores case.
    private var talking: Set<String> = []
    private var percents: [String: Int] = [:]
    private var showing: FaceMoment?

    /// The face to draw, or nil for the rings.
    var faceChanged: ((AikoFace?) -> Void)?

    /// Called at startup and after every settings change.
    func follow(_ environments: EnvironmentSettings) {
        talking = Set(environments.environments
            .filter(\.persona)
            .flatMap { environment in
                environment.configDirectories.map(SnapshotName.forConfigDirectory) + [environment.name]
            }
            .map { $0.lowercased() })

        if TrayMood.hasFace(environments), activity == nil {
            let watch = ActivityWatch(folder: Store.folders.activityFolder)
            watch.changed = { [weak self] before, after in self?.onActivity(before, after) }
            activity = watch
        } else if !TrayMood.hasFace(environments), activity != nil {
            activity?.stop()
            activity = nil
            percents = [:]
            hide()
        }
    }

    /// Called with every new set of limits, named by environment.
    func onLimits(_ snapshots: [LimitSnapshot]) {
        guard activity != nil else { return }

        let now = Date()
        for snapshot in snapshots where talking.contains(snapshot.environment.lowercased()) {
            let key = snapshot.environment.lowercased()
            let percent = TrayMood.highestPercent(snapshot, now)
            let before = percents[key]
            percents[key] = percent

            if let face = TrayMood.forLimit(before, percent) {
                show(face)
            }
        }
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        activity?.stop()
        activity = nil
        faceChanged = nil
    }

    var face: AikoFace? { showing?.face }

    private func onActivity(_ before: ActivityRecord?, _ after: ActivityRecord) {
        guard talking.contains(after.environment.lowercased()),
              let face = TrayMood.forActivity(before, after, Date()) else {
            return
        }

        show(face)
    }

    private func show(_ face: AikoFace) {
        let now = Date()
        let next = TrayMood.next(showing, face, now)
        guard next != showing else { return }

        let isNew = next.face != showing?.face
        showing = next

        // Started again, so a new face gets its full two seconds. The face needs a moment to
        // arrive, and the two seconds count from when it is there.
        timer?.invalidate()
        timer = Timer.scheduledTimer(
            withTimeInterval: TrayMood.showFor + TrayFaceMotion.arrival, repeats: false
        ) { _ in
            MainActor.assumeIsolated { self.hide() }
        }

        if isNew {
            Log.write("face: \(face)")
            faceChanged?(face)
        }
    }

    private func hide() {
        timer?.invalidate()
        timer = nil
        guard showing != nil else { return }

        showing = nil
        faceChanged?(nil)
    }
}
