@echo off
cls

REM ================================================================
REM          Gov2Biz - Quick Start for Windows
REM          Multi-Tenant License Management System
REM ================================================================

echo.
echo ================================================================
echo           Gov2Biz - Starting Application
echo ================================================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running!
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo [OK] Docker is running
echo.

REM Stop existing containers
echo [STOP] Stopping existing containers...
docker-compose down >nul 2>&1

REM Start services
echo [START] Starting Gov2Biz services...
docker-compose up -d

REM Wait for services
echo.
echo [WAIT] Waiting for services to start...
timeout /t 15 /nobreak >nul

REM Initialize database
echo [DB] Initializing database...
docker exec gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd123" -C -i /Scripts/Gov2Biz_Full_Database_Setup.sql >nul 2>&1
if errorlevel 1 (
    echo [INFO] Database already initialized
) else (
    echo [OK] Database setup complete
)

REM Check service health
echo.
echo [CHECK] Verifying service status...
curl -s -o nul -w "%%{http_code}" http://localhost:5005 >nul 2>&1
if errorlevel 1 (
    echo [WARN] Frontend: Starting...
) else (
    echo [OK] Frontend: Running
)

echo.
echo ================================================================
echo                 Application Started!
echo ================================================================
echo.
echo   Web Application:  http://localhost:5005
echo   API Gateway:      http://localhost:8000
echo.
echo   Test Credentials:
echo     User:  testuser@example.com / Password123!
echo     Admin: admin@test.com / Password123!
echo.
echo ================================================================
echo.
echo   View Logs: docker-compose logs -f
echo   Stop App:  docker-compose down
echo.
pause
