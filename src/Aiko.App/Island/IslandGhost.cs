using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// The landing strip: a pane of frosted glass on the edge where the island in hand will land (D-161).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand.
///
/// The glass is drawn by Windows, not by Aiko: WPF draws in software here and cannot blur what lies
/// behind a window. The system backdrop only frosts the active window, and this one never is, so
/// it uses the blur accent instead (RESEARCH, spike 2026-09-14). Windows then rounds all four
/// corners, and a window region would switch the glass off, so the strip reaches past the screen
/// edge by one corner radius: the corners on that side fall outside the screen.
sealed class IslandGhost : Window
{
    private const double Radius = 8;

    /// A thin white over the blur, just enough to tell the pane from what is behind it. The acrylic
    /// accent looked milky: it adds a grey layer of its own that no tint takes away. Halved again
    /// after the owner asked for twice as clear.
    private static readonly Color GlassTint = Color.FromArgb(0x06, 0xFF, 0xFF, 0xFF);

    /// Grain on the glass, the way real frosted glass catches light. Made once, a tile of random
    /// mostly light specks with very little alpha.
    private static readonly ImageBrush Grain = MakeGrain();

    private ScreenEdge? _edge;

    public IslandGhost()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Content = new System.Windows.Controls.Border { Background = Grain };
        SourceInitialized += (_, _) => MakeGlass();
    }

    /// Raised the first time the strip shows. Both windows stay above everything, and the one shown
    /// last lies on top, so the island has to be lifted back over the glass.
    public event Action? Shown;

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the mouse at once, which is what keeps it feeling attached.
    public void PlaceOn(Box box, ScreenEdge edge)
    {
        var glide = _edge is not null && _edge != edge && Motion.IsOn;
        _edge = edge;

        var pane = PastTheEdge(box, edge);
        Width = pane.Width;
        Height = pane.Height;

        if (glide)
        {
            BeginAnimation(LeftProperty, Motion.To(pane.X, Motion.Expand, Motion.Standard));
            BeginAnimation(TopProperty, Motion.To(pane.Y, Motion.Expand, Motion.Standard));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = pane.X;
            Top = pane.Y;
        }

        if (!IsVisible)
        {
            Show();
            TuckUnderTheTaskbar();
            Shown?.Invoke();
        }
    }

    /// Still above every ordinary window, but just below the taskbar: over the bottom edge the part
    /// that reaches past the edge would otherwise lie on the taskbar, rounded corners and all.
    private void TuckUnderTheTaskbar()
    {
        var taskbar = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (taskbar.IsNull)
        {
            return;
        }

        const SET_WINDOW_POS_FLAGS keepPlaceAndFocus =
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        PInvoke.SetWindowPos((HWND)new WindowInteropHelper(this).Handle, taskbar, 0, 0, 0, 0, keepPlaceAndFocus);
    }

    /// The same box, grown past the screen edge by one corner radius.
    private static Box PastTheEdge(Box box, ScreenEdge edge) => edge switch
    {
        ScreenEdge.Top => box with { Y = box.Y - Radius, Height = box.Height + Radius },
        ScreenEdge.Bottom => box with { Height = box.Height + Radius },
        ScreenEdge.Left => box with { X = box.X - Radius, Width = box.Width + Radius },
        _ => box with { Width = box.Width + Radius },
    };

    private void MakeGlass()
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)!.CompositionTarget.BackgroundColor = Colors.Transparent;

        var accent = new AccentPolicy { AccentState = BlurBehind };
        var size = Marshal.SizeOf<AccentPolicy>();
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, memory, false);
            var data = new CompositionAttributeData { Attribute = AccentPolicyAttribute, Data = memory, SizeOfData = size };
            if (!SetWindowCompositionAttribute(handle, ref data))
            {
                Log.Write("landing strip: Windows gave no glass");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }

        var round = RoundCorners;
        DwmSetWindowAttribute(handle, CornerPreference, ref round, sizeof(int));
    }

    private static ImageBrush MakeGrain()
    {
        const int size = 96;
        var random = new Random(7);
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Premultiplied BGRA: mostly light specks at a few percent over the tint. Dark specks at
            // half made the glass read darker than the screen behind it.
            var light = random.Next(4) != 0;
            var alpha = (byte)(GlassTint.A + random.Next(0, 4));
            var value = light ? alpha : (byte)0;
            pixels[i] = value;
            pixels[i + 1] = value;
            pixels[i + 2] = value;
            pixels[i + 3] = alpha;
        }

        var tile = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        tile.Freeze();

        var brush = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    /// ACCENT_ENABLE_BLURBEHIND: blur only, without the grey layer acrylic lays over it.
    private const int BlurBehind = 3;
    private const int AccentPolicyAttribute = 19;
    private const int CornerPreference = 33;

    /// DWMWCP_ROUND: the 8 px corner Windows 11 gives its own windows. Radius above is the same.
    private const int RoundCorners = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(nint window, ref CompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
#pragma warning restore SYSLIB1054
}
