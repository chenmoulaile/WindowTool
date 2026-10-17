Add-Type -AssemblyName System.Drawing

# WindowTool 产品图标：macOS squircle 底座 + 暖色后层窗口 + 白色前层窗口。
# 逐尺寸绘制（细节随尺寸递减）；16/20/24/32 用 BMP 条目保证托盘兼容，>=40 用 PNG。

$Sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

$SM = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$IM = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$POM = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$LC = [System.Drawing.Drawing2D.LineCap]::Round
$F32 = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
$PngFmt = [System.Drawing.Imaging.ImageFormat]::Png

function C([int]$a, [int]$r, [int]$g, [int]$b)
{
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function F([double]$v) { return [single]$v }

function PF([double]$x, [double]$y) { return [System.Drawing.PointF]::new((F $x), (F $y)) }

function RF([double]$x, [double]$y, [double]$w, [double]$h)
{
    return [System.Drawing.RectangleF]::new((F $x), (F $y), (F $w), (F $h))
}

function BRUSH2($c1, $c2) { return [System.Drawing.SolidBrush]::new($c1) }

function New-Squircle([double]$x, [double]$y, [double]$w, [double]$h, [double]$n, [int]$steps)
{
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $a = $w / 2.0
    $b = $h / 2.0
    $cx = $x + $a
    $cy = $y + $b
    $pts = New-Object 'System.Drawing.PointF[]' $steps
    for ($i = 0; $i -lt $steps; $i++)
    {
        $t = 2.0 * [Math]::PI * $i / $steps
        $c = [Math]::Cos($t)
        $s = [Math]::Sin($t)
        $px = $cx + $a * [Math]::Sign($c) * [Math]::Pow([Math]::Abs($c), 2.0 / $n)
        $py = $cy + $b * [Math]::Sign($s) * [Math]::Pow([Math]::Abs($s), 2.0 / $n)
        $pts[$i] = [System.Drawing.PointF]::new((F $px), (F $py))
    }
    $path.AddLines($pts)
    $path.CloseFigure()
    return $path
}

function New-RoundRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r)
{
    if ($r * 2 -gt $w) { $r = $w / 2.0 }
    if ($r * 2 -gt $h) { $r = $h / 2.0 }
    $d = F ($r * 2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc((F $x), (F $y), $d, $d, 180, 90)
    $path.AddArc((F ($x + $w - 2 * $r)), (F $y), $d, $d, 270, 90)
    $path.AddArc((F ($x + $w - 2 * $r)), (F ($y + $h - 2 * $r)), $d, $d, 0, 90)
    $path.AddArc((F $x), (F ($y + $h - 2 * $r)), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Icon([int]$size)
{
    $ss = if ($size -ge 64) { 2 } else { 4 }
    $px = $size * $ss
    $src = New-Object System.Drawing.Bitmap $px, $px, $F32
    $g = [System.Drawing.Graphics]::FromImage($src)
    $g.SmoothingMode = $SM
    $g.InterpolationMode = $IM
    $g.PixelOffsetMode = $POM
    $g.Clear([System.Drawing.Color]::Transparent)

    $u = $px / 100.0
    $detail = if ($size -ge 48) { 2 } elseif ($size -ge 24) { 1 } else { 0 }

    # 底座
    $bx = 2.0 * $u
    $by = 3.0 * $u
    $bw = 96.0 * $u
    $bh = 94.0 * $u
    $base = New-Squircle $bx $by $bw $bh 5.0 260

    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @((C 255 106 192 255), (C 255 52 130 238), (C 255 20 66 184))
    $blend.Positions = @(0.0, 0.52, 1.0)
    $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        (PF ($bx + $bw) $by), (PF $bx ($by + $bh)),
        (C 255 106 192 255), (C 255 20 66 184))
    $grad.InterpolationColors = $blend
    $g.FillPath($grad, $base)
    $grad.Dispose()

    # 底座顶部柔光
    $s1 = $g.Save()
    $g.SetClip($base)
    $hl = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        (PF $bx $by), (PF $bx ($by + $bh * 0.52)), (C 56 255 255 255), (C 0 255 255 255))
    $hl.WrapMode = [System.Drawing.Drawing2D.WrapMode]::TileFlipX
    $g.FillRectangle($hl, (F $bx), (F $by), (F $bw), (F ($bh * 0.52)))
    $hl.Dispose()
    $g.Restore($s1)

    # 窗口叠层：后层暖色 + 前层白色，靠色彩与投影拉出纵深
    $fr = if ($detail -ge 1) { 5.0 * $u } else { 3.6 * $u }
    if ($detail -ge 1)
    {
        $bkx = $bx + $bw * 0.14; $bky = $by + $bh * 0.20
        $bkw = $bw * 0.50;       $bkh = $bh * 0.38
        $back = New-RoundRect $bkx $bky $bkw $bkh (4.4 * $u)
        $bBlend = New-Object System.Drawing.Drawing2D.ColorBlend 2
        $bBlend.Colors = @((C 250 255 200 108), (C 250 240 148 52))
        $bBlend.Positions = @(0.0, 1.0)
        $bGrad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            (PF $bkx $bky), (PF ($bkx + $bkw) ($bky + $bkh)),
            (C 250 255 200 108), (C 250 240 148 52))
        $bGrad.InterpolationColors = $bBlend
        $g.FillPath($bGrad, $back)
        # 后层也带一条标题栏，读起来才是"另一个窗口"而不是一张色块
        $s0 = $g.Save()
        $g.SetClip($back)
        $bBar = New-Object System.Drawing.SolidBrush (C 70 150 62 0)
        $g.FillRectangle($bBar, (F $bkx), (F $bky), (F $bkw), (F ($bkh * 0.26)))
        $bBar.Dispose()
        $g.Restore($s0)
        $bGrad.Dispose(); $back.Dispose()

        $fx = $bx + $bw * 0.28; $fy = $by + $bh * 0.36
        $fw = $bw * 0.60;       $fh = $bh * 0.44
    }
    else
    {
        $fx = $bx + $bw * 0.19; $fy = $by + $bh * 0.27
        $fw = $bw * 0.63;       $fh = $bh * 0.46
    }

    # 前层窗口投影
    if ($detail -ge 1)
    {
        $shp = New-RoundRect ($fx + 1.4 * $u) ($fy + 2.4 * $u) $fw $fh $fr
        $shb = New-Object System.Drawing.SolidBrush (C 62 5 24 66)
        $g.FillPath($shb, $shp)
        $shb.Dispose(); $shp.Dispose()
    }

    $front = New-RoundRect $fx $fy $fw $fh $fr
    $body = New-Object System.Drawing.SolidBrush (C 252 255 255 255)
    $g.FillPath($body, $front)
    $body.Dispose()

    # 标题栏 + 内容（细节分级）
    $barH = if ($detail -ge 2) { $fh * 0.27 } elseif ($detail -ge 1) { $fh * 0.30 } else { $fh * 0.34 }
    $s2 = $g.Save()
    $g.SetClip($front)
    $barBrush = New-Object System.Drawing.SolidBrush (C 255 60 132 232)
    $g.FillRectangle($barBrush, (F $fx), (F $fy), (F $fw), (F $barH))
    $barBrush.Dispose()

    if ($detail -ge 2)
    {
        $dr = [Math]::Max(1.4, 2.15 * $u)
        $dcy = $fy + $barH / 2.0
        $cols = @((C 255 240 102 92), (C 255 244 188 78), (C 255 102 198 108))
        for ($i = 0; $i -lt 3; $i++)
        {
            $dcx = $fx + $fw * 0.13 + $i * ($dr * 2.75)
            $b2 = New-Object System.Drawing.SolidBrush $cols[$i]
            $g.FillEllipse($b2, (F ($dcx - $dr)), (F ($dcy - $dr)), (F ($dr * 2)), (F ($dr * 2)))
            $b2.Dispose()
        }
        $lh = [Math]::Max(1.4, 2.0 * $u)
        $l1 = New-Object System.Drawing.SolidBrush (C 208 120 166 218)
        $l2 = New-Object System.Drawing.SolidBrush (C 152 156 194 232)
        $g.FillRectangle($l1, (F ($fx + $fw * 0.13)), (F ($fy + $barH + $fh * 0.13)), (F ($fw * 0.63)), (F $lh))
        $g.FillRectangle($l2, (F ($fx + $fw * 0.13)), (F ($fy + $barH + $fh * 0.13 + $lh * 2.3)), (F ($fw * 0.40)), (F $lh))
        $l1.Dispose(); $l2.Dispose()
    }
    elseif ($detail -ge 1)
    {
        $lh = [Math]::Max(1.6, 2.6 * $u)
        $l1 = New-Object System.Drawing.SolidBrush (C 200 124 170 220)
        $g.FillRectangle($l1, (F ($fx + $fw * 0.13)), (F ($fy + $barH + $fh * 0.20)), (F ($fw * 0.58)), (F $lh))
        $l1.Dispose()
    }
    else
    {
        $lh = [Math]::Max(2.0, 4.0 * $u)
        $l1 = New-Object System.Drawing.SolidBrush (C 220 84 142 214)
        $g.FillRectangle($l1, (F ($fx + $fw * 0.16)), (F ($fy + $barH + $fh * 0.26)), (F ($fw * 0.52)), (F $lh))
        $l1.Dispose()
    }
    $g.Restore($s2)
    $front.Dispose()

    # 底座轮廓：内圈提亮 + 外圈压暗
    $inner = New-Object System.Drawing.Pen (C 74 255 255 255), (F ([Math]::Max(0.8, 1.4 * $u)))
    $g.DrawPath($inner, $base)
    $inner.Dispose()
    $outer = New-Object System.Drawing.Pen (C 46 6 26 74), (F ([Math]::Max(0.8, 1.1 * $u)))
    $g.DrawPath($outer, $base)
    $outer.Dispose()
    $base.Dispose()
    $g.Dispose()

    if ($ss -eq 1) { return $src }
    $out = New-Object System.Drawing.Bitmap $size, $size, $F32
    $g2 = [System.Drawing.Graphics]::FromImage($out)
    $g2.InterpolationMode = $IM
    $g2.PixelOffsetMode = $POM
    $g2.SmoothingMode = $SM
    $dst = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $from = [System.Drawing.Rectangle]::new(0, 0, $px, $px)
    $g2.DrawImage($src, $dst, $from, [System.Drawing.GraphicsUnit]::Pixel)
    $g2.Dispose()
    $src.Dispose()
    return $out
}

