@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsFormsIntegration.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"

set ICO=
if exist app.ico set ICO=/win32icon:app.ico

"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ %ICO% /win32manifest:app.manifest /out:WindowTool.exe %REFS% src\*.cs
if %errorlevel%==0 (
  echo BUILD OK - WindowTool.exe
) else (
  echo BUILD FAILED
  exit /b 1
)
