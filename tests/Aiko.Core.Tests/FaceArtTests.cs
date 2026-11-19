using System.Text.RegularExpressions;

namespace Aiko.Core.Tests;

public class FaceArtTests
{
    public static TheoryData<AikoFace, FaceGround, bool> EveryFace()
    {
        var data = new TheoryData<AikoFace, FaceGround, bool>();
        foreach (var face in Enum.GetValues<AikoFace>())
        {
            foreach (var ground in Enum.GetValues<FaceGround>())
            {
                data.Add(face, ground, false);
                data.Add(face, ground, true);
            }
        }

        return data;
    }

    private static readonly HashSet<string> Palette =
        [FaceArt.Ink, FaceArt.Paper, FaceArt.Muted, FaceArt.Caution, FaceArt.Pink, FaceArt.Drop];

    // Only the commands the mockup used, with plain numbers: a comma for a decimal point from a
    // Russian Windows would break every face.
    private static readonly Regex PathData = new(@"^[MLHVCQTAZ0-9 .\-]+$");

    [Theory]
    [MemberData(nameof(EveryFace))]
    public void Every_face_is_drawn_in_the_palette_with_readable_path_data(AikoFace face, FaceGround ground, bool small)
    {
        var picture = FaceArt.Draw(face, ground, small);
        var shapes = picture.Groups.SelectMany(g => g.Shapes).ToList();

        Assert.NotEmpty(shapes);
        Assert.All(shapes, shape =>
        {
            Assert.Matches(PathData, shape.Data);
            Assert.All(shape.Holes ?? [], hole => Assert.Matches(PathData, hole));
            Assert.True(shape.Fill is not null || shape.Stroke is not null);
            Assert.True(shape.Fill is null || Palette.Contains(shape.Fill), shape.Fill);
            Assert.True(shape.Stroke is null || Palette.Contains(shape.Stroke), shape.Stroke);
            Assert.InRange(shape.Opacity, 0.1, 1);
        });
    }

    [Fact]
    public void The_chibi_head_has_an_outline_only_on_a_light_taskbar()
    {
        var dark = FaceArt.Draw(AikoFace.Fresh, FaceGround.Dark, small: true).Groups[0].Shapes[0];
        var light = FaceArt.Draw(AikoFace.Fresh, FaceGround.Light, small: true).Groups[0].Shapes[0];

        Assert.Null(dark.Stroke);
        Assert.Equal(FaceArt.Ink, light.Stroke);
        Assert.Equal(3.26, light.StrokeWidth);
    }

    [Fact]
    public void A_small_chibi_uses_thicker_lines_and_a_tighter_view()
    {
        var big = FaceArt.Draw(AikoFace.Done, FaceGround.Dark, small: false);
        var small = FaceArt.Draw(AikoFace.Done, FaceGround.Dark, small: true);

        Assert.Equal((0, 0, 64), (big.ViewLeft, big.ViewTop, big.ViewSize));
        Assert.Equal((3, 1, 59), (small.ViewLeft, small.ViewTop, small.ViewSize));
        Assert.True(small.Groups[1].Shapes[0].StrokeWidth > big.Groups[1].Shapes[0].StrokeWidth);
    }

    [Theory]
    [InlineData(AikoFace.Tired, FaceArt.Drop)]
    [InlineData(AikoFace.Error, FaceArt.Drop)]
    [InlineData(AikoFace.Waiting, FaceArt.Caution)]
    public void Signs_sit_outside_the_face_group(AikoFace face, string color)
    {
        var picture = FaceArt.Draw(face, FaceGround.Dark, small: true);

        Assert.Contains(picture.Groups[^1].Shapes, s => s.Fill == color);
        Assert.DoesNotContain(picture.Groups[1].Shapes, s => s.Fill == color);
    }
}
