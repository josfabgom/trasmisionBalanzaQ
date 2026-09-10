@echo off
cd /d "%~dp0"
powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0ActualizadorBQ.ps1"
