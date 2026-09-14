using System.Runtime.InteropServices;
using System.Windows;

namespace Aiko.App;

/// The real mouse, for the --try checks that use a window the way a person would.
/// Points are in real screen pixels, which is what PointToScreen gives.
static class MouseInput
{
    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;

    public static void MoveTo(Point point) => SetCursorPos((int)point.X, (int)point.Y);

    public static void Down() => mouse_event(LeftDown, 0, 0, 0, 0);

    public static void Up() => mouse_event(LeftUp, 0, 0, 0, 0);

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extraInfo);
#pragma warning restore SYSLIB1054
}
