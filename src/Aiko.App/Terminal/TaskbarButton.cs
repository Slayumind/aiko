using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The button of the terminal window on the taskbar (D-409, D-414). It shows the rings of the tray
/// icon: the ring of the environment chosen in the tray menu and the dot of the other one. The sign
/// of the sessions sits in its corner as an overlay. It has no face: every new icon makes Windows
/// build the framed window again, and the terminal in it missed a frame each time, a flicker on
/// every face. A new icon goes only when the picture changed.
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

    /// The sign of the sessions. Only a change touches the taskbar.
    public void ShowBadge(SessionBadge badge)
    {
        if (badge == _badge)
        {
            return;
        }

        _badge = badge;
        _window.TaskbarItemInfo.Overlay = BadgeDrawing.Render(Pixels(OverlaySize), badge);
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
