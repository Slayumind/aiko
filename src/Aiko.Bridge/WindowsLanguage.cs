using System.Runtime.InteropServices;

/// The language of the Windows interface. The bridge runs with invariant globalization to start
/// fast, so CultureInfo cannot tell it; Windows can.
static class WindowsLanguage
{
    public static int UserInterface()
    {
        try
        {
            return GetUserDefaultUILanguage();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return 0;
        }
    }

    // One call with no arguments and a plain number back, so the classic import is enough and the
    // project needs no unsafe code for the generated one.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
#pragma warning restore SYSLIB1054
}
