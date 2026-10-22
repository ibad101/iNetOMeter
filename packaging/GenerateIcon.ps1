# GenerateIcon.ps1 - Generates multi-resolution assets/app.ico matching assets/logo.svg
Add-Type -AssemblyName System.Drawing

function Draw-IconFrame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Dark navy rounded background
    $radius = [Math]::Max(2.0, $size * 0.22)
    $diameter = $radius * 2.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rect = New-Object System.Drawing.RectangleF 0, 0, ($size - 1), ($size - 1)
    
    $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc(($rect.Right - $diameter), $rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc(($rect.Right - $diameter), ($rect.Bottom - $diameter), $diameter, $diameter, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $diameter), $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(11, 17, 32))
    $g.FillPath($bgBrush, $path)
    $bgBrush.Dispose()

    # Perimeter border
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 255, 255, 255)), ([Math]::Max(1.0, $size * 0.03))
    $g.DrawPath($borderPen, $path)
    $borderPen.Dispose()
    $path.Dispose()

    # Gauge track
    $cx = $size / 2.0
    $cy = $size * 0.52
    $arcR = $size * 0.32
    $arcRect = New-Object System.Drawing.RectangleF ($cx - $arcR), ($cy - $arcR), ($arcR * 2.0), ($arcR * 2.0)
    $trackThickness = [Math]::Max(1.5, $size * 0.08)

    $trackPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(35, 255, 255, 255)), $trackThickness
    $trackPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $trackPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($trackPen, $arcRect, 135, 270)
    $trackPen.Dispose()

    # Colored speed arc (cyan #22D3EE)
    $arcPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(34, 211, 238)), $trackThickness
    $arcPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arcPen, $arcRect, 135, 190)
    $arcPen.Dispose()

    # Needle (white line pointing up-right)
    $needleLen = $arcR * 0.85
    $angleRad = -35.0 * [Math]::PI / 180.0
    $nx = $cx + ($needleLen * [Math]::Cos($angleRad))
    $ny = $cy + ($needleLen * [Math]::Sin($angleRad))
    $needleThickness = [Math]::Max(1.2, $size * 0.06)
    $needlePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $needleThickness
    $needlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $needlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($needlePen, [float]$cx, [float]$cy, [float]$nx, [float]$ny)
    $needlePen.Dispose()

    # Center pivot circle
    $pivotR = [Math]::Max(2.0, $size * 0.08)
    $pivotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillEllipse($pivotBrush, [float]($cx - $pivotR), [float]($cy - $pivotR), [float]($pivotR * 2), [float]($pivotR * 2))
    $pivotBrush.Dispose()

    $innerR = $pivotR * 0.45
    $innerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(59, 130, 246))
    $g.FillEllipse($innerBrush, [float]($cx - $innerR), [float]($cy - $innerR), [float]($innerR * 2), [float]($innerR * 2))
    $innerBrush.Dispose()

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytesList = @()

foreach ($s in $sizes) {
    $frame = Draw-IconFrame $s
    $ms = New-Object System.IO.MemoryStream
    $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytesList += ,@($s, $ms.ToArray())
    $frame.Dispose()
    $ms.Dispose()
}

$outputPath = Join-Path $PSScriptRoot "..\assets\app.ico"
$outputDir = [System.IO.Path]::GetDirectoryName($outputPath)
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$fs = [System.IO.File]::Create($outputPath)
$bw = New-Object System.IO.BinaryWriter $fs

# Write ICONDIR Header (6 bytes)
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # 1 = ICO
$bw.Write([uint16]$pngBytesList.Count) # Count of frames

# Calculate initial image data offset: 6 header bytes + (16 bytes * count)
$offset = 6 + (16 * $pngBytesList.Count)

# Write Directory Entries (16 bytes each)
foreach ($item in $pngBytesList) {
    $s = $item[0]
    $data = $item[1]
    $w = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $h = if ($s -ge 256) { [byte]0 } else { [byte]$s }

    $bw.Write($w)               # Width (0 = 256)
    $bw.Write($h)               # Height (0 = 256)
    $bw.Write([byte]0)          # ColorCount
    $bw.Write([byte]0)          # Reserved
    $bw.Write([uint16]1)        # Planes
    $bw.Write([uint16]32)       # BitCount (32-bit ARGB)
    $bw.Write([uint32]$data.Length) # BytesInRes
    $bw.Write([uint32]$offset)  # ImageOffset
    $offset += $data.Length
}

# Write PNG payload for each frame
foreach ($item in $pngBytesList) {
    $data = $item[1]
    $bw.Write($data)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Output "Successfully generated assets/app.ico ($((Get-Item $outputPath).Length) bytes, $($sizes.Count) frames)"
