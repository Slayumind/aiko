using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The button of the terminal window on the taskbar (D-409, D-418). Not pinned, it repeats the tray
/// icon: the ring of the environment chosen in the tray menu, the dot of the other one, and the face
/// of Aiko in their place at the same moments, with the same short transition. Pinned, Windows draws
/// the pin, and the face takes the corner for its two seconds. The corner, an overlay, shows the
/// sign of the sessions.
///
/// Windows 11 draws the button from the window only when the window has an app id of its own, so
/// the window takes one before it is shown (TaskbarIdentity). A new icon goes to Windows past
/// WindowChrome, which would hide the window and the terminal in it for a frame
/// (TerminalWindow.TakeIcon, D-416), so the icon may change as often as the face needs. It changes
/// only when the picture did.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TaskbarButton
{
    /// The size Windows gives the picture of a taskbar button at 100 percent.
    private const int Size = 32;

    /// The size of an overlay at 100 percent.
    private const int OverlaySize = 16;

    /// The name a pin made from the button shows. A brand, the same in every language.
    private const string AppName = "Aiko";

    private readonly Window _window;
    private readonly Func<(CardRow? Ring, CardRow? Dot)> _rows;
    private readonly TrayFaceAnimator _faces;
    private (CardRow? Ring, CardRow? Dot) _lastRows;
    private SessionBadge _badge = SessionBadge.None;
    private ImageSource? _cornerFace;
    private bool _pinned;
    private byte[] _picture = [];

    public TaskbarButton(Window window, Func<(CardRow? Ring, CardRow? Dot)> rows)
    {
        _window = window;
        _rows = rows;
        _window.TaskbarItemInfo ??= new TaskbarItemInfo();
        _window.SourceInitialized += (_, _) => TakeAppId();
        _faces = new TrayFaceAnimator(window.Dispatcher);
        _faces.Changed += () => Draw(sameRows: true);
    }

    /// New numbers or another environment in the tray menu.
    public void Update() => Draw(sameRows: false);

    /// A face comes or goes, at the same moments as on the tray icon (D-211).
    public void ShowFace(AikoFace? face, FaceStyle style)
    {
        var ground = TaskbarTheme.Ground();
        _cornerFace = face is { } small ? Bitmap(FaceDrawing.For(style, small, ground, Pixels(OverlaySize)), Pixels(OverlaySize)) : null;
        DrawCorner();

        if (face is { } shown && !_pinned)
        {
            _faces.Show(FaceDrawing.For(style, shown, ground, Pixels(Size)));
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
        DrawCorner();
    }

    /// Before the window is shown: an id of its own, unless Aiko is pinned by its shortcut.
    private void TakeAppId()
    {
        var program = Environment.ProcessPath;
        if (program is null)
        {
            return;
        }

        var (appId, pinned) = TaskbarIdentity.Choose(TaskbarPins.Read(), program);
        _pinned = pinned;
        if (appId is not null)
        {
            TaskbarPins.Give(new WindowInteropHelper(_window).Handle, appId, program, AppName);
        }
    }

    private ButtonLook Look() => ButtonLook.For(_pinned, _cornerFace is not null, _badge.Kind);

    private void DrawCorner()
    {
        _window.TaskbarItemInfo.Overlay = Look().Corner switch
        {
            ButtonCorner.Badge => BadgeDrawing.Render(Pixels(OverlaySize), _badge),
            ButtonCorner.Face => _cornerFace,
            _ => null,
        };
    }

    /// The rings or a frame of a face; the rows are read again only for new numbers, not for every
    /// frame of a transition. A pinned button shows the pin, but the picture still goes to the
    /// window: Alt+Tab shows it.
    private void Draw(bool sameRows)
    {
        if (!sameRows)
        {
            _lastRows = _rows();
        }

        var picture = RingIcon.RenderPng(Pixels(Size), _lastRows.Ring, _lastRows.Dot, _faces.Frame, _faces.Face);
        if (picture.AsSpan().SequenceEqual(_picture))
        {
            return;
        }

        _picture = picture;
        _window.Icon = Decode(picture);
    }

    /// Real pixels on the screen of the window, so the picture is not scaled up and blurred.
    private int Pixels(int units) => (int)Math.Round(units * VisualTreeHelper.GetDpi(_window).DpiScaleX);

    private static BitmapSource Bitmap(ImageSource image, int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(image, new Rect(0, 0, size, size));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapFrame Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }
}
