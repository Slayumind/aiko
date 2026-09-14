namespace Aiko.Core;

/// The numbers of the glass: how far it blurs, how much it lifts colour and light, and how the
/// picture behind it bends. Measured in the units WPF places windows in; the capture comes in real
/// pixels and the scale converts.
///
/// Aiko's glass follows the owner's reference, the liquid glass of ui-layouts: blur 24, no tint, and
/// the blurred picture bent by low-frequency noise (baseFrequency 0.003 0.007, displacement scale
/// 200), which is what makes it read as liquid rather than frosted.
public sealed record GlassRecipe(
    double BlurRadius,
    double Saturation,
    double Lift,
    int Downscale,
    double BendScale = 0,
    double BendFrequencyX = 0.003,
    double BendFrequencyY = 0.007)
{
    public static readonly GlassRecipe Aiko = new(BlurRadius: 24, Saturation: 1, Lift: 0, Downscale: 4, BendScale: 200);
}

/// A picture in BGRA, 8 bits a channel, rows from the top.
public sealed record GlassImage(byte[] Pixels, int Width, int Height);

/// Turns a picture of the screen into glass: smaller, blurred, toned, and bent like liquid.
///
/// Aiko cannot blur the live screen (RESEARCH, glass spikes), so it takes a picture under the
/// landing strip when the island is picked up and frosts that once.
public static class FrostedGlass
{
    /// Three box blurs in a row come close to a Gaussian blur, and each costs the same whatever the
    /// radius.
    private const int BlurPasses = 3;

    public static GlassImage Frost(GlassImage screen, GlassRecipe recipe, double scale)
    {
        var small = Shrink(screen, Math.Max(1, recipe.Downscale));

        // The radius spreads over the passes: three boxes of r reach about as far as one Gaussian of r * sqrt(3).
        var radius = (int)Math.Round(recipe.BlurRadius * scale / Math.Max(1, recipe.Downscale) / Math.Sqrt(BlurPasses));
        for (var pass = 0; pass < BlurPasses && radius > 0; pass++)
        {
            BoxBlur(small, radius, horizontal: true);
            BoxBlur(small, radius, horizontal: false);
        }

        Tone(small, recipe.Saturation, recipe.Lift);

        var perUnit = scale / Math.Max(1, recipe.Downscale);
        return recipe.BendScale > 0
            ? Bend(small, recipe.BendScale * perUnit, recipe.BendFrequencyX / perUnit, recipe.BendFrequencyY / perUnit)
            : small;
    }

