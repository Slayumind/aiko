using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The picture of the taskbar button and the app id of the terminal window, from
/// spec/cases/taskbar-button (D-418).
public class TaskbarLookTests
{
    [Fact]
    public void Every_look_case_draws_the_same_way()
    {
        foreach (var row in SpecCases.Json("taskbar-button").GetProperty("looks").EnumerateArray())
        {
            var look = ButtonLook.For(
                row.GetProperty("pinned").GetBoolean(),
                row.GetProperty("face").GetBoolean(),
                Enum.Parse<BadgeKind>(row.GetProperty("badge").GetString()!));

            Assert.Equal(
                new ButtonLook(
                    Enum.Parse<ButtonCentre>(row.GetProperty("centre").GetString()!),
                    Enum.Parse<ButtonCorner>(row.GetProperty("corner").GetString()!)),
                look);
        }
    }

    [Fact]
    public void Every_identity_case_chooses_the_same_id()
    {
        var cases = SpecCases.Json("taskbar-button");
        var program = cases.GetProperty("program").GetString()!;
        foreach (var row in cases.GetProperty("identities").EnumerateArray())
        {
            var pins = row.GetProperty("pins").EnumerateArray()
                .Select(pin => new TaskbarPin(Text(pin.GetProperty("appId")), Text(pin.GetProperty("target"))));

            var (appId, pinned) = TaskbarIdentity.Choose(pins, program);

            var name = row.GetProperty("name").GetString();
            Assert.True(Text(row.GetProperty("appId")) == appId, $"{name}: app id {appId}");
            Assert.True(row.GetProperty("pinned").GetBoolean() == pinned, $"{name}: pinned {pinned}");
        }
    }

    [Fact]
    public void The_shortcut_id_is_the_one_Velopack_gives()
    {
        Assert.Equal("velopack.Slayumind.Aiko", TaskbarIdentity.Shortcut);
    }

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetString();
}
