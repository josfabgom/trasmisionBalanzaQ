@echo off
echo Iniciando Sistema BalanzaQ Portable...
start "" "BalanzaQ.Web.exe" --urls "http://0.0.0.0:5069"
timeout /t 5
start http://localhost:5069
echo Sistema iniciado en http://localhost:5069
pause
