using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Opens what Ctrl+click found in the output of a command line (D-358): an address in the browser,
/// a file in VS Code at its line when VS Code is there, otherwise in the program Windows gives it.
/// A file that is not on the disk opens nothing.
static class ShellLinks
{
    public static string? At(string line, int column) => ShellLink.At(line, column);

    public static void Open(string link, string folder)
    {
        try
        {
            if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                return;
            }

            if (FileLink.Parse(link) is not { } file)
            {
                return;
            }

            var path = Path.IsPathRooted(file.Path) ? file.Path : Path.Combine(folder, file.Path);
            if (!File.Exists(path))
            {
                return;
            }

            if (ShellLaunch.FindOnPath("code.cmd") is { } code)
            {
                var start = new ProcessStartInfo(code) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("-g");
                start.ArgumentList.Add(file.Line is { } at ? $"{path}:{at}" : path);
                Process.Start(start);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            TerminalLog.LinkNotOpened();
        }
    }
}
