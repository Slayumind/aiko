using System.Windows;
using Aiko.Core;

namespace Aiko.App;

/// --try-island: drags the island with the real mouse from the top of the screen to the left edge,
/// the way a person would, and checks where it lands. The island is a window of its own above
/// everything, so the mouse only ever touches it. Nothing is saved.
static class IslandCheck
{
    public static int Run()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var island = new IslandWindow();
        var moved = false;
        island.Moved += _ => moved = true;

        var result = 1;
        island.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(400);
                result = await Walk(island, () => moved) ? 0 : 1;
            }
            catch (Exception failed)
            {
                Log.Write($"--try-island: {failed.GetType().Name}: {failed.Message}");
            }
            finally
            {
                application.Shutdown();
            }
        };

        island.Show(CardSnapshot.Example(), IslandPosition.Default);
        application.Run();
        return result;
    }

    /// --try-strip: only shows the landing strip at the top of the screen for three seconds, to look
    /// at the glass. The mouse is left alone.
    public static int ShowStrip()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var work = SystemParameters.WorkArea;
        var strip = new IslandGhost();
        strip.PlaceOn(new Box(work.X + (work.Width / 2) - 120, work.Y, 240, 60), ScreenEdge.Top);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => application.Shutdown();
        timer.Start();
        application.Run();
        return 0;
    }

    private static async Task<bool> Walk(IslandWindow island, Func<bool> moved)
    {
        var scale = PresentationSource.FromVisual(island)!.CompositionTarget!.TransformToDevice.M11;
        var start = island.PointToScreen(new Point(island.ActualWidth / 2, island.ActualHeight / 2));
        var work = SystemParameters.WorkArea;

        // To the left edge, a third of the way down the screen.
        var end = new Point((work.X + 30) * scale, (work.Y + (work.Height / 3)) * scale);

        MouseInput.MoveTo(start);
        MouseInput.Down();
        var halfway = false;
        const int steps = 40;
        for (var i = 1; i <= steps; i++)
        {
            MouseInput.MoveTo(new Point(start.X + ((end.X - start.X) * i / steps), start.Y + ((end.Y - start.Y) * i / steps)));
            await Task.Delay(15);
            if (i == steps)
            {
                halfway = Application.Current.Windows.OfType<IslandGhost>().Any(ghost => ghost.IsVisible);
            }
        }

        // A moment with the island in hand, so a screenshot can catch the landing strip.
        await Task.Delay(1500);
        var columnInHand = island.ActualHeight > island.ActualWidth;
        MouseInput.Up();
        await Task.Delay(700);

        var landedLeft = Math.Abs(island.Left - work.X) < 1;
        var column = island.ActualHeight > island.ActualWidth;
        var ghostGone = !Application.Current.Windows.OfType<IslandGhost>().Any();

        Log.Write($"--try-island: strip shown in hand={halfway}, column already in hand={columnInHand}, landed on the left={landedLeft} at {island.Left:0},{island.Top:0}, rings in a column={column}, strip gone={ghostGone}, moved raised={moved()}");
        return halfway && columnInHand && landedLeft && column && ghostGone && moved();
    }
}
