@echo off
rem 重新编译 HDR转SDR.exe
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32manifest:app.manifest ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    "/out:..\HDR转SDR.exe" HDR2SDR.cs
echo.
echo 编译结束，退出码 %ERRORLEVEL%
pause