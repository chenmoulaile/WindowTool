Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$x=16; $y=34; $w=224; $hh=196; $r=36
$path.AddArc($x,$y,$r,$r,180,90)
$path.AddArc(($x+$w-$r),$y,$r,$r,270,90)
$path.AddArc(($x+$w-$r),($y+$hh-$r),$r,$r,0,90)
$path.AddArc($x,($y+$hh-$r),$r,$r,90,90)
$path.CloseFigure()

$bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,28,96,192))
$g.FillPath($bgBrush,$path)

$titleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,255,255))
$g.FillRectangle($titleBrush, ($x+22), ($y+20), 60, 12)

$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 20
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($pen, 128, 186, 128, 108)

$pts = @(
    (New-Object System.Drawing.Point 128,64),
    (New-Object System.Drawing.Point 84,122),
    (New-Object System.Drawing.Point 172,122)
)
$whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g.FillPolygon($whiteBrush,$pts)

$g.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ms.Dispose()
$bmp.Dispose()

$outPath = Join-Path $PSScriptRoot 'app.ico'
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]1)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([uint16]1)
$bw.Write([uint16]32)
$bw.Write([uint32]$png.Length)
$bw.Write([uint32]22)
$bw.Write($png)
$bw.Close()
$fs.Close()
Write-Output ("ICON OK: " + $outPath + " (" + (Get-Item $outPath).Length + " bytes)")
