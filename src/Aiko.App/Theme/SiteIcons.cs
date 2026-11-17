using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aiko.App;

/// The icons of the slayumind icon set (`.local/icons` of the site), so the terminal draws the
/// same marks as the Office (D-332). Each path is drawn in a box of 24; the lines are filled
/// shapes one unit wide, which is why they stay thin at any size.
static class SiteIcons
{
    /// check.svg
    public const string Check =
        "M23.855,6.353l-13.883,14c-.419,.418-.973,.647-1.562,.647s-1.144-.229-1.561-.646L.152,13.859" +
        "c-.198-.192-.203-.509-.012-.707,.194-.198,.51-.203,.707-.012l6.703,6.5c.469,.467,1.256,.461,1.713,.006" +
        "L23.145,5.647c.195-.194,.511-.197,.708-.003,.195,.195,.197,.512,.003,.708Z";

    /// copy.svg
    public const string Copy =
        "m16,18.5v1c0,2.481-2.019,4.5-4.5,4.5h-7c-2.481,0-4.5-2.019-4.5-4.5v-7c0-2.481,2.019-4.5,4.5-4.5h1" +
        "c.276,0,.5.224.5.5s-.224.5-.5.5h-1c-1.93,0-3.5,1.57-3.5,3.5v7c0,1.93,1.57,3.5,3.5,3.5h7" +
        "c1.93,0,3.5-1.57,3.5-3.5v-1c0-.276.224-.5.5-.5s.5.224.5.5Zm8-14v7c0,2.481-2.019,4.5-4.5,4.5h-7" +
        "c-2.481,0-4.5-2.019-4.5-4.5v-7c0-2.481,2.019-4.5,4.5-4.5h7c2.481,0,4.5,2.019,4.5,4.5Zm-1,0" +
        "c0-1.93-1.57-3.5-3.5-3.5h-7c-1.93,0-3.5,1.57-3.5,3.5v7c0,1.93,1.57,3.5,3.5,3.5h7" +
        "c1.93,0,3.5-1.57,3.5-3.5v-7Z";

    /// An icon of the set at a size, in a colour of the tokens.
    public static Path Draw(string data, double size, string tone) => new()
    {
        Data = Geometry.Parse(data),
        Fill = Tokens.Brush(tone),
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
