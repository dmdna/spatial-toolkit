@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0pico-dev.ps1" %*
exit /b %errorlevel%
