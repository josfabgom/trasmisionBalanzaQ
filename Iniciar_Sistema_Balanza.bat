@echo off
echo ==============================================
echo Iniciando Sistema BalanzaQ Central
echo ==============================================
echo.

cd /d "%~dp0\BalanzaQ.Web"

echo Restaurando paquetes...
dotnet build

echo Iniciando servidor web local...
start http://localhost:5200
dotnet run --urls "http://localhost:5200"

pause
