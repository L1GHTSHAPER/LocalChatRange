# Renders package\icon.png (256x256, required by Thunderstore) with System.Drawing.
param([string]$Out = (Join-Path $PSScriptRoot '..\package\icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}
function C([int]$a, [int]$r, [int]$gr, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $gr, $b) }

# Background
$bg = New-RoundedRect 0 0 256 256 44
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, 256), (C 255 46 62 92), (C 255 22 30 48)
$g.FillPath($bgBrush, $bg)

# Chat range on the ground: soft fill with a bright edge
$area = New-Object System.Drawing.RectangleF 18, 128, 220, 92
$areaPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$areaPath.AddEllipse($area)
$glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $areaPath
$glow.CenterColor = C 30 140 217 255
$glow.SurroundColors = @((C 120 140 217 255))
$g.FillPath($glow, $areaPath)
$g.DrawEllipse((New-Object System.Drawing.Pen (C 255 140 217 255), 7), $area)

# Players in range: green rings
$green = New-Object System.Drawing.Pen (C 255 140 255 140), 5
$g.DrawEllipse($green, 40, 168, 40, 16)
$g.DrawEllipse($green, 176, 178, 40, 16)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 140 255 140)), 50, 136, 20, 20)
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C 255 140 255 140)), 52, 152, 16, 24)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 140 255 140)), 186, 146, 20, 20)
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C 255 140 255 140)), 188, 162, 16, 24)

# Your character in the centre
$cream = New-Object System.Drawing.SolidBrush (C 255 250 238 222)
$g.FillPath($cream, (New-RoundedRect 104 116 48 64 22))
$g.FillEllipse($cream, 100, 70, 56, 56)
$ink = New-Object System.Drawing.SolidBrush (C 255 34 44 68)
$g.FillEllipse($ink, 116, 92, 7, 9)
$g.FillEllipse($ink, 133, 92, 7, 9)

# Speech bubble
$bubble = New-RoundedRect 148 22 88 54 18
$tail = New-Object System.Drawing.Drawing2D.GraphicsPath
$tail.AddPolygon(@((New-Object System.Drawing.PointF 162, 68), (New-Object System.Drawing.PointF 186, 70), (New-Object System.Drawing.PointF 152, 92)))
$white = New-Object System.Drawing.SolidBrush (C 255 255 255 255)
$g.FillPath($white, $bubble)
$g.FillPath($white, $tail)
foreach ($x in 166, 186, 206) { $g.FillEllipse($ink, $x, 43, 12, 12) }

$g.Dispose()
$full = [System.IO.Path]::GetFullPath($Out)
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Icon: $full"
