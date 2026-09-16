using Aiko.Core;

namespace Aiko.App;

/// One place where a version check is made: the daily timer and the button in settings both come
/// here. Reading the consent and writing down the reported periods live together, because a caller
/// that forgot the second half would quietly lose a week of counting.
static class UpdateRun
{
    public static StatsChoice Choice()
    {
        var allowed = SettingsStore.Load().SendStats;
        var persona = allowed && SettingsStore.LoadEnvironments().Environments.Any(environment => environment.Persona);
        return new StatsChoice(allowed, persona);
    }

    public static async Task<UpdateInfo> AskAsync(CancellationToken cancel)
    {
        using var client = new UpdateClient();
        var answer = await client.AskAsync(Choice(), cancel).ConfigureAwait(true);

        if (answer.Answered && (answer.Week is not null || answer.Month is not null))
        {
            var reported = ReportedPeriods.Read();
            ReportedPeriods.Write(reported with
            {
                Week = answer.Week ?? reported.Week,
                Month = answer.Month ?? reported.Month,
            });
        }

        return answer.Info;
    }

    /// "Reset ID" in the privacy page. The periods go with the identifier: keeping them would make
    /// the fresh identifier skip this week's count.
    public static void ForgetIdentity()
    {
        InstallId.Forget();
        ReportedPeriods.Forget();
        Log.Write("privacy: the install id was reset");
    }
}
