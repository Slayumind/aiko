# Draws the Aiko logo, the ring, into src/Aiko.App/Assets/aiko.ico.
#
#   pwsh -NoProfile -File tools/app-icon.ps1
#
# The ring is a grey track and a green arc over 42 % of the circle, from the top, clockwise. There is
# no plate behind it: a dark plate melted into a dark taskbar at 16 px. The track is half see-through,
# so the same file reads on a dark and on a light taskbar. Every size is drawn on its own grid rather
# than scaled down from 256, so the small ones stay sharp.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$green = [System.Drawing.Color]::FromArgb(255, 0, 188, 125)
$track = [System.Drawing.Color]::FromArgb(120, 140, 140, 140)
$target = Join-Path $PSScriptRoot '..\src\Aiko.App\Assets\aiko.ico'

function Draw-Ring([int] $size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.PixelOffsetMode = 'HighQuality'
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $radius = $size * 0.36
    $stroke = [math]::Max(2, $size * 0.16)
    $centre = $size / 2.0
    $box = [System.Drawing.RectangleF]::new($centre - $radius, $centre - $radius, 2 * $radius, 2 * $radius)
    $graphics.DrawEllipse([System.Drawing.Pen]::new($track, $stroke), $box)
    $graphics.DrawArc([System.Drawing.Pen]::new($green, $stroke), $box, -90, 360 * 0.42)
    $graphics.Dispose()

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return , $stream.ToArray()
}

# An .ico file is a small header, one 16-byte entry per size, then the images. Windows 10 and 11 read
# PNG images inside an .ico at every size.
$images = foreach ($size in $sizes) { , (Draw-Ring $size) }
$file = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16] 0)
$writer.Write([uint16] 1)
$writer.Write([uint16] $sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $side = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte] $side)
    $writer.Write([byte] $side)
    $writer.Write([byte] 0)
    $writer.Write([byte] 0)
    $writer.Write([uint16] 1)
    $writer.Write([uint16] 32)
    $writer.Write([uint32] $images[$i].Length)
    $writer.Write([uint32] $offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

[System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($target), $file.ToArray())
"aiko.ico: $($sizes.Count) sizes, $([math]::Round($file.Length / 1KB, 1)) KB"
