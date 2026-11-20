# Build a multi-size app.ico (PNG-compressed entries, supported on Windows Vista+).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = New-Object System.Collections.ArrayList

$script:g = $null
$script:f = 1.0

function Fill-Ellipse([double]$x, [double]$y, [double]$w, [double]$h, [System.Drawing.Color]$c) {
    $b = New-Object System.Drawing.SolidBrush $c
    $g.FillEllipse($b, [float]($x * $script:f), [float]($y * $script:f), [float]($w * $script:f), [float]($h * $script:f))
    $b.Dispose()
}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $script:g = [System.Drawing.Graphics]::FromImage($bmp)
    $script:g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $script:g.Clear([System.Drawing.Color]::Transparent)
    $script:f = $s / 32.0

    Fill-Ellipse 6 4 7 8 ([System.Drawing.Color]::FromArgb(255, 86, 192, 166))
    Fill-Ellipse 19 4 7 8 ([System.Drawing.Color]::FromArgb(255, 86, 192, 166))
    Fill-Ellipse 4 8 24 22 ([System.Drawing.Color]::FromArgb(255, 95, 203, 175))
    Fill-Ellipse 10.5 15 4.5 6 ([System.Drawing.Color]::White)
    Fill-Ellipse 17 15 4.5 6 ([System.Drawing.Color]::White)
    Fill-Ellipse 11.8 16.4 2.2 3 ([System.Drawing.Color]::FromArgb(255, 43, 43, 51))
    Fill-Ellipse 18.3 16.4 2.2 3 ([System.Drawing.Color]::FromArgb(255, 43, 43, 51))

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 51, 48, 58)), ([float](2.2 * $script:f))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $script:g.DrawArc($pen, [float](11 * $script:f), [float](19 * $script:f), [float](10 * $script:f), [float](8 * $script:f), 20, 140)
    $pen.Dispose()
    $script:g.Dispose()
    $script:g = $null

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [void]$pngs.Add($ms.ToArray())
    $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = 0
    if ($s -lt 256) { $dim = $s }
    $len = $pngs[$i].Length

    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$len)
    $bw.Write([UInt32]$offset)
    $offset += $len
}

foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()

$target = Join-Path $PSScriptRoot 'app.ico'
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Output "icon written: $target ($((Get-Item $target).Length) bytes)"
