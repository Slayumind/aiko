using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The button of the terminal window on the taskbar (D-409, D-415). The icon shows the rings of the
/// tray icon: the ring of the environment chosen in the tray menu and the dot of the other one. The
/// corner, an overlay, shows the sign of the sessions, and the face of Aiko for its two seconds.
///
/// A new icon goes to Windows past WindowChrome, which would hide the window and the terminal in it
/// for a frame (TerminalWindow.TakeIcon, D-416), so the rings change as soon as the picture does.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TaskbarButton
{
    /// The size Windows gives the picture of a taskbar button at 100 percent.
    private const int Size = 32;

    /// The size of an overlay at 100 percent.
    private const int OverlaySize = 16;

    private readonly Window _window;
    private readonly Func<(CardRow? Ring, CardRow? Dot)> _rows;
    private SessionBadge _badge = SessionBadge.None;
    private ImageSource? _face;
    private byte[] _picture = [];

    public TaskbarButton(Window window, Func<(CardRow? Ring, CardRow? Dot)> rows)
    {
        _window = window;
        _rows = rows;
        _window.TaskbarItemInfo ??= new TaskbarItemInfo();
    }

    /// New numbers or another environment in the tray menu: the rings are drawn again, and the icon
    /// changes only when the picture did.
    public void Update()
    {
        var (ring, dot) = _rows();
        var picture = RingIcon.RenderPng(Pixels(Size), ring, dot);
        if (picture.AsSpan().SequenceEqual(_picture))
        {
            return;
        }

        _picture = picture;
        _window.Icon = Decode(picture);
    }

    /// A face comes or goes, at the same moments as on the tray icon (D-211): it takes the corner
    /// from the sign while it stays.
    public void ShowFace(AikoFace? face, FaceStyle style)
    {
        _face = face is { } shown ? Rasterize(FaceDrawing.For(style, shown, TaskbarTheme.Ground(), Pixels(OverlaySize)), Pixels(OverlaySize)) : null;
        PutCorner();
    }

    /// The sign of the sessions. Only a change touches the taskbar.
    public void ShowBadge(SessionBadge badge)
    {
        if (badge == _badge)
        {
            return;
        }

        _badge = badge;
        PutCorner();
    }

    private void PutCorner() =>
        _window.TaskbarItemInfo.Overlay = _face ?? BadgeDrawing.Render(Pixels(OverlaySize), _badge);

    private static BitmapSource Rasterize(ImageSource image, int pixels)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(image, new Rect(0, 0, pixels, pixels));
        }

        var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// Real pixels on the screen of the window, so the picture is not scaled up and blurred.
    private int Pixels(int units) => (int)Math.Round(units * VisualTreeHelper.GetDpi(_window).DpiScaleX);

    private static BitmapFrame Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }
}
