using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The download of the speech model (D-313, D-316), one for the whole app: the settings page can
/// close and open again and still see it going.
///
/// The file grows as "<name>.part" next to its place and gets its real name only after its SHA256
/// matched, so a model that is there is a model that is whole. A stopped download keeps its part
/// and goes on from there next time.
static class ModelDownload
{
    private static readonly string Model = ThisComputer.Folders.VoiceModelFile;
    private static readonly string Part = Model + ".part";
    private static CancellationTokenSource? CurrentDownload;

    /// Bytes so far, on a thread of the pool. The page brings it to its own thread.
    public static event Action<long>? Progress;

    /// The end: null when the model is in place, or a short reason for the log and the page.
    public static event Action<string?>? Ended;

    public static bool Running => CurrentDownload is not null;

    public static long Done { get; private set; }

    /// The size of the model file, or null when it is not there.
    public static long? ModelBytes => File.Exists(Model) ? new FileInfo(Model).Length : null;

    public static void Start()
    {
        if (CurrentDownload is not null || ModelBytes == VoiceModel.Bytes)
        {
            return;
        }

        var stop = CurrentDownload = new CancellationTokenSource();
        Task.Run(async () =>
        {
            string? problem;
            try
            {
                problem = await Fetch(stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                problem = "stopped";
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException)
            {
                problem = exception.GetType().Name;
            }

            Log.Write($"voice model download: {problem ?? "done"}");
            CurrentDownload = null;
            Ended?.Invoke(problem);
        });
    }

    public static void Stop() => CurrentDownload?.Cancel();

    /// Removes the model and any part of it: the page offers this when voice input is turned off.
    public static void Remove()
    {
        Stop();
        File.Delete(Model);
        File.Delete(Part);
    }

    private static async Task<string?> Fetch(CancellationToken stop)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Model)!);
        var have = File.Exists(Part) ? new FileInfo(Part).Length : 0;
        if (have > VoiceModel.Bytes)
        {
            File.Delete(Part);
            have = 0;
        }

        // A large file over a slow line: the time is for the whole file, the stop button is the
        // real limit.
        using var client = AikoHttp.Plain(TimeSpan.FromHours(2));
        using var request = new HttpRequestMessage(HttpMethod.Get, VoiceModel.Url);
        if (have > 0)
        {
            request.Headers.Range = new RangeHeaderValue(have, null);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop).ConfigureAwait(false);
        if (have > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            have = 0;
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"HTTP {(int)response.StatusCode}";
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var file = new FileStream(Part, have > 0 ? FileMode.Open : FileMode.Create, FileAccess.ReadWrite))
        {
            // Going on from a part: what is already there counts into the hash first.
            var buffer = new byte[1 << 20];
            for (int read; have > 0 && (read = await file.ReadAsync(buffer, stop).ConfigureAwait(false)) > 0;)
            {
                hash.AppendData(buffer, 0, read);
            }

            file.Position = have;
            Done = have;
            await using var body = await response.Content.ReadAsStreamAsync(stop).ConfigureAwait(false);
            for (int read; (read = await body.ReadAsync(buffer, stop).ConfigureAwait(false)) > 0;)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), stop).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                Done += read;
                Progress?.Invoke(Done);
            }
        }

        if (Done != VoiceModel.Bytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != VoiceModel.Sha256)
        {
            File.Delete(Part);
            return "the file did not match";
        }

        File.Move(Part, Model, overwrite: true);
        return null;
    }
}
