namespace Aiko.Core.Tests;

public class TrayFaceMotionTests
{
    private static void Near(IconFrame expected, IconFrame actual)
    {
        Assert.Equal(expected.RingScale, actual.RingScale, 3);
        Assert.Equal(expected.RingOpacity, actual.RingOpacity, 3);
        Assert.Equal(expected.RingSweep, actual.RingSweep, 3);
        Assert.Equal(expected.FaceScale, actual.FaceScale, 3);
        Assert.Equal(expected.FaceOpacity, actual.FaceOpacity, 3);
    }

    [Fact]
    public void Each_step_ends_where_the_next_one_starts()
    {
        Near(new IconFrame(0.6, 0, 1, 0.6, 0), TrayFaceMotion.At(FacePhase.RingsOut, 1));
        Near(new IconFrame(0.6, 0, 1, 1, 1), TrayFaceMotion.At(FacePhase.FaceIn, 1));
        Near(new IconFrame(0.6, 0, 1, 0.8, 0), TrayFaceMotion.At(FacePhase.FaceOut, 1));
        Near(IconFrame.Rings, TrayFaceMotion.At(FacePhase.RingsBack, 1));
    }

    [Fact]
    public void The_rings_start_whole_and_come_back_whole()
    {
        Near(IconFrame.Rings, TrayFaceMotion.At(FacePhase.RingsOut, 0));
        Assert.Equal(0, TrayFaceMotion.At(FacePhase.RingsBack, 0).RingSweep);
    }

    [Fact]
    public void The_face_springs_a_little_past_its_size()
    {
        var sizes = Enumerable.Range(0, 101).Select(i => TrayFaceMotion.At(FacePhase.FaceIn, i / 100.0).FaceScale).ToList();

        Assert.True(sizes.Max() > 1);
        Assert.True(sizes.Max() < 1.1);
    }

    [Fact]
    public void Eight_frames_a_step_and_the_last_one_at_the_end()
    {
        foreach (var phase in Enum.GetValues<FacePhase>())
        {
            var frames = TrayFaceMotion.Frames(phase);

            Assert.Equal(TrayFaceMotion.FramesPerStep, frames.Count);
            Near(TrayFaceMotion.At(phase, 1), frames[^1]);
        }
    }

    [Fact]
    public void The_island_shrinks_to_the_face_while_the_rings_go_and_grows_back_with_them()
    {
        Assert.Equal(0, TrayFaceMotion.IslandFit(FacePhase.RingsOut, 0));
        Assert.Equal(1, TrayFaceMotion.IslandFit(FacePhase.RingsOut, 1));
        Assert.Equal(1, TrayFaceMotion.IslandFit(FacePhase.FaceIn, 0.3));
        Assert.Equal(1, TrayFaceMotion.IslandFit(FacePhase.FaceOut, 0.7));
        Assert.Equal(0, TrayFaceMotion.IslandFit(FacePhase.RingsBack, 1));
        Assert.Equal(0.5, TrayFaceMotion.IslandFit(FacePhase.RingsBack, 0.5), 6);
    }

    [Fact]
    public void The_face_is_fully_there_400_ms_after_the_event()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(400), TrayFaceMotion.Arrival);
    }

    [Fact]
    public void A_face_already_shown_only_comes_in_again()
    {
        Assert.Equal([FacePhase.RingsOut, FacePhase.FaceIn], TrayFaceMotion.ToFace(faceShown: false));
        Assert.Equal([FacePhase.FaceIn], TrayFaceMotion.ToFace(faceShown: true));
        Assert.Equal([FacePhase.FaceOut, FacePhase.RingsBack], TrayFaceMotion.ToRings(faceShown: true));
        Assert.Equal([FacePhase.RingsBack], TrayFaceMotion.ToRings(faceShown: false));
    }
}
