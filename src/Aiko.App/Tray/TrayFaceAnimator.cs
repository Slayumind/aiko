using System.Windows.Media;
using System.Windows.Threading;
using Aiko.Core;
using Microsoft.Win32;

namespace Aiko.App;

/// Plays the frames between the rings and a face on the tray icon (D-211). The timer runs only
/// through a transition, a few hundred milliseconds; while a face stays or the rings are back it is
/// stopped.
sealed class TrayFaceAnimator
{
    private readonly DispatcherTimer _timer;
    private readonly Queue<(IconFrame Frame, TimeSpan Wait)> _frames = new();

    public TrayFaceAnimator(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher);
        _timer.Tick += (_, _) => Step();
    }

    /// Raised for every new frame, on the dispatcher thread.
    public event Action? Changed;

    public IconFrame Frame { get; private set; } = IconFrame.Rings;

    /// The face being drawn, kept while it fades out. Null once the rings are back.
    public ImageSource? Face { get; private set; }

    public void Show(ImageSource face)
    {
        var shown = Frame.ShowsFace;
        Face = face;
        Play(TrayFaceMotion.ToFace(shown));
    }

    public void Hide()
    {
        if (Face is null)
        {
            return;
        }

        Play(TrayFaceMotion.ToRings(Frame.ShowsFace));
    }

    private void Play(IReadOnlyList<FacePhase> phases)
    {
        _timer.Stop();
        _frames.Clear();

        if (!Motion.IsOn)
        {
            // Animations are off in Windows: the end of the way, at once.
            Frame = TrayFaceMotion.At(phases[^1], 1);
            Finish();
            return;
        }

        foreach (var phase in phases)
        {
            var wait = TrayFaceMotion.Duration(phase) / TrayFaceMotion.FramesPerStep;
            foreach (var frame in TrayFaceMotion.Frames(phase))
            {
                _frames.Enqueue((frame, wait));
            }
        }

        Step();
    }

    private void Step()
    {
        if (!_frames.TryDequeue(out var next))
        {
            _timer.Stop();
            return;
        }

        Frame = next.Frame;
        if (_frames.Count == 0)
        {
            _timer.Stop();
            Finish();
            return;
        }

        _timer.Interval = next.Wait;
        _timer.Start();
        Changed?.Invoke();
    }

    private void Finish()
    {
        if (!Frame.ShowsFace)
        {
            Face = null;
        }

        Changed?.Invoke();
    }
}

/// Whether the taskbar is light. The chibi needs its outline there (D-214), and the emoji draws in
/// black instead of white.
static class TaskbarTheme
{
    public static FaceGround Ground()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int light && light == 1 ? FaceGround.Light : FaceGround.Dark;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return FaceGround.Dark;
        }
    }
}
