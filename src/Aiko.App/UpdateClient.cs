using System.Net.Http;
using System.Reflection;
using Aiko.Core;

namespace Aiko.App;

/// Asks slayumind.org which version is the latest, and, when the user has allowed it, says "one
/// copy of Aiko ran today" while it is there.
///
/// The number comes from the site and the files come from GitHub. Two switches, not one: update
/// checks and the count are separate consents since 0.2.1, because somebody may well want to hear
/// about a new version without being counted. It is still one request — with the count off, the
/// identifier and the flags are simply not in it and the server has nothing to write.
///
/// What goes along with the question, when the count is on: the version, the Windows version, an
/// identifier that changes every day, whether this is the first run of the week and of the month,
/// and whether the personality, the skills and the memory are on anywhere. Nothing else.
sealed class UpdateClient : IDisposable
{
    private const string Endpoint = "https://slayumind.org/api/v1/aiko/version";

    private readonly HttpClient _http;

    public UpdateClient()
    {
        // Short: nobody should wait for a version check, and a silent failure is fine here.
        _http = AikoHttp.Client(TimeSpan.FromSeconds(2.5));
    }

    public async Task<UpdateAnswer> AskAsync(StatsChoice stats, CancellationToken cancel)
    {
        var request = Build(stats);

        try
        {
            using var response = await _http.GetAsync(request.Address, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"update check: the site answered {(int)response.StatusCode}");
                return UpdateAnswer.NoAnswer;
            }

            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return new UpdateAnswer(UpdateInfo.FromJson(body), true, request.Week, request.Month);
        }
        catch (TaskCanceledException)
        {
            Log.Write("update check: the site took too long");
            return UpdateAnswer.NoAnswer;
        }
        catch (HttpRequestException)
        {
            Log.Write("update check: no connection");
            return UpdateAnswer.NoAnswer;
        }
    }

    /// The day is UTC on both sides. It used to be the computer's own day while the site wrote the
    /// row under its UTC day; east of Greenwich one copy could then send two different identifiers
    /// inside one server day, and the unique index counted it twice.
    private static Request Build(StatsChoice stats)
    {
        var version = Uri.EscapeDataString(CurrentVersion());

        if (!stats.Allowed)
        {
            return new Request(new Uri($"{Endpoint}?v={version}"), null, null);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var id = Heartbeat.DailyId(InstallId.Current(), today);
        if (id.Length == 0)
        {
            // No steady value on this computer, so no identifier at all. See InstallId.
            return new Request(new Uri($"{Endpoint}?v={version}"), null, null);
        }

        var reported = ReportedPeriods.Read();
        var week = Heartbeat.FirstThisWeek(reported.Week, today) ? Heartbeat.WeekKey(today) : null;
        var month = Heartbeat.FirstThisMonth(reported.Month, today) ? Heartbeat.MonthKey(today) : null;

        var windows = Uri.EscapeDataString(Environment.OSVersion.Version.ToString());
        var address = $"{Endpoint}?v={version}&os={windows}&day={id}&p={Bit(stats.Persona)}&s={Bit(stats.Skills)}&mem={Bit(stats.Memory)}"
            + (week is null ? string.Empty : "&w=1")
            + (month is null ? string.Empty : "&m=1");

        return new Request(new Uri(address), week, month);
    }

    private static int Bit(bool on) => on ? 1 : 0;

    public static string CurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    public void Dispose() => _http.Dispose();

    private readonly record struct Request(Uri Address, string? Week, string? Month);
}

/// What the user allows and what there is to say about this copy, gathered before the request so
/// that the client itself reads nothing from the settings.
///
/// The three flags say only whether a thing is on in at least one environment: not in which, not
/// how many, and nothing about what the skills or the memory hold.
readonly record struct StatsChoice(bool Allowed, bool Persona, bool Skills, bool Memory);

/// Whether the site answered, and which periods the request claimed. The periods are written down
/// only on an answer: a claim that never arrived must be made again.
sealed record UpdateAnswer(UpdateInfo Info, bool Answered, string? Week, string? Month)
{
    public static readonly UpdateAnswer NoAnswer = new(UpdateInfo.Unknown, false, null, null);
}
