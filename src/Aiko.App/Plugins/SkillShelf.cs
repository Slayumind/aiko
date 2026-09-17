using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The skills plugin that ships with this copy of Aiko, in the "plugins" folder next to the app, and
/// its copy in the local marketplace (D-234). Claude Code installs from the copy; the copy is renewed
/// only when a file of the plugin changed.
static class SkillShelf
{
    private static readonly string Source = Path.Combine(AppContext.BaseDirectory, SkillPlugin.FolderName, SkillCatalog.PluginName);

    private static (SkillPlugin? Plugin, IReadOnlyList<string> Skills)? Found;

    /// The plugin as it ships, or null when this build has none.
    public static SkillPlugin? Shipped => (Found ??= FindShipped()).Plugin;

    /// The skills inside the shipped plugin, by folder name.
    public static IReadOnlyList<string> Skills => (Found ??= FindShipped()).Skills;

    /// Copies the plugin into the marketplace folder when its files differ from the copy already
    /// there, and removes every other plugin folder, such as the one-skill plugins of older
    /// versions. Returns whether the copy changed.
    public static bool CopyTo(string marketplaceFolder)
    {
        var target = Path.Combine(marketplaceFolder, SkillPlugin.FolderName);
        var changed = false;

        if (Shipped is { } plugin)
        {
            changed = Copy(Path.Combine(target, plugin.Name));
        }

        if (Directory.Exists(target))
        {
            foreach (var old in Directory.GetDirectories(target).Where(d => Path.GetFileName(d) != Shipped?.Name))
            {
                Directory.Delete(old, recursive: true);
            }
        }

        return changed;
    }

    private static bool Copy(string to)
    {
        var files = Directory.EnumerateFiles(Source, "*", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(Source, path), Path: path))
            .ToList();

        var hash = SkillPlugin.ContentHash(files.Select(f => (f.Relative, File.ReadAllBytes(f.Path))));
        var manifest = SkillPlugin.VersionedManifest(File.ReadAllText(ManifestIn(Source)), hash);
        var copied = ManifestIn(to);
        if (manifest is null || (File.Exists(copied) && SkillPlugin.VersionOf(File.ReadAllText(copied)) == SkillPlugin.VersionOf(manifest)))
        {
            return false;
        }

        // A new folder moved into place in one step, so Claude Code never copies half a plugin.
        var temporary = to + "." + Environment.ProcessId + ".tmp";
        if (Directory.Exists(temporary))
        {
            Directory.Delete(temporary, recursive: true);
        }

        foreach (var (relative, path) in files)
        {
            var destination = Path.Combine(temporary, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(path, destination, overwrite: true);
        }

        File.WriteAllText(ManifestIn(temporary), manifest);

        if (Directory.Exists(to))
        {
            Directory.Delete(to, recursive: true);
        }

        Directory.Move(temporary, to);
        return true;
    }

    private static string ManifestIn(string pluginFolder) => Path.Combine(pluginFolder, ".claude-plugin", "plugin.json");

    private static (SkillPlugin?, IReadOnlyList<string>) FindShipped()
    {
        var manifest = ManifestIn(Source);
        if (!File.Exists(manifest) || SkillPlugin.FromManifest(File.ReadAllText(manifest)) is not { } plugin)
        {
            return (null, []);
        }

        var skillsFolder = Path.Combine(Source, SkillPlugin.SkillsFolder);
        var skills = Directory.Exists(skillsFolder)
            ? Directory.GetDirectories(skillsFolder)
                .Where(folder => File.Exists(Path.Combine(folder, "SKILL.md")))
                .Select(folder => Path.GetFileName(folder))
                .ToList()
            : [];

        return (plugin, skills);
    }
}
