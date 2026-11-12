using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The button of the terminal window on the taskbar (D-409). It repeats the tray icon: the ring of
/// the environment chosen in the tray menu, the dot of the other one, and a face at the same
/// moments. The sign of the sessions sits in its corner as an overlay. It draws only when
/// something changes; the face plays the same short transition as on the tray.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TaskbarButton
{
    /// The size Windows gives the picture of a taskbar button at 100 percent.
    private const int Size = 32;

    /// The size of an overlay at 100 percent.
    private const int OverlaySize = 16;

    private readonly Window _window;
    private readonly Func<(CardRow? Ring, CardRow? Dot)> _rows;
    private readonly TrayFaceAnimator _faces;
    private (CardRow? Ring, CardRow? Dot) _lastRows;
    private SessionBadge _badge = SessionBadge.None;

    public TaskbarButton(Window window, Func<(CardRow? Ring, CardRow? Dot)> rows)
    {
        _window = window;
        _rows = rows;
        _window.TaskbarItemInfo ??= new TaskbarItemInfo();
        _faces = new TrayFaceAnimator(window.Dispatcher);
        _faces.Changed += () => Draw(sameRows: true);
    }

    /// New numbers or another environment in the tray menu.
    public void Update() => Draw(sameRows: false);

    /// A face comes or goes, at the same moments as on the tray icon (D-211).
    public void ShowFace(AikoFace? face, FaceStyle style)
    {
        if (face is { } shown)
        {
            _faces.Show(FaceDrawing.For(style, shown, TaskbarTheme.Ground(), Pixels(Size)));
        }
        else
        {
            _faces.Hide();
        }
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

    private void Draw(bool sameRows)
    {
        if (!sameRows)
        {
            _lastRows = _rows();
        }

        var size = Pixels(Size);
        _window.Icon = Decode(RingIcon.RenderPng(size, _lastRows.Ring, _lastRows.Dot, _faces.Frame, _faces.Face));
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
