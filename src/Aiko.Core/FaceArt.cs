using System.Globalization;

namespace Aiko.Core;

/// What the face sits on: the dark taskbar and Aiko's windows, or a light taskbar.
public enum FaceGround
{
    Dark,
    Light,
}

/// One filled or stroked outline. Data is SVG path data, which WPF reads as it is. Holes are cut out
/// of the fill, so a highlight stays transparent on any taskbar.
public sealed record FaceShape(
    string Data,
    string? Fill = null,
    string? Stroke = null,
    double StrokeWidth = 0,
    double Opacity = 1,
    IReadOnlyList<string>? Holes = null);

/// Shapes that share one transform: scaled around a centre, then moved.
public sealed record FaceGroup(
    IReadOnlyList<FaceShape> Shapes,
    double Scale = 1,
    double CenterX = 0,
    double CenterY = 0,
    double OffsetX = 0,
    double OffsetY = 0);

/// A face in a square view box, drawn in order.
public sealed record FacePicture(double ViewLeft, double ViewTop, double ViewSize, IReadOnlyList<FaceGroup> Groups);

/// Aiko's two faces as shapes (D-213, D-214, D-215): the chibi mochi head and the floating emoji.
/// A port of the accepted mockup generators, with the same numbers, so the app and the mockup match.
/// The core knows no WPF; the app turns these shapes into a drawing.
public static class FaceArt
{
    /// The palette of D-213: the eight colours of DESIGN plus the blue drop.
    public const string Ink = "#0A0A0A";
    public const string Paper = "#FAFAFA";
    public const string Muted = "#A1A1A1";
    public const string Caution = "#FE9A00";
    public const string Pink = "#FF6467";
    public const string Drop = "#33A9EE";

    /// A darker caution for the emoji on a light taskbar, where #FE9A00 is too pale to read.
    public const string CautionOnLight = "#D97F00";

    /// Below this size the chibi draws with thicker lines and a tighter view box.
    public const double SmallUpTo = 48;

    public static FacePicture Draw(FaceStyle style, AikoFace face, FaceGround ground, bool small) =>
        style == FaceStyle.Emoji ? Emoji.Draw(face, ground) : Chibi.Draw(face, ground, small);

