using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Aiko.Core;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Aiko.App;

/// How far the install got. The words the user sees are picked from this in the settings page.
enum InstallStep
{
    /// This copy was not put here by the installer — a build folder, or the portable zip. Velopack
    /// has nothing to replace, so the download page is the only honest answer.
    NotInstalled,

    NothingNewer,

    /// GitHub could not be reached, or the package would not download.
    Failed,

    /// The package is here and checked. Restarting puts it in place.
    Ready,

    /// The package is here and it did not pass. It has been deleted.
    Refused,
}

sealed record InstallOutcome(InstallStep Step, UpdateVerdict Verdict, string? Version);

/// Downloads a new version from GitHub with Velopack and installs it only when the release list
/// is signed with our key and the package matches its line (D-246).
///
/// Nothing here starts by itself. The daily check only ever says "a newer one is out"; this runs
/// when the person presses the button, and the new version is put in place when they press the
/// second one. Velopack applies an update by restarting the app, and a tray app that restarts
/// itself while somebody is working is a tray app people uninstall.
static class UpdateInstall
{
    private const string Repository = "https://github.com/Slayumind/aiko";

    private const string ManifestName = "SHA256SUMS.txt";

    private const string SignatureName = "SHA256SUMS.txt.sig";

    private static UpdateManager? Manager;
    private static VelopackAsset? CheckedPackage;

    /// The version waiting to be put in place, or null while there is none.
    public static string? ReadyVersion { get; private set; }

    public static async Task<InstallOutcome> DownloadAsync(CancellationToken cancel)
    {
        try
        {
            return await RunAsync(cancel).ConfigureAwait(true);
        }
        catch (Exception failed) when (failed is HttpRequestException or IOException
                                           or TaskCanceledException or InvalidOperationException)
        {
            Log.Write($"update: the download failed ({failed.GetType().Name})");
            return new InstallOutcome(InstallStep.Failed, UpdateVerdict.Ok, null);
        }
    }

    /// Restarts into the version that was checked. Velopack ends this process itself.
    public static void ApplyAndRestart()
    {
        if (Manager is null || CheckedPackage is null)
        {
            return;
        }

        Log.Write($"update: restarting into {CheckedPackage.Version}");
        Manager.ApplyUpdatesAndRestart(CheckedPackage);
    }

    private static async Task<InstallOutcome> RunAsync(CancellationToken cancel)
    {
        using var downloader = new GuardedDownloader();

        // The locator knows where this copy was installed and where its packages are kept. The
        // manager keeps its own private, so we hand it the same one and can then ask where the
        // package landed.
        var locator = VelopackLocator.Current;
        var manager = new UpdateManager(new GithubSource(Repository, null, false, downloader), null, locator);

        if (!manager.IsInstalled || locator.PackagesDir is not { } packages)
        {
            Log.Write("update: this copy was not put here by the installer, so nothing is applied");
            return new InstallOutcome(InstallStep.NotInstalled, UpdateVerdict.Ok, null);
        }

        var update = await manager.CheckForUpdatesAsync().ConfigureAwait(true);
        if (update is null)
        {
            return new InstallOutcome(InstallStep.NothingNewer, UpdateVerdict.Ok, null);
        }

        var asset = update.TargetFullRelease;
        var version = asset.Version.ToString();
        Log.Write($"update: downloading {version}");
        await manager.DownloadUpdatesAsync(update, null, cancel).ConfigureAwait(true);

        // Where Velopack keeps what it downloaded, the same path its own helper builds.
        var package = Path.Combine(packages, asset.FileName);
        var release = await ReleaseListAsync(downloader, $"v{version}", cancel).ConfigureAwait(true);

        var verdict = UpdateGate.Check(
            UpdateKey.Pem, release.Manifest, release.Signature, asset.FileName, HashOf(package));

        if (verdict != UpdateVerdict.Ok)
        {
            // The package goes, so that a refused file cannot be applied later by anything else.
            Delete(package);
            Log.Write($"update refused: {UpdateGate.Reason(verdict)}");
            return new InstallOutcome(InstallStep.Refused, verdict, version);
        }

        Manager = manager;
        CheckedPackage = asset;
        ReadyVersion = version;
        Log.Write($"update: {version} is checked and ready to install");
        return new InstallOutcome(InstallStep.Ready, verdict, version);
    }

    /// SHA256SUMS.txt of the release and the signature beside it. A release made before the key
    /// existed has neither, and then the gate answers "no signed list" and nothing is installed.
    private static async Task<(byte[] Manifest, byte[] Signature)> ReleaseListAsync(
        GuardedDownloader downloader, string tag, CancellationToken cancel)
    {
        var manifest = await downloader.BytesAsync($"{Repository}/releases/download/{tag}/{ManifestName}", cancel)
            .ConfigureAwait(true);
        var signature = await downloader.BytesAsync($"{Repository}/releases/download/{tag}/{SignatureName}", cancel)
            .ConfigureAwait(true);
        return (manifest, signature);
    }

    private static byte[] HashOf(string file)
    {
        using var stream = File.OpenRead(file);
        return SHA256.HashData(stream);
    }

    private static void Delete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            Log.Write($"update: the refused package is still on disk ({failed.GetType().Name})");
        }
    }
}

/// Velopack's downloader, made to open a connection the way the rest of Aiko does.
///
/// Velopack would otherwise build an HttpClient of its own, and the promise of D-005 — Aiko opens
/// these hosts and nothing else — would stop at the edge of our own code.
sealed class GuardedDownloader : HttpClientFileDownloader, IDisposable
{
    private readonly List<HttpClient> _made = [];

    public async Task<byte[]> BytesAsync(string address, CancellationToken cancel)
    {
        var client = AikoHttp.Plain(TimeSpan.FromSeconds(30));
        _made.Add(client);

        using var response = await client.GetAsync(address, cancel).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false)
            : [];
    }

    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
    {
        var client = AikoHttp.Plain(TimeSpan.FromSeconds(timeout <= 0 ? 60 : timeout));
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        _made.Add(client);
        return client;
    }

    public void Dispose()
    {
        foreach (var client in _made)
        {
            client.Dispose();
        }

        _made.Clear();
    }
}
