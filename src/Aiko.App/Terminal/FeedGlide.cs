using System.Windows.Controls;
using System.Windows.Media;

namespace Aiko.App;

/// The feed moves by gliding, not jumping (D-332, check of 2026-10-23).
///
/// A new line of an answer made the feed jump 20–32 px a few times a second, and that was what
/// made the ink look uneven. A notch of the wheel jumped 48 px in one frame, and on a screen of
/// 240 Hz that read as a jerk, though the step itself took under a millisecond (measure of
/// 2026-10-26). Now each frame closes a part of the way left, so the feed slows as it arrives and
/// is there in about 200 ms; a new line or a new notch on the way only moves the place it heads
/// for. It runs only while there is a way to go, and with animations off in Windows it jumps.
sealed class FeedGlide
{
    /// The time in which the way left shrinks to a third: 95 % of it is gone in about 200 ms.
    private const double EaseMs = 70;

    private ScrollViewer? _scroll;
    private TimeSpan _lastFrame;
    private bool _running;

    /// Where the glide heads; null is the end of the feed, wherever it moves to.
    private double? _target;

    public void ToEnd(ScrollViewer scroll)
    {
        _target = null;
        if (!Motion.IsOn)
        {
            Stop();
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
            return;
        }

        Start(scroll);
    }

    /// The wheel: the feed heads further by this many pixels, up when negative. A notch that comes
    /// while the feed still moves adds to the place it heads for.
    public void By(ScrollViewer scroll, double pixels)
    {
        var from = _running && _target is { } heading ? heading : scroll.VerticalOffset;
        var to = Math.Clamp(from + pixels, 0, scroll.ScrollableHeight);
        if (!Motion.IsOn)
        {
            Stop();
            scroll.ScrollToVerticalOffset(to);
            return;
        }

        _target = to;
        Start(scroll);
    }

    private void Start(ScrollViewer scroll)
    {
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

        var end = Math.Min(_target ?? scroll.ScrollableHeight, scroll.ScrollableHeight);
        var next = scroll.VerticalOffset + ((end - scroll.VerticalOffset) * (1 - Math.Exp(-passed / EaseMs)));
        if (Math.Abs(end - next) < 0.5)
        {
            next = end;
            Stop();
        }

        scroll.ScrollToVerticalOffset(next);
    }
}
