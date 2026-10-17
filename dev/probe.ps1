$ErrorActionPreference = "Continue"
Set-Location "D:\WindowTool"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = @(Get-ChildItem "src\*.cs" | ForEach-Object { $_.FullName })
$src += (Resolve-Path "dev\ChromeProbe.cs").Path
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
         "-main:WindowTool.ChromeProbe","-win32manifest:app.manifest") + $refs + @("-out:build\probe.exe") + $src
& $csc $all > build\probe.log 2>&1
if ($LASTEXITCODE -ne 0) { Write-Output "PROBE BUILD FAILED"; Get-Content build\probe.log; exit 1 }
Write-Output "PROBE BUILD OK"
cmd /c "build\probe.exe 3500 build\probe.png > build\probe.out 2>&1"
Get-Content build\probe.out
