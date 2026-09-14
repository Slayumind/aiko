using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;

namespace Aiko.App;

/// Frosted pictures of the four edges of one screen, taken when the island is picked up.
///
/// The landing strip can only ever lie along an edge, so a band along each edge is all it needs.
/// Taken once and frosted once: while the island is in hand the strip only picks the piece of the
/// band under itself.
sealed class GlassBands
{
    /// How far into the screen a band reaches. Wider than any island, in either direction.
    public const double Depth = 120;

    private static readonly ScreenEdge[] Edges = [ScreenEdge.Top, ScreenEdge.Bottom, ScreenEdge.Left, ScreenEdge.Right];

    private readonly Dictionary<ScreenEdge, (BitmapSource Picture, Box Band)> _bands;

    private GlassBands(Box work, Dictionary<ScreenEdge, (BitmapSource Picture, Box Band)> bands)
    {
        Work = work;
        _bands = bands;
    }

    public Box Work { get; }

    /// Pictures and frosts the edges on another thread: the two take about 50 ms together, and on the
    /// thread that moves the island that is a stutter in the hand.
    ///
    /// Work is in the units WPF places windows in, scale turns them into real pixels. The windows to
    /// leave out are Aiko's own, which would otherwise be blurred into the glass under them.
    public static async Task<GlassBands> TakeAsync(Box work, double scale, IReadOnlyList<nint> leaveOut)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var pictures = await Task.Run(() => ScreenCapture.Without(leaveOut, () => Edges
            .Select(edge => (Edge: edge, Band: BandOn(edge, work)))
            .Select(found => (found.Edge, found.Band, Picture: ScreenCapture.Take(
                (int)Math.Round(found.Band.X * scale),
                (int)Math.Round(found.Band.Y * scale),
                (int)Math.Round(found.Band.Width * scale),
                (int)Math.Round(found.Band.Height * scale))))
            .ToList()));
        var pictured = clock.ElapsedMilliseconds;

        var bands = await Task.Run(() => pictures
            .Where(found => found.Picture is not null)
            .ToDictionary(found => found.Edge, found => (Frost(found.Picture!, scale), found.Band)));

        Log.Write($"glass pictured in {pictured} ms, frosted in {clock.ElapsedMilliseconds - pictured} ms");
        return new GlassBands(work, bands);
    }

    /// The frosted picture under a strip on this edge, as a brush cut to the strip's own place.
    public ImageBrush? BrushFor(ScreenEdge edge, Box strip)
    {
        if (!_bands.TryGetValue(edge, out var found))
        {
            return null;
        }

        // The picture is smaller than the band it shows; this many of its pixels per unit of the band.
        var perUnit = found.Picture.PixelWidth / found.Band.Width;
        return new ImageBrush(found.Picture)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(
                (strip.X - found.Band.X) * perUnit,
                (strip.Y - found.Band.Y) * perUnit,
                strip.Width * perUnit,
                strip.Height * perUnit),
            Stretch = Stretch.Fill,
        };
    }

    /// The frosted picture of one edge, for --try-strip to save and look at.
    public BitmapSource? PictureOn(ScreenEdge edge) => _bands.TryGetValue(edge, out var found) ? found.Picture : null;

    /// Frozen, so the thread that made it can hand it to the one that draws.
    private static BitmapSource Frost(GlassImage picture, double scale)
    {
        var frosted = FrostedGlass.Frost(picture, GlassRecipe.Aiko, scale);
        var bitmap = BitmapSource.Create(frosted.Width, frosted.Height, 96, 96, PixelFormats.Bgra32, null, frosted.Pixels, frosted.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Box BandOn(ScreenEdge edge, Box work) => edge switch
    {
        ScreenEdge.Top => new Box(work.X, work.Y, work.Width, Depth),
        ScreenEdge.Bottom => new Box(work.X, work.Bottom - Depth, work.Width, Depth),
        ScreenEdge.Left => new Box(work.X, work.Y, Depth, work.Height),
        _ => new Box(work.Right - Depth, work.Y, Depth, work.Height),
    };
}
