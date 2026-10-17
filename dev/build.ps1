$ErrorActionPreference = "Stop"
Set-Location "D:\WindowTool"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$src = Get-ChildItem "src\*.cs" | ForEach-Object { $_.FullName }
$refs = @(
  "-r:System.dll","-r:System.Core.dll","-r:System.Drawing.dll","-r:System.Windows.Forms.dll",
  "-r:System.Xml.dll","-r:System.IO.Compression.dll","-r:System.IO.Compression.FileSystem.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsFormsIntegration.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"
)
$common = @("-nologo","-codepage:65001","-target:winexe","-platform:anycpu","-optimize+",
            "-win32icon:app.ico","-win32manifest:app.manifest")
$out = if ($args[0]) { $args[0] } else { "build\WindowTool.exe" }
$all = $common + $refs + @("-out:$out") + $src
& $csc $all 2>&1 | Out-String -Width 400 | Write-Output
if ($LASTEXITCODE -eq 0) { Write-Output "BUILD OK -> $out" } else { Write-Output "BUILD FAILED ($LASTEXITCODE)" }
exit $LASTEXITCODE
