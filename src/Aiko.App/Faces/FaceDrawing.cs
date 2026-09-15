using System.Windows;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// Turns the shapes of Aiko.Core.FaceArt into a WPF drawing. Vector all the way, so one face serves
/// a 16 px tray icon and a 44 px picture in a window.
static class FaceDrawing
{
    public static DrawingImage For(FaceStyle style, AikoFace face, FaceGround ground, double pixels) =>
        Image(FaceArt.Draw(style, face, ground, small: pixels <= FaceArt.SmallUpTo));

    public static DrawingImage Image(FacePicture picture)
    {
        var view = new Rect(picture.ViewLeft, picture.ViewTop, picture.ViewSize, picture.ViewSize);
        var root = new DrawingGroup { ClipGeometry = new RectangleGeometry(view) };

        // An empty square as big as the view box: without it the image shrinks to what is drawn,
        // and every face would sit at a different size and place.
        root.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(view)));

        foreach (var group in picture.Groups)
        {
            var drawings = new DrawingGroup();
            if (group.Scale != 1 || group.OffsetX != 0 || group.OffsetY != 0)
            {
                drawings.Transform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(group.Scale, group.Scale, group.CenterX, group.CenterY),
                        new TranslateTransform(group.OffsetX, group.OffsetY),
                    },
                };
            }

            foreach (var shape in group.Shapes)
            {
                drawings.Children.Add(Drawing(shape));
            }

            root.Children.Add(drawings);
        }

        var image = new DrawingImage(root);
        image.Freeze();
        return image;
    }

    private static GeometryDrawing Drawing(FaceShape shape)
    {
        // F1 is the non-zero fill rule, the one SVG uses. WPF's own default is even-odd.
        Geometry geometry = Geometry.Parse("F1 " + shape.Data);
        if (shape.Holes is { Count: > 0 } holes)
        {
            var cut = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var hole in holes)
            {
                cut.Children.Add(Geometry.Parse("F1 " + hole));
            }

            geometry = new CombinedGeometry(GeometryCombineMode.Exclude, geometry, cut);
        }

        var fill = shape.Fill is null ? null : Brush(shape.Fill, shape.Opacity);
        var pen = shape.Stroke is null
            ? null
            : new Pen(Brush(shape.Stroke, shape.Opacity), shape.StrokeWidth)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };

        return new GeometryDrawing(fill, pen, geometry);
    }

    private static SolidColorBrush Brush(string hex, double opacity)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }
}
