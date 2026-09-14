using System.Runtime.InteropServices;
using Aiko.Core;

namespace Aiko.App;

/// A picture of a piece of the screen, in real pixels.
///
/// Leaving CAPTUREBLT out does not keep layered windows out of the picture on Windows 11: the island
/// came out as an orange smudge in the glass under it. Windows that must not be in it are named in
/// Without.
static class ScreenCapture
{
    /// Keeps these windows out of every picture taken inside take, and only then: left on, they would
    /// also vanish from the person's own screenshots and screen sharing.
    public static T Without<T>(IReadOnlyList<nint> windows, Func<T> take)
    {
        foreach (var window in windows)
        {
            if (!SetWindowDisplayAffinity(window, ExcludeFromCapture))
            {
                Log.Write($"screen pictures: a window could not be left out ({Marshal.GetLastWin32Error()})");
            }
        }

        try
        {
            return take();
        }
        finally
        {
            foreach (var window in windows)
            {
                SetWindowDisplayAffinity(window, NoAffinity);
            }
        }
    }

    private const uint ExcludeFromCapture = 0x11;
    private const uint NoAffinity = 0;

    public static GlassImage? Take(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var info = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
        };

        var bitmap = CreateDIBSection(screen, ref info, 0, out var bits, 0, 0);
        try
        {
            if (bitmap == 0)
            {
                return null;
            }

            var previous = SelectObject(memory, bitmap);
            var copied = BitBlt(memory, 0, 0, width, height, screen, x, y, SourceCopy);
            SelectObject(memory, previous);
            if (!copied)
            {
                return null;
            }

            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new GlassImage(pixels, width, height);
        }
        finally
        {
            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    private const int SourceCopy = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(nint dc, ref BitmapInfoHeader info, uint usage, out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint gdiObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint gdiObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int operation);
#pragma warning restore SYSLIB1054
}
