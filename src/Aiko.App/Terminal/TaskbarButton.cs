using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Aiko.Core;
using Aiko.Core.Terminal;
using Windows.Win32.UI.WindowsAndMessaging;

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
///
/// The icons are made here and kept for a few seconds after a newer one replaced them. WPF, given
/// the picture as Window.Icon, destroyed the old icon the moment it set the new one; Explorer reads
/// the icon of a button a little later, and under load it read one that was already gone and drew a
/// black square during the move between the rings and a face.
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
    private string? _appId;
    private byte[] _picture = [];

    /// The icons given to the window, oldest first, with the time each was given.
    private readonly Queue<(HICON Big, HICON Small, long Given)> _given = new();

    /// How long an icon lives after a newer one took its place.
    private static readonly TimeSpan KeepReplaced = TimeSpan.FromSeconds(3);

    public TaskbarButton(Window window, Func<(CardRow? Ring, CardRow? Dot)> rows)
    {
        _window = window;
        _rows = rows;
        _window.TaskbarItemInfo ??= new TaskbarItemInfo();
        _window.SourceInitialized += (_, _) => TakeAppId();

        // To pin or unpin Aiko the person goes to the Start menu or the taskbar and comes back, so the
        // pins are read again each time the window becomes active, and the window moves to the right
        // button while it is open.
        _window.Activated += (_, _) => TakeAppId();
        _faces = new TrayFaceAnimator(window.Dispatcher);
        _faces.Changed += () => Draw(sameRows: true);
        _window.SourceInitialized += (_, _) =>
        {
            // A picture drawn before the window had a handle is given now.
            _picture = [];
            Draw(sameRows: true);
        };
        _window.Closed += (_, _) => Forget(all: true);
    }

    /// Every picture given to the window, for --try-shell.
    internal Action<byte[]>? GivenForCheck { get; set; }

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

    /// An id of its own, unless Aiko is pinned by its shortcut. Only a change touches the window:
    /// Windows moves its button to the pin of the new id.
    private void TakeAppId()
    {
        var program = Environment.ProcessPath;
        if (program is null)
        {
            return;
        }

        var (appId, pinned) = TaskbarIdentity.Choose(TaskbarPins.Read(), program);
        if (appId == _appId && pinned == _pinned)
        {
            return;
        }

        var wasPinned = _pinned;
        _pinned = pinned;
        if (appId != _appId)
        {
            _appId = appId;
            TaskbarPins.Give(new WindowInteropHelper(_window).Handle, appId, program, AppName);
        }

        if (pinned && !wasPinned)
        {
            // The face leaves the middle of a button that now shows the pin.
            _faces.Hide();
        }

        DrawCorner();
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

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _picture = picture;
        GivenForCheck?.Invoke(picture);
        var small = RingIcon.RenderPng(Pixels(SmallSize), _lastRows.Ring, _lastRows.Dot, _faces.Frame, _faces.Face);
        var icons = (Big: RingIcon.FromPng(picture, Pixels(Size)), Small: RingIcon.FromPng(small, Pixels(SmallSize)));
        SendMessage(handle, SetIcon, BigIcon, icons.Big);
        SendMessage(handle, SetIcon, SmallIcon, icons.Small);
        _given.Enqueue((icons.Big, icons.Small, Environment.TickCount64));
        Forget(all: false);
    }

    /// Destroys the icons that were replaced long enough ago; all of them when the window is gone.
    /// The newest stays while the window lives: it is the one the button shows.
    private void Forget(bool all)
    {
        while (_given.Count > (all ? 0 : 1) && (all || Environment.TickCount64 - _given.Peek().Given > KeepReplaced.TotalMilliseconds))
        {
            var (big, small, _) = _given.Dequeue();
            Windows.Win32.PInvoke.DestroyIcon(big);
            Windows.Win32.PInvoke.DestroyIcon(small);
        }
    }

    private const int SmallSize = 16;
    private const int SetIcon = 0x0080;
    private static readonly IntPtr BigIcon = 1;
    private static readonly IntPtr SmallIcon = 0;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private static IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, HICON icon) =>
        SendMessage(window, message, wParam, (IntPtr)icon);

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
}
