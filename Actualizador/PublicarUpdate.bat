@echo off
cd /d "%~dp0"
powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Normal -File "%~dp0PublicarUpdate.ps1"
