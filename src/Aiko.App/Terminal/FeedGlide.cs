using System.Windows.Controls;
using System.Windows.Media;

namespace Aiko.App;

/// The feed follows a growing answer by gliding down, not jumping (D-332, check of 2026-10-23).
///
/// A new line of an answer made the feed jump 20–32 px a few times a second, and that was what
/// made the ink look uneven. Now each frame closes a part of the way left, so the feed slows as
/// it arrives and is there in about 200 ms; a new line on the way only moves the end it heads
/// for. It runs only while there is a way to go, and with animations off in Windows it jumps.
sealed class FeedGlide
{
    /// The time in which the way left shrinks to a third: 95 % of it is gone in about 200 ms.
    private const double EaseMs = 70;

    private ScrollViewer? _scroll;
    private TimeSpan _lastFrame;
    private bool _running;

    public void ToEnd(ScrollViewer scroll)
    {
        if (!Motion.IsOn)
        {
            Stop();
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
            return;
        }

        _scroll = scroll;
        if (!_running)
        {
            _running = true;
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    /// The reader took the scroll: the glide lets go at once.
    public void Stop()
    {
        if (_running)
        {
            _running = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_scroll is not { } scroll || e is not RenderingEventArgs frame)
        {
            Stop();
            return;
        }

        if (_lastFrame == TimeSpan.Zero || frame.RenderingTime == _lastFrame)
        {
            _lastFrame = frame.RenderingTime;
            return;
        }

        var passed = (frame.RenderingTime - _lastFrame).TotalMilliseconds;
        _lastFrame = frame.RenderingTime;

        var end = scroll.ScrollableHeight;
        var next = scroll.VerticalOffset + ((end - scroll.VerticalOffset) * (1 - Math.Exp(-passed / EaseMs)));
        if (end - next < 0.5)
        {
            next = end;
            Stop();
        }

        scroll.ScrollToVerticalOffset(next);
    }
}
