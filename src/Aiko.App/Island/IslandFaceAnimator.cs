using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// The face on the island (D-211): the same steps and numbers as on the tray icon, but smooth. The
/// island is a window, so it follows the screen's own frames, and only while a step is playing.
sealed class IslandFaceAnimator
{
    private readonly FrameworkElement _rings;
    private readonly Image _face;
    private readonly Func<IEnumerable<RingGauge>> _gauges;
    private readonly Queue<FacePhase> _steps = new();
    private readonly Stopwatch _clock = new();

    private FacePhase _step;
    private bool _listening;

    public IslandFaceAnimator(FrameworkElement rings, Image face, Func<IEnumerable<RingGauge>> gauges)
    {
        _rings = rings;
        _face = face;
        _gauges = gauges;

        _rings.RenderTransformOrigin = new Point(0.5, 0.5);
        _face.RenderTransformOrigin = new Point(0.5, 0.5);
        Apply(IconFrame.Rings);
    }

    public IconFrame Frame { get; private set; } = IconFrame.Rings;

    public void Show(ImageSource face)
    {
        var shown = Frame.ShowsFace;
        _face.Source = face;
        Play(TrayFaceMotion.ToFace(shown));
    }

    public void Hide()
    {
        if (_face.Source is null)
        {
            return;
        }

        Play(TrayFaceMotion.ToRings(Frame.ShowsFace));
    }

    /// For a snapshot: one frame, at once.
    public void Set(IconFrame frame, ImageSource? face)
    {
        _face.Source = face;
        Apply(frame);
    }

    /// New rings come in with a full arc; while the rings grow back they have to keep the sweep.
    public void Reapply() => Apply(Frame);

    private void Play(IReadOnlyList<FacePhase> steps)
    {
        _steps.Clear();

        if (!Motion.IsOn)
        {
            Apply(TrayFaceMotion.At(steps[^1], 1));
            Finish();
            return;
        }

        foreach (var step in steps)
        {
            _steps.Enqueue(step);
        }

        NextStep();
        if (!_listening)
        {
            CompositionTarget.Rendering += OnRendering;
            _listening = true;
        }
    }

    private void NextStep()
    {
        _step = _steps.Dequeue();
        _clock.Restart();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var t = _clock.Elapsed / TrayFaceMotion.Duration(_step);
        Apply(TrayFaceMotion.At(_step, t));

        if (t < 1)
        {
            return;
        }

        if (_steps.Count > 0)
        {
            NextStep();
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _listening = false;
        Finish();
    }

    private void Finish()
    {
        if (!Frame.ShowsFace)
        {
            _face.Source = null;
        }
    }

    private void Apply(IconFrame frame)
    {
        Frame = frame;
        _rings.RenderTransform = new ScaleTransform(frame.RingScale, frame.RingScale);
        _rings.Opacity = frame.RingOpacity;
        _face.RenderTransform = new ScaleTransform(frame.FaceScale, frame.FaceScale);
        _face.Opacity = frame.FaceOpacity;

        foreach (var gauge in _gauges())
        {
            gauge.Sweep = frame.RingSweep;
        }
    }
}