    /// Moves every pixel by smooth noise, as SVG feDisplacementMap does with feTurbulence: each pixel
    /// shows the one up to half the scale away, in a direction that changes slowly across the picture.
    /// Frequencies are waves per pixel. The noise is the same every time, so the glass does not
    /// flicker between two drags.
    public static GlassImage Bend(GlassImage image, double scale, double frequencyX, double frequencyY)
    {
        var bent = new byte[image.Pixels.Length];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var dx = (SmoothNoise(x * frequencyX, y * frequencyY, 11) - 0.5) * scale;
                var dy = (SmoothNoise(x * frequencyX, y * frequencyY, 29) - 0.5) * scale;
                var sx = Math.Clamp((int)Math.Round(x + dx), 0, image.Width - 1);
                var sy = Math.Clamp((int)Math.Round(y + dy), 0, image.Height - 1);

                var from = ((sy * image.Width) + sx) * 4;
                var to = ((y * image.Width) + x) * 4;
                bent[to] = image.Pixels[from];
                bent[to + 1] = image.Pixels[from + 1];
                bent[to + 2] = image.Pixels[from + 2];
                bent[to + 3] = image.Pixels[from + 3];
            }
        }

        return new GlassImage(bent, image.Width, image.Height);
    }

    /// Value noise between 0 and 1: random values on a grid of whole numbers, blended smoothly between them.
    public static double SmoothNoise(double x, double y, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = Fade(x - x0);
        var ty = Fade(y - y0);

        var top = Lerp(Lattice(x0, y0, seed), Lattice(x0 + 1, y0, seed), tx);
        var bottom = Lerp(Lattice(x0, y0 + 1, seed), Lattice(x0 + 1, y0 + 1, seed), tx);
        return Lerp(top, bottom, ty);
    }

    private static double Fade(double t) => t * t * (3 - (2 * t));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double Lattice(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)((x * 374761393) + (y * 668265263) + (seed * 982451653));
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return h / (double)uint.MaxValue;
        }
    }

    /// Averages each square of factor by factor pixels into one. Alpha comes out opaque: a picture
    /// of the screen has no see-through parts, and BitBlt leaves the alpha at zero.
    public static GlassImage Shrink(GlassImage image, int factor)
    {
        var width = Math.Max(1, image.Width / factor);
        var height = Math.Max(1, image.Height / factor);
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0, count = 0;
                for (var dy = 0; dy < factor && (y * factor) + dy < image.Height; dy++)
                {
                    var row = ((y * factor) + dy) * image.Width;
                    for (var dx = 0; dx < factor && (x * factor) + dx < image.Width; dx++)
                    {
                        var i = (row + (x * factor) + dx) * 4;
                        b += image.Pixels[i];
                        g += image.Pixels[i + 1];
                        r += image.Pixels[i + 2];
                        count++;
                    }
                }

                var o = ((y * width) + x) * 4;
                pixels[o] = (byte)(b / count);
                pixels[o + 1] = (byte)(g / count);
                pixels[o + 2] = (byte)(r / count);
                pixels[o + 3] = 255;
            }
        }

        return new GlassImage(pixels, width, height);
    }

    /// A running average over 2r+1 pixels along one direction. The edges repeat the last pixel, so
    /// the glass does not darken towards the ends of the screen.
    public static void BoxBlur(GlassImage image, int radius, bool horizontal)
    {
        var length = horizontal ? image.Width : image.Height;
        var lines = horizontal ? image.Height : image.Width;
        var window = (2 * radius) + 1;
        var source = new int[length * 3];

        for (var line = 0; line < lines; line++)
        {
            for (var i = 0; i < length; i++)
            {
                var p = Index(image, line, i, horizontal);
                source[i * 3] = image.Pixels[p];
                source[(i * 3) + 1] = image.Pixels[p + 1];
                source[(i * 3) + 2] = image.Pixels[p + 2];
            }

            for (var channel = 0; channel < 3; channel++)
            {
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += source[(Math.Clamp(k, 0, length - 1) * 3) + channel];
                }

                for (var i = 0; i < length; i++)
                {
                    image.Pixels[Index(image, line, i, horizontal) + channel] = (byte)(sum / window);
                    sum += source[(Math.Min(i + radius + 1, length - 1) * 3) + channel];
                    sum -= source[(Math.Max(i - radius, 0) * 3) + channel];
                }
            }
        }
    }

    /// Saturation pushes each colour away from its own grey; lift moves every channel a share of the
    /// way towards white.
    public static void Tone(GlassImage image, double saturation, double lift)
    {
        var pixels = image.Pixels;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            var grey = (0.0722 * b) + (0.7152 * g) + (0.2126 * r);

            pixels[i] = Channel(grey + ((b - grey) * saturation), lift);
            pixels[i + 1] = Channel(grey + ((g - grey) * saturation), lift);
            pixels[i + 2] = Channel(grey + ((r - grey) * saturation), lift);
        }
    }

    private static byte Channel(double value, double lift)
    {
        var clamped = Math.Clamp(value, 0, 255);
        return (byte)Math.Round(clamped + ((255 - clamped) * lift));
    }

    private static int Index(GlassImage image, int line, int along, bool horizontal) =>
        (horizontal ? (line * image.Width) + along : (along * image.Width) + line) * 4;
}
