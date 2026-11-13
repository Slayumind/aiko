using System.IO;
using System.Runtime.InteropServices;
namespace Aiko.App;

/// Sends a folder to the Recycle Bin instead of deleting it, so the person can take it back.
static class RecycleBin
{
    private const uint Delete = 3;
    private const ushort Silent = 0x0004;
    private const ushort NoConfirmation = 0x0010;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoErrorUi = 0x0400;

    public static bool Send(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        var operation = new FileOperation
        {
            Function = Delete,
            // The list of paths ends with two zeros; the marshaller adds the second one.
            From = folder + '\0',
            Flags = (ushort)(AllowUndo | NoConfirmation | Silent | NoErrorUi),
        };

        var result = SHFileOperation(ref operation);
        var sent = result == 0 && !operation.AnyAborted && !Directory.Exists(folder);
        Log.Write($"recycle bin: {Path.GetFileName(folder)} {(sent ? "sent" : $"not sent ({result})")}");
        return sent;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct FileOperation
    {
        public nint Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        public bool AnyAborted;
        public nint NameMappings;
        public string? ProgressTitle;
    }

#pragma warning disable SYSLIB1054
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref FileOperation operation);
#pragma warning restore SYSLIB1054
}
