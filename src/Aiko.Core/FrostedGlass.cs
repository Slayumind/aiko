namespace Aiko.Core;

/// The numbers of the glass: how far it blurs, how much it lifts colour and light. Measured in the
/// units WPF places windows in; the capture comes in real pixels and the scale converts.
///
/// The recipe follows Fluent acrylic (blur 30, saturation 1.25, noise 2 %) and CSS glassmorphism,
/// which lifts saturation further. Blur alone greys colours out, and that is what made the first
/// glass look muddy.
public sealed record GlassRecipe(double BlurRadius, double Saturation, double Lift, int Downscale)
{
    public static readonly GlassRecipe Aiko = new(BlurRadius: 30, Saturation: 1.5, Lift: 0.05, Downscale: 4);
}

/// A picture in BGRA, 8 bits a channel, rows from the top.
public sealed record GlassImage(byte[] Pixels, int Width, int Height);

/// Turns a picture of the screen into frosted glass: smaller, blurred, more saturated, a little lighter.
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
        return small;
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
