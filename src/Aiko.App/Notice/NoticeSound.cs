using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;

namespace Aiko.App;

/// The sound of a session that waits for the person (D-375): Aiko's own, so it is not taken for
/// mail or a chat. The file is quiet itself; the player has no volume. It plays only while the
/// system takes notices: not while "Do not disturb", a presentation or a full-screen app is on.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class NoticeSound
{
    private static byte[]? Wave;

    public static void Play()
    {
        if (!SystemAcceptsNotices())
        {
            return;
        }

        try
        {
            Wave ??= Read();
            // A new player each time: one that still plays would be cut off by the next sound.
            new SoundPlayer(new MemoryStream(Wave)).Play();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            Log.Write($"notice sound: {e.GetType().Name}");
        }
    }

    private static byte[] Read()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Sounds/waiting.wav", UriKind.Absolute))
            ?? throw new IOException("the sound is not in the app");
        using var stream = resource.Stream;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// Whether the system takes notices now (SHQueryUserNotificationState).
    private static bool SystemAcceptsNotices() =>
        SHQueryUserNotificationState(out var state) != 0 || state == AcceptsNotifications;

    private const int AcceptsNotifications = 5;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
