$ErrorActionPreference = "Continue"
Set-Location "D:\WindowTool"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = @(Get-ChildItem "src\*.cs" | ForEach-Object { $_.FullName })
$src += (Resolve-Path "dev\RenderHarness.cs").Path
$refs = @(
  "-r:System.dll","-r:System.Core.dll","-r:System.Drawing.dll","-r:System.Windows.Forms.dll",
  "-r:System.Xml.dll","-r:System.IO.Compression.dll","-r:System.IO.Compression.FileSystem.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsFormsIntegration.dll",
  "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"
)
$all = @("-nologo","-codepage:65001","-target:exe","-platform:anycpu","-optimize+",
         "-main:WindowTool.RenderHarness") + $refs + @("-out:build\render.exe") + $src
& $csc $all > build\render.log 2>&1
if ($LASTEXITCODE -ne 0) { Write-Output "HARNESS BUILD FAILED"; Get-Content build\render.log; exit 1 }
cmd /c "build\render.exe build\shots > build\shots.log 2>&1"
Get-Content build\shots.log
Get-ChildItem build\shots\*.png | ForEach-Object { "{0}  {1}" -f $_.LastWriteTime.ToString("HH:mm:ss"), $_.Name }
