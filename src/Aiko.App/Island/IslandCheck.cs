using System.Windows;
using System.Windows.Media;
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

    /// --try-strip: only shows the landing strip on the bottom edge for three seconds, over a sample to see through,
    /// to look at the glass and at the taskbar cutting it. The mouse is left alone.
    public static int ShowStrip()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var work = SystemParameters.WorkArea;
        // Something to see through: over a black terminal any glass this clear is invisible.
        var sample = new System.Windows.Controls.StackPanel { Background = new LinearGradientBrush(Color.FromRgb(0x3B, 0x2A, 0x6B), Color.FromRgb(0x0E, 0x5A, 0x4F), 0) };
        for (var line = 0; line < 8; line++)
        {
            sample.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "PS C:\\projects\\aiko> claude --resume   ● Reading src/Aiko.App/Island/IslandGhost.cs",
                Foreground = line % 3 == 0 ? Brushes.Orange : Brushes.White,
                FontSize = 14,
                Margin = new Thickness(12, 2, 0, 2),
            });
        }

        var backdrop = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = work.X + (work.Width / 2) - 320,
            Top = work.Bottom - 220,
            Width = 640,
            Height = 220,
            Content = sample,
        };
        backdrop.Show();

        // Over the bottom edge, where the part past the edge has to hide under the taskbar.
        var frosted = new IslandGhost();
        frosted.PlaceOn(new Box(work.X + (work.Width / 2) - 120, work.Bottom - 60, 240, 60), ScreenEdge.Bottom);

        // The strip sits just under the taskbar, and a window shown later lands above it, so the
        // sample goes under the strip by hand.
        Windows.Win32.PInvoke.SetWindowPos(
            (Windows.Win32.Foundation.HWND)new System.Windows.Interop.WindowInteropHelper(backdrop).Handle,
            (Windows.Win32.Foundation.HWND)new System.Windows.Interop.WindowInteropHelper(frosted).Handle,
            0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => application.Shutdown();
        timer.Start();
        application.Run();
        return 0;
    }

    /// --try-unfold: shows an island at the top of the screen, unfolds it and folds it again from code,
    /// and logs its size and place on the way. The mouse is left alone.
    public static int TryUnfold()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var island = new IslandWindow();
        island.Show(CardSnapshot.Example(), IslandPosition.Default);

        var step = 0;
        var widths = new List<double>();
        var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        sampler.Tick += (_, _) => widths.Add(island.ActualWidth);
        sampler.Start();

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) =>
        {
            switch (step++)
            {
                case 0:
                    Log.Write($"--try-unfold: folded {island.ActualWidth:0}x{island.ActualHeight:0} centre {island.Left + (island.ActualWidth / 2):0}");
                    widths.Clear();
                    island.Unfold(true);
                    break;
                case 1:
                    Log.Write($"--try-unfold: unfolded {island.ActualWidth:0}x{island.ActualHeight:0} centre {island.Left + (island.ActualWidth / 2):0}, widths on the way {string.Join(" ", widths.Select(w => w.ToString("0")))}");
                    widths.Clear();
                    island.Unfold(false);
                    break;
                default:
                    Log.Write($"--try-unfold: folded again {island.ActualWidth:0}x{island.ActualHeight:0}, widths on the way {string.Join(" ", widths.Select(w => w.ToString("0")))}");
                    application.Shutdown();
                    break;
            }
        };
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
