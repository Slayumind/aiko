using System.Runtime.InteropServices;
using Windows.Win32;

namespace Aiko.App;

/// Loads Direct3D on a second thread while the rest of the start goes on.
///
/// WPF calls Direct3DCreate9 for its first window, also with software rendering. On a laptop with
/// two video cards that one call took 480 ms of an 815 ms start, most of it loading the video
/// driver. Windows keeps what the call loaded, so when WPF asks a moment later the answer is
/// ready. The object itself is not needed and is released at once.
static class GraphicsWarmUp
{
    public static void Start() =>
        new Thread(Run) { IsBackground = true, Name = "Direct3D warm-up" }.Start();

    private static void Run()
    {
        try
        {
            var direct3D = PInvoke.Direct3DCreate9(PInvoke.D3D_SDK_VERSION);
            if (direct3D is not null)
            {
                Marshal.ReleaseComObject(direct3D);
            }
        }
        catch (Exception)
        {
            // Everything is caught on purpose. An exception that leaves a thread ends the whole
            // process, and this thread only saves time: WPF makes the same call a moment later
            // and deals with whatever is wrong, a machine with no Direct3D 9 included.
        }
    }
}
