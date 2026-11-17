using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Aiko.Core;
using Microsoft.Win32;

namespace Aiko.App;

/// Finds Claude Code and opens it in a new window for one account folder.
///
/// Aiko never signs in itself: Anthropic does not allow other programs to offer Claude.ai login
/// (D-157). It opens Claude Code, and Claude Code asks the person to sign in in the browser.
static class ClaudeLauncher
{
    public static string? FindClaude()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = ClaudeInstall.FreshPath(
            ThisComputer.Platform,
            ReadPath(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
            ReadPath(Registry.CurrentUser, "Environment"),
            Environment.ExpandEnvironmentVariables);

        return ClaudeInstall.Find(ThisComputer.Platform, path, CommandFolder.Folder, home, SafeExists);
    }

    /// Opens Claude Code in its own console window. The folder of environment 1 is used by leaving
    /// CLAUDE_CONFIG_DIR out, the way a plain claude would, never by pointing the variable at it.
    public static bool Open(string configFolder, string workingDirectory)
    {
        if (FindClaude() is not { } claude)
        {
            Log.Write("launch: claude.exe not found");
            return false;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var start = new ProcessStartInfo(claude)
        {
            // Started from a window app without a console, a console program gets a window of its
            // own, and Windows Terminal takes it when it is the default terminal.
            UseShellExecute = false,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : home,
        };

        if (ClaudeConfigFolder.IsDefault(configFolder, home))
        {
            start.Environment.Remove("CLAUDE_CONFIG_DIR");
        }
        else
        {
            start.Environment["CLAUDE_CONFIG_DIR"] = configFolder;
        }

        try
        {
            Process.Start(start)?.Dispose();
            Log.Write($"launch: Claude Code opened for {Path.GetFileName(configFolder)}");
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Write($"launch: could not open Claude Code ({e.GetType().Name})");
            return false;
        }
    }

    private static string? ReadPath(RegistryKey root, string keyPath)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            return key?.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static bool SafeExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
