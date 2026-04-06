@echo off
cd /d "%~dp0"
echo Iniciando Sistema BalanzaQ Portable Final...
start "" "BalanzaQ.Web.exe" --urls "http://0.0.0.0:5069"
timeout /t 5
start http://localhost:5069
pause
