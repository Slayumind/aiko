using System.IO;
using System.Runtime.InteropServices;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The pins of the taskbar and the app id of a window (D-418). Windows keeps a pin made from the
/// Start menu as a shortcut in a folder of the user, and a pin made from the button of a window only
/// in its list in the registry.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TaskbarPins
{
    private static readonly Guid AppUserModel = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly Guid Link = new("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25");
    private static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    private const uint RelaunchCommand = 2;
    private const uint RelaunchName = 4;
    private const uint Id = 5;
    private const uint LinkTarget = 2;

    private const ushort Text = 31;

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    /// Every pin the folder has. A shortcut that cannot be read is left out: it is not Aiko's.
    public static IReadOnlyList<TaskbarPin> Read()
    {
        var pins = new List<TaskbarPin>();
        if (TaskbarIdentity.ListsOwnPin(Favorites()))
        {
            pins.Add(new TaskbarPin(TaskbarIdentity.Own, null));
        }

        if (!Directory.Exists(Folder))
        {
            return pins;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.lnk"))
        {
            var iid = PropertyStoreId;
            if (SHGetPropertyStoreFromParsingName(file, IntPtr.Zero, 0, ref iid, out var store) != 0)
            {
                continue;
            }

            try
            {
                pins.Add(new TaskbarPin(Value(store, AppUserModel, Id), Value(store, Link, LinkTarget)));
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }

        return pins;
    }

    /// The list of pins Windows keeps in the registry. A pin made from the button of a window lives
    /// only there, with no shortcut in the folder.
    private static byte[] Favorites()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband");
            return key?.GetValue("Favorites") as byte[] ?? [];
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    /// Gives the window the app id, with what a pin made from its button starts: the program and its
    /// name. Windows reads them when the id is set, so they go first. No icon of the pin: with one,
    /// Windows draws the button from it and not from the window (2026-11-13); without, the pin takes
    /// the icon of the program.
    public static void Give(IntPtr window, string appId, string program, string name)
    {
        var iid = PropertyStoreId;
        if (SHGetPropertyStoreForWindow(window, ref iid, out var store) != 0)
        {
            return;
        }

        try
        {
            Put(store, RelaunchCommand, $"\"{program}\"");
            Put(store, RelaunchName, name);
            Put(store, Id, appId);
            store.Commit();
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static string? Value(IPropertyStore store, Guid group, uint id)
    {
        var key = new PropertyKey { Group = group, Id = id };
        if (store.GetValue(ref key, out var value) != 0)
        {
            return null;
        }

        try
        {
            return value.Type == Text ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static void Put(IPropertyStore store, uint id, string text)
    {
        var key = new PropertyKey { Group = AppUserModel, Id = id };
        var value = new PropertyValue { Type = Text, Pointer = Marshal.StringToCoTaskMemUni(text) };
        try
        {
            store.SetValue(ref key, ref value);
        }
        finally
        {
            Marshal.FreeCoTaskMem(value.Pointer);
        }
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropertyValue value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid Group;
        public uint Id;
    }

    /// The head of a PROPVARIANT; only text values are read and written.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropertyValue
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, int flags, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropertyValue value);
}
