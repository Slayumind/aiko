using Aiko.Core;

namespace Aiko.Core.Tests;

public class FrostedGlassTests
{
    private static GlassImage Solid(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
        }

        return new GlassImage(pixels, width, height);
    }

    private static (byte B, byte G, byte R, byte A) At(GlassImage image, int x, int y)
    {
        var i = ((y * image.Width) + x) * 4;
        return (image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2], image.Pixels[i + 3]);
    }

    [Fact]
    public void One_colour_stays_that_colour_through_the_blur()
    {
        var glass = FrostedGlass.Frost(Solid(64, 32, 40, 90, 160), new GlassRecipe(30, 1, 0, 4), scale: 1.5);

        Assert.Equal(16, glass.Width);
        Assert.Equal(8, glass.Height);
        Assert.All(Enumerable.Range(0, glass.Width), x => Assert.Equal((40, 90, 160, 255), At(glass, x, 4)));
    }

    [Fact]
    public void The_blur_spreads_a_bright_line_to_its_neighbours()
    {
        var image = Solid(40, 1, 0, 0, 0);
        for (var c = 0; c < 3; c++)
        {
            image.Pixels[(20 * 4) + c] = 255;
        }

        FrostedGlass.BoxBlur(image, radius: 2, horizontal: true);

        Assert.Equal(51, At(image, 20, 0).R);
        Assert.Equal(51, At(image, 18, 0).R);
        Assert.Equal(0, At(image, 17, 0).R);
    }

    [Fact]
    public void The_ends_of_the_screen_do_not_turn_dark()
    {
        var image = Solid(10, 1, 200, 200, 200);

        FrostedGlass.BoxBlur(image, radius: 4, horizontal: true);

        Assert.Equal(200, At(image, 0, 0).R);
        Assert.Equal(200, At(image, 9, 0).R);
    }

    [Fact]
    public void Saturation_pushes_colours_apart_and_leaves_grey_alone()
    {
        var colour = Solid(1, 1, 100, 120, 160);
        var grey = Solid(1, 1, 128, 128, 128);

        FrostedGlass.Tone(colour, saturation: 1.5, lift: 0);
        FrostedGlass.Tone(grey, saturation: 1.5, lift: 0);

        var (b, _, r, _) = At(colour, 0, 0);
        Assert.True(r - b > 60);
        Assert.Equal((128, 128, 128, 0), At(grey, 0, 0));
    }

    [Fact]
    public void Bending_by_nothing_changes_nothing_and_one_colour_stays_one_colour()
    {
        var stripes = Solid(40, 10, 0, 0, 0);
        for (var i = 0; i < stripes.Pixels.Length; i += 4 * 3)
        {
            stripes.Pixels[i + 2] = 255;
        }

        Assert.Equal(stripes.Pixels, FrostedGlass.Bend(stripes, scale: 0, 0.05, 0.05).Pixels);

        var solid = Solid(40, 10, 10, 20, 30);
        Assert.Equal(solid.Pixels, FrostedGlass.Bend(solid, scale: 50, 0.05, 0.05).Pixels);
    }

    [Fact]
    public void The_bend_moves_a_picture_and_does_it_the_same_way_each_time()
    {
        var ramp = Solid(200, 4, 0, 0, 0);
        for (var x = 0; x < 200; x++)
        {
            for (var y = 0; y < 4; y++)
            {
                ramp.Pixels[(((y * 200) + x) * 4) + 2] = (byte)x;
            }
        }

        var once = FrostedGlass.Bend(ramp, scale: 40, 0.02, 0.02);
        var again = FrostedGlass.Bend(ramp, scale: 40, 0.02, 0.02);

        Assert.NotEqual(ramp.Pixels, once.Pixels);
        Assert.Equal(once.Pixels, again.Pixels);
    }

    [Fact]
    public void The_noise_stays_between_nought_and_one_and_changes_smoothly()
    {
        for (var i = 0; i < 500; i++)
        {
            var x = i * 0.137;
            var here = FrostedGlass.SmoothNoise(x, x * 0.5, 3);
            var near = FrostedGlass.SmoothNoise(x + 0.01, x * 0.5, 3);

            Assert.InRange(here, 0, 1);
            Assert.True(Math.Abs(here - near) < 0.05);
        }
    }

    [Fact]
    public void Lift_moves_towards_white_by_its_share()
    {
        var image = Solid(1, 1, 0, 100, 255);

        FrostedGlass.Tone(image, saturation: 1, lift: 0.1);

        Assert.Equal((26, 116, 255, 0), At(image, 0, 0));
    }
}
