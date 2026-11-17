using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What goes straight into the line of a program when there is no editor in between (D-370): a
/// picture is written as a PNG into the folder of the session and its path pasted on its own, so
/// Claude Code makes it an attachment; another file is its path in quotes; text is pasted as it is.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalPaste
{
    /// Pictures larger than this are made smaller before they go: Claude Code takes up to about
    /// 5 MB of a picture, and a screenshot of a large screen is often more as PNG.
    private const int LargestSide = 1800;

    private static readonly string[] PictureFiles = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    public static bool IsPicture(string path) => PictureFiles.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// The writes for what the clipboard holds now, or none when it holds nothing to paste.
    public static IReadOnlyList<string> FromClipboard(string picturesFolder)
    {
        try
        {
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } picture && Save(picture, picturesFolder) is { } path)
            {
                return EditorSend.Inserts("", [path]);
            }

            if (Clipboard.ContainsFileDropList())
            {
                return ForFiles([.. Clipboard.GetFileDropList().Cast<string>()]);
            }

            return Clipboard.ContainsText() ? EditorSend.Inserts(Clipboard.GetText(), []) : [];
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program holds the clipboard; the person pastes again.
            return [];
        }
    }

    /// Dropped or copied files: each picture on its own, the rest as paths in quotes, one paste.
    public static IReadOnlyList<string> ForFiles(IReadOnlyList<string> paths)
    {
        var pictures = paths.Where(IsPicture).ToList();
        var others = paths.Where(path => !IsPicture(path)).Select(path => path.Contains(' ') ? $"\"{path}\"" : path).ToList();
        return EditorSend.Inserts(others.Count > 0 ? string.Join(' ', others) + " " : "", pictures);
    }

    /// The picture as a PNG in the folder, made smaller when it is large. Null when it cannot be written.
    public static string? Save(BitmapSource picture, string folder)
    {
        var scale = Math.Min(1.0, LargestSide / (double)Math.Max(picture.PixelWidth, picture.PixelHeight));
        BitmapSource sized = scale < 1 ? new TransformedBitmap(picture, new ScaleTransform(scale, scale)) : picture;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sized));
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"picture-{Guid.NewGuid().ToString("N")[..8]}.png");
            using var file = File.Create(path);
            encoder.Save(file);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
