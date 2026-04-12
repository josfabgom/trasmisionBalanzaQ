@echo off
echo ==============================================
echo Iniciando Sistema BalanzaQ Portable
echo ==============================================
echo.

cd /d "%~dp0"

echo Iniciando servidor web local...
start http://localhost:5200
BalanzaQ.Web.exe --urls "http://localhost:5200"
exit
