using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// The one plugin with all of Aiko's skills (D-234). The files ship with Aiko in its "plugins" folder
/// and are copied into the local marketplace with only the skills the person left on, from where
/// Claude Code installs them.
public sealed record SkillPlugin(string Name, string Description)
{
    public const string FolderName = "plugins";

    public const string SkillsFolder = "skills";

    /// Where the marketplace lists it: a path inside the marketplace folder.
    public string Source => $"./{FolderName}/{Name}";

    /// Whether a file of the plugin goes into the copy. A file of a skill goes only while that skill
    /// is on; the manifest and anything outside the skills folder always go.
    public static bool Includes(string relativePath, Func<string, bool> isSkillOn)
    {
        var parts = relativePath.Replace('\\', '/').Split('/');
        return parts is not [SkillsFolder, var skill, _, ..] || isSkillOn(skill);
    }

    /// Claude Code updates a plugin from a folder only when its version changes. The plugin keeps its
    /// own version and gets the hash of the copied files as build metadata, so switching a skill or
    /// changing a file is a new version, and an unchanged copy is never installed again.
    public static string ContentHash(IEnumerable<(string Path, byte[] Content)> files)
    {
        using var sha = SHA256.Create();
        foreach (var (path, content) in files.OrderBy(f => f.Path.Replace('\\', '/'), StringComparer.Ordinal))
        {
            var name = Encoding.UTF8.GetBytes(path.Replace('\\', '/') + "\n");
            sha.TransformBlock(name, 0, name.Length, null, 0);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!)[..12];
    }

    /// The manifest with "1.0.0+hash" as its version. Null when the manifest is not a plugin of ours.
    public static string? VersionedManifest(string manifestJson, string contentHash)
    {
        if (Parse(manifestJson) is not { } manifest || manifest["name"] is not JsonValue)
        {
            return null;
        }

        var own = manifest["version"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Split('+')[0] : "0.0.0";
        manifest["version"] = $"{own}+{contentHash}";
        return manifest.ToJsonString(Indented) + "\n";
    }

    /// The version a copied manifest carries, to see whether the copy is still current.
    public static string? VersionOf(string? manifestJson) =>
        Parse(manifestJson)?["version"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// Name and description from the manifest, for the marketplace list. Null for any other plugin.
    public static SkillPlugin? FromManifest(string? manifestJson) =>
        Parse(manifestJson) is { } manifest
        && manifest["name"] is JsonValue name && name.TryGetValue<string>(out var text)
        && text == SkillCatalog.PluginName
            ? new SkillPlugin(text, manifest["description"] is JsonValue d && d.TryGetValue<string>(out var about) ? about : "")
            : null;

    private static JsonObject? Parse(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
