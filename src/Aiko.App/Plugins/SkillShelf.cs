using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The skills that ship with this copy of Aiko, in the "plugins" folder next to the app, and their
/// copies in the local marketplace (D-202). Claude Code installs a skill from the copy; the copy is
/// renewed only when a file of the skill changed.
static class SkillShelf
{
    private static readonly string Source = Path.Combine(AppContext.BaseDirectory, SkillPlugin.FolderName);

    private static IReadOnlyList<SkillPlugin>? Found;

    public static IReadOnlyList<SkillPlugin> Shipped => Found ??= FindShipped();

    /// Copies every shipped skill whose files changed into the marketplace folder, and removes the
    /// copies of skills this Aiko no longer ships. Returns the names of the skills that changed.
    public static IReadOnlyList<string> CopyTo(string marketplaceFolder)
    {
        var target = Path.Combine(marketplaceFolder, SkillPlugin.FolderName);
        var changed = new List<string>();

        foreach (var skill in Shipped)
        {
            var from = Path.Combine(Source, skill.Name);
            var to = Path.Combine(target, skill.Name);
            var files = Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories)
                .Select(path => (Relative: Path.GetRelativePath(from, path), Path: path))
                .ToList();

            var hash = SkillPlugin.ContentHash(files.Select(f => (f.Relative, File.ReadAllBytes(f.Path))));
            var manifest = SkillPlugin.VersionedManifest(File.ReadAllText(Path.Combine(from, ".claude-plugin", "plugin.json")), hash);
            var copied = Path.Combine(to, ".claude-plugin", "plugin.json");
            if (manifest is null || (File.Exists(copied) && SkillPlugin.VersionOf(File.ReadAllText(copied)) == SkillPlugin.VersionOf(manifest)))
            {
                continue;
            }

            // A new folder moved into place in one step, so Claude Code never copies half a skill.
            var temporary = to + "." + Environment.ProcessId + ".tmp";
            foreach (var (relative, path) in files)
            {
                var destination = Path.Combine(temporary, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(path, destination, overwrite: true);
            }

            File.WriteAllText(Path.Combine(temporary, ".claude-plugin", "plugin.json"), manifest);

            if (Directory.Exists(to))
            {
                Directory.Delete(to, recursive: true);
            }

            Directory.Move(temporary, to);
            changed.Add(skill.Name);
        }

        if (Directory.Exists(target))
        {
            foreach (var old in Directory.GetDirectories(target).Where(d => Shipped.All(s => s.Name != Path.GetFileName(d))))
            {
                Directory.Delete(old, recursive: true);
            }
        }

        return changed;
    }

    private static List<SkillPlugin> FindShipped()
    {
        if (!Directory.Exists(Source))
        {
            return [];
        }

        return Directory.GetDirectories(Source)
            .Select(folder => Path.Combine(folder, ".claude-plugin", "plugin.json"))
            .Where(File.Exists)
            .Select(manifest => SkillPlugin.FromManifest(File.ReadAllText(manifest)))
            .OfType<SkillPlugin>()
            .Where(skill => File.Exists(Path.Combine(Source, skill.Name, "skills", skill.Name, "SKILL.md")))
            .ToList();
    }
}
