@echo off
chcp 65001 >nul
title HDR to SDR
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0hdr2sdr.ps1" %*
echo.
pause
