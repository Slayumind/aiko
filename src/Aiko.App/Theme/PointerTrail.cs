using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aiko.App;

/// Whether the pointer really moved since the last look.
///
/// WPF raises MouseEnter and MouseMove when the content moves under a still pointer: after a scroll,
/// a new layout or a list that was filled again. A list that lights its rows on those takes away
/// the row the keyboard chose. cmdk and Radix light a row only when the pointer itself moves.
static class PointerTrail
{
    private static Point Last = new(double.NaN, double.NaN);

    public static bool Moved(Visual relativeTo)
    {
        if (PresentationSource.FromVisual(relativeTo) is null)
        {
            return false;
        }

        var now = relativeTo.PointToScreen(Mouse.GetPosition((IInputElement)relativeTo));
        var moved = now != Last;
        Last = now;
        return moved;
    }
}
