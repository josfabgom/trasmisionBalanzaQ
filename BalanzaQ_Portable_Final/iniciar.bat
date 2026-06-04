@echo off
title BalanzaQ Portable Launcher
cd /d "%~dp0"

echo Comprobando si BalanzaQ ya esta en ejecucion...
tasklist /FI "IMAGENAME eq BalanzaQ.Web.exe" 2>NUL | find /I /N "BalanzaQ.Web.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo El servidor ya se encuentra en ejecucion.
) else (
    echo Iniciando servidor BalanzaQ...
    start "" "BalanzaQ.Web.exe"
    echo Esperando a que el servidor arranque...
    timeout /t 3 /nobreak > NUL
)

echo Abriendo la aplicacion en el navegador...
start http://localhost:5200
