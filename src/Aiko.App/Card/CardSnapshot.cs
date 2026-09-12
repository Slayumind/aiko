using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;

namespace Aiko.App;

/// Draws the card on made up numbers and writes it to a file. Started with --snapshot, so the
/// card can be looked at after every change without waiting for real limits to move.
static class CardSnapshot
{
    public static void Write(string path)
    {
        var now = DateTimeOffset.Now;
        var cards = new[]
        {
            Card("Personal", now, fiveHour: 42, sevenDay: 18, fiveHourLeft: 2.4, model: 61),
            Card("Work", now, fiveHour: 82, sevenDay: 64, fiveHourLeft: 1.1, model: null),
        };

        var panel = new CardPanel();
        panel.Show(CardModel.From(cards, now));

        // The card floats above the desktop, so the picture gets the panel background under it.
        // Otherwise the shadow would hang over nothing.
        var ground = new Border { Background = Tokens.Brush("Background"), Child = panel };
        ground.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ground.Arrange(new Rect(ground.DesiredSize));
        ground.UpdateLayout();

        // Twice the size: the card is small and the numbers have to be readable on the picture.
        const int scale = 2;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(ground.DesiredSize.Width * scale),
            (int)Math.Ceiling(ground.DesiredSize.Height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);
        bitmap.Render(ground);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static CardState Card(
        string name,
        DateTimeOffset now,
        int fiveHour,
        int sevenDay,
        double fiveHourLeft,
        int? model)
    {
        var windows = new List<LimitWindow>
        {
            new(LimitKind.FiveHour, fiveHour, now.AddHours(fiveHourLeft)),
            new(LimitKind.SevenDay, sevenDay, now.AddDays(3.5)),
        };

        var snapshot = new LimitSnapshot(name, LimitSource.StatusLine, now.AddMinutes(-2), windows)
        {
            Model = model is { } percent ? new ModelLimit("Fable", percent, now.AddDays(5)) : null,
        };

        return CardState.From(snapshot, now);
    }
}
