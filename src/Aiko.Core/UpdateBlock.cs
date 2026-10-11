namespace Aiko.Core;

/// Where the update line on the General page stands.
public enum UpdateStage
{
    /// Nothing was asked in this run of the app yet.
    NotAsked,

    Asking,

    UpToDate,

    Available,

    /// The site did not answer or answered something we cannot read.
    CheckFailed,

    Downloading,

    /// Downloaded and checked; it takes its place after a restart.
    Ready,

    /// The download did not finish. Trying again is safe.
    DownloadFailed,

    /// This copy cannot install it, or the file was refused. Only the download page helps.
    CannotInstall,
}

/// The one thing the person can do at a stage.
public enum UpdateAction
{
    /// Nothing to press: Aiko is busy.
    None,

    Check,

    Install,

    Restart,

    OpenDownloadPage,
}

/// The update line of the General page: what it says is the app's, what it offers is decided here.
///
/// The line shows one state on the left and one action on the right, never a row of buttons to
/// choose from. The action is always the next step from that state.
public static class UpdateBlock
{
    public static UpdateAction ActionFor(UpdateStage stage) => stage switch
    {
        UpdateStage.Asking or UpdateStage.Downloading => UpdateAction.None,
        UpdateStage.Available or UpdateStage.DownloadFailed => UpdateAction.Install,
        UpdateStage.Ready => UpdateAction.Restart,
        UpdateStage.CannotInstall => UpdateAction.OpenDownloadPage,
        _ => UpdateAction.Check,
    };

    /// What the site's answer means for the line.
    public static UpdateStage After(UpdateState state) => state switch
    {
        UpdateState.Available => UpdateStage.Available,
        UpdateState.UpToDate => UpdateStage.UpToDate,

        // Never "up to date" when we do not know: that is the one answer that would keep every
        // copy quiet after a bad deploy.
        _ => UpdateStage.CheckFailed,
    };

    /// The download in whole percent, kept inside 0 to 100 whatever the downloader reports.
    public static int Percent(int reported) => Math.Clamp(reported, 0, 100);
}
