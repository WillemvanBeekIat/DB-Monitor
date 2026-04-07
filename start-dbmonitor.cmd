@echo off
REM Start DbMonitor with PostgreSQL in Docker
REM For more options, use Start-DbMonitor.ps1 in PowerShell

echo [DbMonitor] Starting PostgreSQL container...
docker compose up -d

echo [DbMonitor] Waiting for PostgreSQL to be ready...
:wait_loop
docker inspect --format="{{.State.Health.Status}}" dbmonitor-postgres 2>nul | findstr "healthy" >nul
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

echo [DbMonitor] PostgreSQL is ready!
echo.
echo Connection Details:
echo   Host: localhost
echo   Port: 5432
echo   Database: dbmonitor
echo   Username: dbmonitor
echo   Password: dbmonitor_dev_password
echo.
echo [DbMonitor] Starting DbMonitor application...
echo.

cd /d "%~dp0src\DbMonitor.Web"
dotnet run
