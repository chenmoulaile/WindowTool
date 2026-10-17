param([string]$src,[string]$out,[int]$x,[int]$y,[int]$w,[int]$h,[int]$z=2)
Add-Type -AssemblyName System.Drawing
$bmp=[System.Drawing.Bitmap]::FromFile($src)
$rect=New-Object System.Drawing.Rectangle $x,$y,$w,$h
$crop=New-Object System.Drawing.Bitmap ($w*$z),($h*$z)
$g=[System.Drawing.Graphics]::FromImage($crop)
$g.InterpolationMode='NearestNeighbor'
$g.DrawImage($bmp,(New-Object System.Drawing.Rectangle 0,0,($w*$z),($h*$z)),$rect,[System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose();$bmp.Dispose()
$crop.Save($out);$crop.Dispose()
"saved $out"
