using System.Windows.Media;

namespace Aiko.App;

/// The colours of the terminal, from the palette of slayumind (D-295). The real terminal and the
/// blocks of a command line both draw from here, so a block looks like the terminal.
internal static class TerminalColors
{
    /// The sixteen colours, 0xRRGGBB. The palette has no grey between muted and raised that text can
    /// be read in, so bright black is the one colour added: halfway between them.
    public static readonly uint[] Ansi =
    [
        0x171717, 0xFF6467, 0x00BC7D, 0xFE9A00, 0x11A9F6, 0xD477CD, 0x00D3F2, 0xA1A1A1,
        0x737373, 0xFF8270, 0xAFE773, 0xFE9A00, 0x11A9F6, 0xF7BCF0, 0x24DCC3, 0xFAFAFA,
    ];

    /// The ground of the terminal is the surface of every other panel.
    public static Color Ground => Tokens.Colour("SurfaceColor");

    public static uint Hex(Color colour) => ((uint)colour.R << 16) | ((uint)colour.G << 8) | colour.B;
}
