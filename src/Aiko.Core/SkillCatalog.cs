namespace Aiko.Core;

/// What a group of skills is about. The settings page and the site show the skills under these.
public enum SkillDomain
{
    Projects,
    Games,
}

/// Skills of one domain, in the order they are listed.
public sealed record SkillGroup(SkillDomain Domain, IReadOnlyList<string> Skills);

/// Aiko's own skills, grouped by domain, in the order the settings page lists them. All of them ship
/// inside one plugin, so Claude Code shows each one as aiko:name (D-234).
public static class SkillCatalog
{
    /// The plugin that holds every skill.
    public const string PluginName = "aiko";

    public static IReadOnlyList<SkillGroup> Groups { get; } =
    [
        new(SkillDomain.Projects, ["copy", "docs-hygiene", "release-gate", "calendar", "drive"]),
        new(SkillDomain.Games, ["gamedesign-research", "playtest", "blender-to-unity", "texturing", "glb-for-web", "palette"]),
    ];

    /// Every skill, domain by domain.
    public static IReadOnlyList<string> All { get; } = [.. Groups.SelectMany(group => group.Skills)];

    /// What the person types to call the skill. The short /name may belong to Claude Code itself or
    /// to a skill of the person's own; the full name always reaches this one.
    public static string Call(string skill) => $"/{PluginName}:{skill}";
}