    private static string N(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    private static string Circle(double x, double y, double r) =>
        $"M{N(x - r)} {N(y)} A{N(r)} {N(r)} 0 1 0 {N(x + r)} {N(y)} A{N(r)} {N(r)} 0 1 0 {N(x - r)} {N(y)} Z";

    private static string Ellipse(double x, double y, double rx, double ry) =>
        $"M{N(x - rx)} {N(y)} A{N(rx)} {N(ry)} 0 1 0 {N(x + rx)} {N(y)} A{N(rx)} {N(ry)} 0 1 0 {N(x - rx)} {N(y)} Z";

    private static string RoundRect(double x, double y, double w, double h, double r) =>
        $"M{N(x + r)} {N(y)} H{N(x + w - r)} A{N(r)} {N(r)} 0 0 1 {N(x + w)} {N(y + r)} V{N(y + h - r)} " +
        $"A{N(r)} {N(r)} 0 0 1 {N(x + w - r)} {N(y + h)} H{N(x + r)} A{N(r)} {N(r)} 0 0 1 {N(x)} {N(y + h - r)} " +
        $"V{N(y + r)} A{N(r)} {N(r)} 0 0 1 {N(x + r)} {N(y)} Z";

    private static FaceShape Line(string data, double width, string color, double opacity = 1) =>
        new(data, Stroke: color, StrokeWidth: Math.Round(width, 2), Opacity: opacity);

    private static class Chibi
    {
        private const string Head = "M32 7 C48 7 58 18.5 58 34 C58 49 46.5 58 32 58 C17.5 58 6 49 6 34 C6 18.5 16 7 32 7 Z";
        private const double L = 22;
        private const double R = 42;

        public static FacePicture Draw(AikoFace face, FaceGround ground, bool small)
        {
            // Thicker lines at 16 to 32 px, or they vanish.
            var k = small ? 1.45 : 1;
            var light = ground == FaceGround.Light;
            var shapes = new List<FaceShape>();

            // On a dark taskbar the white head needs no outline (D-214).
            var head = light
                ? new FaceShape(Head, Fill: Paper, Stroke: Ink, StrokeWidth: Math.Round(2.25 * k, 2))
                : new FaceShape(Head, Fill: Paper);

            var extras = new List<FaceShape>();
            switch (face)
            {
                case AikoFace.Fresh:
                    Brows(shapes, "soft", k);
                    Dome(shapes, L, k);
                    Dome(shapes, R, k);
                    shapes.Add(Line("M26.8 46 Q29.4 49.6 32 46.6 Q34.6 49.6 37.2 46", 1.7 * k, Ink));
                    Blush(shapes, 0.5);
                    shapes.Add(Line("M29.4 42.2 L30.4 40.8 M31.6 42.2 L32.6 40.8 M33.8 42.2 L34.8 40.8", 0.9 * k, Pink, 0.7));
                    break;

                case AikoFace.Tired:
                    Brows(shapes, "sad", k);
                    Lidded(shapes, L, 36.2, -1.6, k);
                    Lidded(shapes, R, 36.2, 1.6, k);
                    shapes.Add(Line($"M{N(L - 4.2)} 44.6 Q{N(L)} 46 {N(L + 4.2)} 44.6", 1 * k, Muted));
                    shapes.Add(Line($"M{N(R - 4.2)} 44.6 Q{N(R)} 46 {N(R + 4.2)} 44.6", 1 * k, Muted));
                    shapes.Add(Line("M26.8 48 Q28.2 46.2 29.6 48 T32.4 48 T35.2 48 T37.4 47.8", 1.5 * k, Ink));
                    Blush(shapes, 0.35);
                    extras.Add(DropShape());
                    break;

                case AikoFace.Asleep:
                    Brows(shapes, "soft", k);
                    shapes.Add(Line($"M{N(L - 5)} 36.2 Q{N(L)} 40.6 {N(L + 5)} 36.2", 1.9 * k, Ink));
                    shapes.Add(Line($"M{N(R - 5)} 36.2 Q{N(R)} 40.6 {N(R + 5)} 36.2", 1.9 * k, Ink));
                    shapes.Add(new FaceShape(Ellipse(32, 47.4, 1.7, 2), Fill: Ink));
                    Blush(shapes, 0.55);
                    break;

                case AikoFace.Working:
                    Brows(shapes, "focused", k);
                    Dome(shapes, L, k, 36.4, 0.4, 1.2);
                    Dome(shapes, R, k, 36.4, 0.4, 1.2);
                    shapes.Add(Line("M26.4 46.4 L35.4 47", 1.7 * k, Ink));
                    shapes.Add(new FaceShape("M31.4 46.8 L31.3 49.8 Q33.8 54.4 36.4 50 L36.4 47.1 Z", Fill: Pink, Stroke: Ink, StrokeWidth: Math.Round(1.1 * k, 2)));
                    Blush(shapes, 0.4);
                    extras.Add(Line("M55.5 7.5 L59.1 3.3 M57.5 13.1 L62.1 11.7 M51.3 5.1 L51.9 1.3", Math.Min(2.6, 1.9 * k), light ? Ink : Paper));
                    break;

                case AikoFace.Waiting:
                    Brows(shapes, "raised", k);
                    RoundEye(shapes, L, k);
                    RoundEye(shapes, R, k);
                    shapes.Add(new FaceShape(Ellipse(32, 48, 2.7, 3.3), Fill: Pink));
                    Blush(shapes, 0.5);
                    extras.Add(new FaceShape(RoundRect(53.5, 1.5, 5, 13, 2.5), Fill: Caution));
                    extras.Add(new FaceShape(Circle(56, 19.4, 2.8), Fill: Caution));
                    break;

                case AikoFace.Done:
                    Brows(shapes, "soft", k);
                    shapes.Add(Line($"M{N(L - 5)} 38 Q{N(L)} 31.2 {N(L + 5)} 38", 2.1 * k, Ink));
                    shapes.Add(Line($"M{N(R - 5)} 38 Q{N(R)} 31.2 {N(R + 5)} 38", 2.1 * k, Ink));
                    shapes.Add(new FaceShape("M25.6 44 L38.4 44 Q38.4 53.6 32 53.6 Q25.6 53.6 25.6 44 Z", Fill: Ink));
                    shapes.Add(new FaceShape("M28.6 51 Q32 48 35.4 51 Q34 53.2 32 53.2 Q30 53.2 28.6 51 Z", Fill: Pink));
                    Blush(shapes, 0.55);
                    break;

                default:
                    Brows(shapes, "sad", k);
                    shapes.Add(Line("M18 31.6 L25 35 L18 38.4", 2.2 * k, Ink));
                    shapes.Add(Line("M46 31.6 L39 35 L46 38.4", 2.2 * k, Ink));
                    shapes.Add(new FaceShape("M26 51.6 Q26 43.4 32 43.4 Q38 43.4 38 51.6 Q32 49.6 26 51.6 Z", Fill: Ink));
                    shapes.Add(new FaceShape("M28.4 50.9 Q32 47.6 35.6 50.9 Q32 49.9 28.4 50.9 Z", Fill: Pink));
                    extras.Add(DropShape());
                    break;
            }

            return new FacePicture(
                small ? 3 : 0,
                small ? 1 : 0,
                small ? 59 : 64,
                [new FaceGroup([head]), new FaceGroup(shapes, Scale: 1.1, CenterX: 32, CenterY: 40), new FaceGroup(extras)]);
        }

        private static FaceShape DropShape() =>
            new("M51.5 10 C45.8 18.4 45.8 24.6 51.5 24.6 C57.2 24.6 57.2 18.4 51.5 10 Z", Fill: Drop);

        private static void Brows(List<FaceShape> shapes, string kind, double k)
        {
            foreach (var (x, side) in new[] { (L, -1.0), (R, 1.0) })
            {
                string O(double dx) => N(x + side * dx);
                var data = kind switch
                {
                    "soft" => $"M{O(-4.6)} 26.2 Q{O(0)} 22.6 {O(4.8)} 25.4",
                    "sad" => $"M{O(-4.6)} 23.2 Q{O(-0.6)} 25.8 {O(4.8)} 26.6",
                    "raised" => $"M{O(-4.8)} 23 Q{O(0)} 18.2 {O(4.8)} 22.2",
                    _ => $"M{O(-4.8)} 26.4 Q{O(0)} 24 {O(5)} 23.6",
                };
                shapes.Add(Line(data, 1.5 * k, Ink));
            }
        }

        private static void Highlight(List<FaceShape> shapes, double x, double y, double r) =>
            shapes.Add(new FaceShape(Circle(x, y, r), Fill: Paper));

        /// A round top, a flat base, a big highlight and a small one.
        private static void Dome(List<FaceShape> shapes, double x, double k, double y = 36, double gx = 0, double gy = 0)
        {
            shapes.Add(new FaceShape(
                $"M{N(x - 6)} {N(y + 3.2)} C{N(x - 6.2)} {N(y - 6.8)} {N(x + 6.2)} {N(y - 6.8)} {N(x + 6)} {N(y + 3.2)} Q{N(x)} {N(y + 4)} {N(x - 6)} {N(y + 3.2)} Z",
                Fill: Ink));
            Highlight(shapes, x + 1.8 + gx, y - 1.6 + gy, Math.Min(2.6, 2 * k));
            Highlight(shapes, x - 2.6 + gx, y + 1.4 + gy, Math.Min(1.1, 0.8 * k));
        }

        private static void RoundEye(List<FaceShape> shapes, double x, double k, double y = 35.5)
        {
            shapes.Add(new FaceShape(Circle(x, y, 5.6), Fill: Ink));
            Highlight(shapes, x + 1.6, y - 1.8, Math.Min(2.5, 1.9 * k));
            Highlight(shapes, x - 2, y + 2, Math.Min(1.1, 0.8 * k));
        }

        private static void Lidded(List<FaceShape> shapes, double x, double y, double tilt, double k)
        {
            shapes.Add(new FaceShape(
                $"M{N(x - 5.2)} {N(y - tilt)} L{N(x + 5.2)} {N(y + tilt)} C{N(x + 5)} {N(y + 6.4)} {N(x - 5)} {N(y + 6.4)} {N(x - 5.2)} {N(y - tilt)} Z",
                Fill: Ink));
            shapes.Add(Line($"M{N(x - 6.2)} {N(y - tilt - 0.2)} L{N(x + 6.2)} {N(y + tilt + 0.2)}", 1.9 * k, Ink));
            Highlight(shapes, x + 1.2, y + 2.6, Math.Min(1.4, 1.1 * k));
        }

        private static void Blush(List<FaceShape> shapes, double opacity)
        {
            shapes.Add(new FaceShape(Ellipse(L - 4.4, 45, 4.4, 2.5), Fill: Pink, Opacity: opacity));
            shapes.Add(new FaceShape(Ellipse(R + 4.4, 45, 4.4, 2.5), Fill: Pink, Opacity: opacity));
        }
    }

    private static class Emoji
    {
        private const double LX = 18;
        private const double RX = 46;

        public static FacePicture Draw(AikoFace face, FaceGround ground)
        {
            var light = ground == FaceGround.Light;
            var ink = light ? Ink : Paper;
            var caution = light ? CautionOnLight : Caution;

            // Pink on a dark taskbar looks weaker, so the blush is stronger there.
            var blushK = light ? 1 : 1.55;

            var shapes = new List<FaceShape>();
            var extras = new List<FaceShape>();

            switch (face)
            {
                case AikoFace.Fresh:
                    Brows(shapes, "soft", ink);
                    Dome(shapes, LX, 29, ink);
                    Dome(shapes, RX, 29, ink);
                    shapes.Add(Line("M24.6 44 Q28.3 49.4 32 44.8 Q35.7 49.4 39.4 44", 3.4, ink));
                    Blush(shapes, blushK);
                    break;

                case AikoFace.Tired:
                    Brows(shapes, "sad", ink);
                    Lidded(shapes, LX, 28.6, -1.8, ink);
                    Lidded(shapes, RX, 28.6, 1.8, ink);
                    shapes.Add(Line("M25 47 Q27.4 44.4 29.8 47 T34.6 47 T39.2 46.6", 3.2, ink));
                    Blush(shapes, blushK, 0.35);
                    extras.Add(DropShape());
                    break;

                case AikoFace.Asleep:
                    Brows(shapes, "soft", ink);
                    shapes.Add(Line($"M{N(LX - 7)} 28.4 Q{N(LX)} 35.4 {N(LX + 7)} 28.4", 3.6, ink));
                    shapes.Add(Line($"M{N(RX - 7)} 28.4 Q{N(RX)} 35.4 {N(RX + 7)} 28.4", 3.6, ink));
                    shapes.Add(new FaceShape(Ellipse(32, 46, 2.6, 3), Fill: ink));
                    Blush(shapes, blushK, 0.5);
                    break;

                case AikoFace.Working:
                    Brows(shapes, "focused", ink);
                    Dome(shapes, LX, 30, ink, 0.6, 1.6);
                    Dome(shapes, RX, 30, ink, 0.6, 1.6);
                    shapes.Add(new FaceShape("M31 45.8 L30.8 49.8 Q34.6 56.6 38.6 50.2 L38.6 46.6 Z", Fill: Pink));
                    shapes.Add(Line("M24 45.2 L37.6 46.4", 3.4, ink));
                    Blush(shapes, blushK, 0.4);
                    extras.Add(Line("M56 6 L59.6 1.8 M58 11.6 L62.6 10.2 M51.8 3.6 L52.4 -0.2", 2.8, ink));
                    break;

                case AikoFace.Waiting:
                    Brows(shapes, "raised", ink);
                    RoundEye(shapes, LX, 28.6, ink);
                    RoundEye(shapes, RX, 28.6, ink);
                    shapes.Add(new FaceShape(Ellipse(32, 46.4, 4.2, 5), Fill: Pink));
                    Blush(shapes, blushK, 0.5);
                    extras.Add(new FaceShape(RoundRect(56, 0.5, 6, 14, 3), Fill: caution));
                    extras.Add(new FaceShape(Circle(59, 20, 3.2), Fill: caution));
                    break;

                case AikoFace.Done:
                    Brows(shapes, "soft", ink);
                    shapes.Add(Line($"M{N(LX - 7)} 31 Q{N(LX)} 21.6 {N(LX + 7)} 31", 3.8, ink));
                    shapes.Add(Line($"M{N(RX - 7)} 31 Q{N(RX)} 21.6 {N(RX + 7)} 31", 3.8, ink));
                    shapes.Add(new FaceShape("M23.4 41 L40.6 41 Q40.6 54.2 32 54.2 Q23.4 54.2 23.4 41 Z", Fill: Pink));
                    Blush(shapes, blushK);
                    break;

                default:
                    Brows(shapes, "sad", ink);
                    shapes.Add(Line("M12.6 23.4 L22.6 28.4 L12.6 33.4", 3.6, ink));
                    shapes.Add(Line("M51.4 23.4 L41.4 28.4 L51.4 33.4", 3.6, ink));
                    shapes.Add(new FaceShape("M23.6 51.4 Q23.6 41.2 32 41.2 Q40.4 41.2 40.4 51.4 Q32 48.6 23.6 51.4 Z", Fill: Pink));
                    extras.Add(DropShape());
                    break;
            }

            return new FacePicture(0, 0, 64, [new FaceGroup(shapes, OffsetY: 3), new FaceGroup(extras)]);
        }

        private static FaceShape DropShape() =>
            new("M57.4 1 C51.4 9.8 51.4 16.2 57.4 16.2 C63.4 16.2 63.4 9.8 57.4 1 Z", Fill: Drop);

        private static void Brows(List<FaceShape> shapes, string kind, string ink)
        {
            foreach (var (x, side) in new[] { (LX, -1.0), (RX, 1.0) })
            {
                string O(double dx) => N(x + side * dx);
                var data = kind switch
                {
                    "soft" => $"M{O(-6)} 16.4 Q{O(0)} 11.8 {O(6)} 15.2",
                    "sad" => $"M{O(-6)} 12.6 Q{O(-0.8)} 16 {O(6)} 17",
                    "raised" => $"M{O(-6)} 12.4 Q{O(0)} 6.4 {O(6)} 11.4",
                    _ => $"M{O(-6.4)} 17 Q{O(0)} 13.8 {O(6.6)} 13.2",
                };
                shapes.Add(Line(data, 2.6, ink));
            }
        }

        private static void Dome(List<FaceShape> shapes, double x, double y, string ink, double gx = 0, double gy = 0) =>
            shapes.Add(new FaceShape(
                $"M{N(x - 7)} {N(y + 3.6)} C{N(x - 7.2)} {N(y - 8)} {N(x + 7.2)} {N(y - 8)} {N(x + 7)} {N(y + 3.6)} Q{N(x)} {N(y + 4.6)} {N(x - 7)} {N(y + 3.6)} Z",
                Fill: ink,
                Holes: [Circle(x + 2.2 + gx, y - 1.8 + gy, 2.6), Circle(x - 3 + gx, y + 1.6 + gy, 1.1)]));

        private static void RoundEye(List<FaceShape> shapes, double x, double y, string ink) =>
            shapes.Add(new FaceShape(Circle(x, y, 7), Fill: ink, Holes: [Circle(x + 2, y - 2.2, 2.9), Circle(x - 2.6, y + 2.4, 1.3)]));

        private static void Lidded(List<FaceShape> shapes, double x, double y, double tilt, string ink)
        {
            shapes.Add(new FaceShape(
                $"M{N(x - 6.6)} {N(y - tilt)} L{N(x + 6.6)} {N(y + tilt)} C{N(x + 6.2)} {N(y + 8)} {N(x - 6.2)} {N(y + 8)} {N(x - 6.6)} {N(y - tilt)} Z",
                Fill: ink,
                Holes: [Circle(x + 1.6, y + 3.2, 1.7)]));
            shapes.Add(Line($"M{N(x - 8)} {N(y - tilt - 0.3)} L{N(x + 8)} {N(y + tilt + 0.3)}", 3.2, ink));
        }

        private static void Blush(List<FaceShape> shapes, double blushK, double opacity = 0.55)
        {
            var alpha = Math.Round(Math.Min(0.9, opacity * blushK), 2);
            shapes.Add(new FaceShape(Ellipse(LX - 6, 39, 5.4, 3.2), Fill: Pink, Opacity: alpha));
            shapes.Add(new FaceShape(Ellipse(RX + 6, 39, 5.4, 3.2), Fill: Pink, Opacity: alpha));
        }
    }
}
