import Foundation

/// Where the update line on the General page stands.
public enum UpdateStage: Sendable, Equatable, CaseIterable {
    /// Nothing was asked in this run of the app yet.
    case notAsked

    case asking

    case upToDate

    case available

    /// The site did not answer or answered something we cannot read.
    case checkFailed

    case downloading

    /// Downloaded and checked; it takes its place after a restart.
    case ready

    /// The download did not finish. Trying again is safe.
    case downloadFailed

    /// This copy cannot install it, or the file was refused. Only the download page helps.
    case cannotInstall
}

/// The one thing the person can do at a stage.
public enum UpdateAction: Sendable, Equatable {
    /// Nothing to press: Aiko is busy.
    case none

    case check

    case install

    case restart

    case openDownloadPage
}

/// The update line of the General page: what it says is the app's, what it offers is decided here.
///
/// The line shows one state on the left and one action on the right, never a row of buttons to
/// choose from. The action is always the next step from that state.
///
/// The twin of UpdateBlock.cs.
public enum UpdateBlock {
    public static func actionFor(_ stage: UpdateStage) -> UpdateAction {
        switch stage {
        case .asking, .downloading: return .none
        case .available, .downloadFailed: return .install
        case .ready: return .restart
        case .cannotInstall: return .openDownloadPage
        case .notAsked, .upToDate, .checkFailed: return .check
        }
    }

    /// What the site's answer means for the line.
    public static func after(_ state: UpdateState) -> UpdateStage {
        switch state {
        case .available: return .available
        case .upToDate: return .upToDate

        // Never "up to date" when we do not know: that is the one answer that would keep every
        // copy quiet after a bad deploy.
        case .unknown: return .checkFailed
        }
    }

    /// The download in whole percent, kept inside 0 to 100 whatever the downloader reports.
    public static func percent(_ reported: Int) -> Int {
        min(max(reported, 0), 100)
    }
}
