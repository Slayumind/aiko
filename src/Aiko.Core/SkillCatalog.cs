namespace Aiko.Core;

/// Aiko's own skills, in the order the settings page lists them. Each one ships as its own plugin
/// with the same name (D-202).
public static class SkillCatalog
{
    public static IReadOnlyList<string> All { get; } =
    [
        "aiko-copy",
        "aiko-release-gate",
        "aiko-docs-hygiene",
        "aiko-blender-to-unity",
        "aiko-texturing",
        "aiko-glb-for-web",
        "aiko-palette",
        "aiko-gamedesign-research",
    ];

    public static int OnCount(PersonaSettings persona) => All.Count(persona.IsSkillOn);
}
