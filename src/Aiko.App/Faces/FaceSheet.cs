using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// Every face on one picture, laid out like the sheet in the mockup: big, then 32, 24 and 16 px, on a
/// dark and a light taskbar. For --snapshot-faces.
static class FaceSheet
{
    public static void Write(string path)
    {
        var rows = new StackPanel();
        foreach (var style in Enum.GetValues<FaceStyle>())
        {
            foreach (var ground in Enum.GetValues<FaceGround>())
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                foreach (var face in Enum.GetValues<AikoFace>())
                {
                    row.Children.Add(Cell(style, face, ground));
                }

                rows.Children.Add(row);
            }
        }

        Snapshot.Write(rows, path);
    }

    private static Border Cell(FaceStyle style, AikoFace face, FaceGround ground)
    {
        var sizes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var size in new[] { 32, 24, 16 })
        {
            sizes.Children.Add(Picture(style, face, ground, size, new Thickness(3, 0, 3, 0)));
        }

        var cell = new StackPanel();
        cell.Children.Add(Picture(style, face, ground, 104, new Thickness(0)));
        cell.Children.Add(sizes);

        return new Border
        {
            Width = 128,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 2, 0),
            Background = new SolidColorBrush(ground == FaceGround.Dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xEE, 0xEE, 0xEE)),
            Child = cell,
        };
    }

    private static Image Picture(FaceStyle style, AikoFace face, FaceGround ground, int size, Thickness margin) =>
        new()
        {
            Source = FaceDrawing.For(style, face, ground, size),
            Width = size,
            Height = size,
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center,
        };
}
