using Aiko.Core;

namespace Aiko.Core.Tests;

/// The update line on the General page offers one action, the next step from where it stands.
public class UpdateBlockTests
{
    [Theory]
    [InlineData(UpdateStage.NotAsked, UpdateAction.Check)]
    [InlineData(UpdateStage.Asking, UpdateAction.None)]
    [InlineData(UpdateStage.UpToDate, UpdateAction.Check)]
    [InlineData(UpdateStage.Available, UpdateAction.Install)]
    [InlineData(UpdateStage.CheckFailed, UpdateAction.Check)]
    [InlineData(UpdateStage.Downloading, UpdateAction.None)]
    [InlineData(UpdateStage.Ready, UpdateAction.Restart)]
    [InlineData(UpdateStage.DownloadFailed, UpdateAction.Install)]
    [InlineData(UpdateStage.CannotInstall, UpdateAction.OpenDownloadPage)]
    public void Every_stage_has_one_action(UpdateStage stage, UpdateAction action)
    {
        Assert.Equal(action, UpdateBlock.ActionFor(stage));
    }

    [Theory]
    [InlineData(UpdateState.Available, UpdateStage.Available)]
    [InlineData(UpdateState.UpToDate, UpdateStage.UpToDate)]
    [InlineData(UpdateState.Unknown, UpdateStage.CheckFailed)]
    public void An_answer_we_cannot_read_is_a_failed_check_never_up_to_date(UpdateState state, UpdateStage stage)
    {
        Assert.Equal(stage, UpdateBlock.After(state));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(64, 64)]
    [InlineData(100, 100)]
    [InlineData(140, 100)]
    public void The_percent_stays_between_0_and_100(int reported, int shown)
    {
        Assert.Equal(shown, UpdateBlock.Percent(reported));
    }
}
