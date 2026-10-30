@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

rem v2.0 起不再引用任何 WPF 程序集与 System.IO.Compression（视频背景 / 竞赛环境已移除）
set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll

set ICO=
if exist app.ico set ICO=/win32icon:app.ico /resource:app.ico,WindowTool.tray.ico

"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ %ICO% /win32manifest:app.manifest /out:WindowTool.exe %REFS% src\*.cs
if %errorlevel%==0 (
  echo BUILD OK - WindowTool.exe
) else (
  echo BUILD FAILED
  exit /b 1
)
