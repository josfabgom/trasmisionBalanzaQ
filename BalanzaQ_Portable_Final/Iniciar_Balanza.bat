@echo off
title BalanzaQ Portable
echo ==============================================
echo   BalanzaQ - Sistema de Gestion de Balanzas
echo ==============================================
echo.
echo Iniciando servidor en puerto 5050...
echo El navegador se abrira automaticamente en unos segundos.
echo.
echo [Presione Ctrl+C para cerrar el sistema]
echo.

:: Abrir navegador con retraso
start "" http://localhost:5050

:: Ejecutar aplicacion
BalanzaQ.Web.exe --urls="http://localhost:5050"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: El sistema no pudo iniciarse correctamente.
    echo Verifique que el puerto 5050 no este ocupado.
    echo.
    pause
)
