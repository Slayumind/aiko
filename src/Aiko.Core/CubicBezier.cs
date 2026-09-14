namespace Aiko.Core;

/// The CSS cubic-bezier timing curve, which WPF does not have.
///
/// The accepted prototype was built in the browser with two curves: an easing for anything that
/// opens and a spring that overshoots a little when something lands (DESIGN.md, D-161). This is
/// the same maths, so the windows move the way the prototype did.
public readonly record struct CubicBezier(double X1, double Y1, double X2, double Y2)
{
    /// Opening, appearing, hover: fast start, soft landing.
    public static readonly CubicBezier Standard = new(0.2, 0.8, 0.2, 1);

    /// Release: goes a little past the end and settles back.
    public static readonly CubicBezier Spring = new(0.3, 1.4, 0.5, 1);

    /// Progress along the curve at a share of the time, both from 0 to 1. The curve is given by x,
    /// so x is solved for t first, and y at that t is the answer.
    public double Ease(double time)
    {
        if (time <= 0)
        {
            return 0;
        }

        if (time >= 1)
        {
            return 1;
        }

        return Sample(Y1, Y2, SolveForX(time));
    }

    private double SolveForX(double x)
    {
        // Newton first: a few steps are enough for every curve Aiko uses.
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Sample(X1, X2, t) - x;
            if (Math.Abs(error) < 1e-6)
            {
                return t;
            }

            var slope = Slope(X1, X2, t);
            if (Math.Abs(slope) < 1e-6)
            {
                break;
            }

            t -= error / slope;
        }

        // Bisection when Newton cannot settle, which happens where the curve is almost flat.
        double low = 0, high = 1;
        t = x;
        for (var i = 0; i < 40; i++)
        {
            var value = Sample(X1, X2, t);
            if (Math.Abs(value - x) < 1e-6)
            {
                break;
            }

            if (value < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }

            t = (low + high) / 2;
        }

        return t;
    }

    private static double Sample(double p1, double p2, double t) =>
        ((((1 - (3 * p2)) + (3 * p1)) * t + ((3 * p2) - (6 * p1))) * t + (3 * p1)) * t;

    private static double Slope(double p1, double p2, double t) =>
        (3 * ((1 - (3 * p2)) + (3 * p1)) * t * t) + (2 * ((3 * p2) - (6 * p1)) * t) + (3 * p1);
}
