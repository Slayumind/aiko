namespace Aiko.Core;

/// The steps between the rings and a face (D-211), as in the mockup's timeline.
public enum FacePhase
{
    RingsOut,
    FaceIn,
    FaceOut,
    RingsBack,
}

/// How the icon looks at one moment: the rings and the face, each with a size and an opacity. The
/// sweep draws the arc back from zero when the rings return.
public readonly record struct IconFrame(double RingScale, double RingOpacity, double RingSweep, double FaceScale, double FaceOpacity)
{
    public static readonly IconFrame Rings = new(1, 1, 1, 0.6, 0);

    public bool ShowsFace => FaceOpacity > 0;
}

/// Frames for the tray icon. The icon is a picture Windows is handed whole, so a transition is a
/// few pictures in a row rather than a smooth animation: eight per step, as in the mockup.
public static class TrayFaceMotion
{
    public const int FramesPerStep = 8;

    /// From the event until the face is fully there. The face then stays TrayMood.ShowFor.
    public static readonly TimeSpan Arrival = Duration(FacePhase.RingsOut) + Duration(FacePhase.FaceIn);

    public static TimeSpan Duration(FacePhase phase) => TimeSpan.FromMilliseconds(phase switch
    {
        FacePhase.RingsOut => 160,
        FacePhase.FaceIn => 240,
        FacePhase.FaceOut => 160,
        _ => 280,
    });

    /// The frame at t from 0 to 1 through a step.
    public static IconFrame At(FacePhase phase, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return phase switch
        {
            FacePhase.RingsOut => new(1 - (0.4 * EaseIn(t)), 1 - EaseIn(t), 1, 0.6, 0),
            FacePhase.FaceIn => new(0.6, 0, 1, 0.6 + (0.4 * Spring(t)), Math.Min(1, t * 2)),
            FacePhase.FaceOut => new(0.6, 0, 1, 1 - (0.2 * EaseIn(t)), 1 - EaseIn(t)),
            _ => new(0.6 + (0.4 * Spring(t)), Math.Min(1, t * 2), Math.Min(1, t * 1.3), 0.6, 0),
        };
    }

    /// The pictures of one step, the last one exactly at its end.
    public static IReadOnlyList<IconFrame> Frames(FacePhase phase) =>
        Enumerable.Range(1, FramesPerStep).Select(i => At(phase, (double)i / FramesPerStep)).ToList();

    /// How far the island has shrunk from the size of its rings to the size of the face, 0 to 1.
    /// It shrinks while the rings go and grows back while they return, so the face always sits with
    /// even room on every side.
    public static double IslandFit(FacePhase phase, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return phase switch
        {
            FacePhase.RingsOut => EaseInOut(t),
            FacePhase.RingsBack => 1 - EaseInOut(t),
            _ => 1,
        };
    }

    /// The steps from what the icon shows now to a face. A face already there needs only to come in
    /// again with its new look.
    public static IReadOnlyList<FacePhase> ToFace(bool faceShown) =>
        faceShown ? [FacePhase.FaceIn] : [FacePhase.RingsOut, FacePhase.FaceIn];

    /// The steps back to the rings. Rings still on their way out just come back.
    public static IReadOnlyList<FacePhase> ToRings(bool faceShown) =>
        faceShown ? [FacePhase.FaceOut, FacePhase.RingsBack] : [FacePhase.RingsBack];

    private static double EaseIn(double t) => t * t;

    private static double EaseInOut(double t) => t < 0.5 ? 2 * t * t : 1 - (Math.Pow(-2 * t + 2, 2) / 2);

    /// Overshoots a little past 1 and settles, like the spring in the mockup.
    private static double Spring(double t) =>
        1 - (Math.Pow(1 - t, 3) * Math.Cos(t * Math.PI * 1.15) * (1 - (t * 0.15)));
}