function Get-PngBytes($bmp)
{
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, $PngFmt)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

function Get-DibBytes($bmp)
{
    $w = $bmp.Width
    $h = $bmp.Height
    $rowBytes = [int]([Math]::Ceiling($w / 8.0) * 8)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]0)
    $bw.Write([uint32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([uint32]0)
    $bw.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--)
    {
        for ($x = 0; $x -lt $w; $x++)
        {
            $p = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$p.B)
            $bw.Write([byte]$p.G)
            $bw.Write([byte]$p.R)
            $bw.Write([byte]$p.A)
        }
    }
    for ($y = 0; $y -lt $h; $y++)
    {
        for ($i = 0; $i -lt $rowBytes; $i++) { $bw.Write([byte]0) }
    }
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return ,$bytes
}

$build = Join-Path $PSScriptRoot 'build'
if (-not (Test-Path $build)) { New-Item -ItemType Directory -Path $build | Out-Null }

$frames = @()
foreach ($s in $Sizes)
{
    $bmp = Draw-Icon $s
    $usePng = $s -ge 40
    $bytes = if ($usePng) { Get-PngBytes $bmp } else { Get-DibBytes $bmp }
    $bytes = [byte[]]$bytes
    $frames += [pscustomobject]@{ Size = $s; Bytes = $bytes; Png = $usePng }
    $bmp.Save((Join-Path $build ("icon_" + $s + ".png")), $PngFmt)
    $bmp.Dispose()
    $kind = if ($usePng) { 'PNG' } else { 'BMP' }
    Write-Output ("frame " + $s + 'px ' + $kind + ' ' + $bytes.Length + ' bytes')
}

$outPath = Join-Path $PSScriptRoot 'app.ico'
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames)
{
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$f.Bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f.Bytes) }
$bw.Close()
$fs.Close()

Write-Output ("ICON OK: " + $outPath + " (" + (Get-Item $outPath).Length + " bytes, " + $frames.Count + " frames)")
