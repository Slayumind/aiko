using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// The landing strip: a pane of Windows glass on the edge where the island in hand will land (D-161, D-186).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand.
///
/// The glass is drawn by Windows, not by Aiko: WPF draws in software here and cannot blur what lies
/// behind a window. The system backdrop only frosts the active window, and this one never is, so
/// it uses the acrylic accent instead (RESEARCH, spike 2026-09-14). A window region gives it the
/// shape of the island: the free corners are cut at 45 degrees (D-453).
sealed class IslandGhost : Window
{
    private const double CutSize = 8;

    /// An older Windows switched the glass off for a window with a region (spike 2026-09-14). This
    /// build keeps it (2026-11-18); before it the strip stays a plain rectangle of glass.
    private const int RegionKeepsGlassFrom = 26300;

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
        SourceInitialized += (_, _) =>
        {
            MakeGlass();
            CutCorners();
        };
        SizeChanged += (_, _) => CutCorners();
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

        Width = box.Width;
        Height = box.Height;
        CutCorners();

        if (glide)
        {
            BeginAnimation(LeftProperty, Motion.To(box.X, Motion.Expand, Motion.Standard));
            BeginAnimation(TopProperty, Motion.To(box.Y, Motion.Expand, Motion.Standard));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = box.X;
            Top = box.Y;
        }

        if (!IsVisible)
        {
            Show();
            TaskbarOrder.PutUnder(this);
            Shown?.Invoke();
        }
    }

    /// The outline of the landed island: the two corners against the screen stay whole.
    private void CutCorners()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (Environment.OSVersion.Version.Build < RegionKeepsGlassFrom || handle == 0 || _edge is not { } edge || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Round(ActualWidth * scale.DpiScaleX);
        var height = (int)Math.Round(ActualHeight * scale.DpiScaleY);
        var cut = (int)Math.Round(CutSize * scale.DpiScaleX);
        var (top, right, bottom, left) = (edge != ScreenEdge.Top, edge != ScreenEdge.Right, edge != ScreenEdge.Bottom, edge != ScreenEdge.Left);
        var points = new List<RegionPoint>();
        void Corner(bool cutIt, RegionPoint first, RegionPoint sharp, RegionPoint second)
        {
            if (cutIt)
            {
                points.Add(first);
                points.Add(second);
            }
            else
            {
                points.Add(sharp);
            }
        }

        Corner(left && top, new(0, cut), new(0, 0), new(cut, 0));
        Corner(top && right, new(width - cut, 0), new(width, 0), new(width, cut));
        Corner(right && bottom, new(width, height - cut), new(width, height), new(width - cut, height));
        Corner(bottom && left, new(cut, height), new(0, height), new(0, height - cut));

        // Windows owns the region from here on and frees the one it replaces.
        SetWindowRgn(handle, CreatePolygonRgn([.. points], points.Count, Winding), true);
    }

    private void MakeGlass()
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)!.CompositionTarget.BackgroundColor = Colors.Transparent;

        var accent = new AccentPolicy { AccentState = AcrylicBlurBehind, GradientColor = AcrylicTint };
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

        var square = SquareCorners;
        DwmSetWindowAttribute(handle, CornerPreference, ref square, sizeof(int));
    }

    /// ACCENT_ENABLE_ACRYLICBLURBEHIND: the strongest blur Windows gives a window without focus.
    private const int AcrylicBlurBehind = 4;

    /// The colour acrylic lays over its blur, ABGR: Aiko's surface #171717 at 5 %, so the glass is 95 %
    /// see-through, as the owner asked. Aiko draws nothing of its own over it.
    private const int AcrylicTint = 0x0D171717;

    private const int AccentPolicyAttribute = 19;
    private const int CornerPreference = 33;

    /// DWMWCP_DONOTROUND: the corners come from the region alone.
    private const int SquareCorners = 1;

    private const int Winding = 2;

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct RegionPoint(int X, int Y);

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

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint window, nint region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern nint CreatePolygonRgn(RegionPoint[] points, int count, int mode);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
#pragma warning restore SYSLIB1054
}
