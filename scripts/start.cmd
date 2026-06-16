@echo off
rem Hify one-shot dev launcher.
rem   1) build backend          2) start backend in background
rem   3) poll /api/v1/health     4) start frontend dev server
rem   5) poll frontend + open browser
rem Any failed step aborts with a message and cleans up started windows.
rem Usage: scripts\start.cmd
setlocal EnableExtensions EnableDelayedExpansion

rem --- config ---
set "BACKEND_PORT=5155"
set "FRONTEND_PORT=5173"
set "MAX_WAIT=60"
set "HEALTH_URL=http://localhost:%BACKEND_PORT%/api/v1/health"
set "FRONTEND_URL=http://localhost:%FRONTEND_PORT%/"
set "BACKEND_TITLE=Hify Backend %BACKEND_PORT%"
set "FRONTEND_TITLE=Hify Frontend %FRONTEND_PORT%"

set "SCRIPT_DIR=%~dp0"
set "ROOT=%SCRIPT_DIR%.."
set "HOST_PROJECT=%ROOT%\src\Hify.Host\Hify.Host.csproj"
set "WEB_DIR=%ROOT%\web"

rem --- 0. prerequisites ---
where dotnet >nul 2>nul || goto no_dotnet
where pnpm   >nul 2>nul || goto no_pnpm
where curl   >nul 2>nul || goto no_curl

rem --- 1. build backend ---
echo [1/5] Building backend...
dotnet build "%HOST_PROJECT%" -v q --nologo
if errorlevel 1 goto build_failed

rem --- 2. start backend in background ---
echo [2/5] Starting backend -^> http://localhost:%BACKEND_PORT%
set "ASPNETCORE_ENVIRONMENT=Development"
set "ASPNETCORE_URLS=http://localhost:%BACKEND_PORT%"
start "%BACKEND_TITLE%" dotnet run --project "%HOST_PROJECT%" --no-launch-profile --no-build

rem --- 3. wait for backend health ---
echo [3/5] Waiting for backend health at %HEALTH_URL%
set /a TRIES=0
:poll_health
curl -sf -o nul "%HEALTH_URL%"
if not errorlevel 1 goto health_ok
set /a TRIES+=1
if %TRIES% geq %MAX_WAIT% goto backend_unhealthy
rem ~1s delay; ping is robust even when stdin is redirected (timeout is not)
ping -n 2 127.0.0.1 >nul
goto poll_health
:health_ok
echo       Backend healthy.

rem --- 4. ensure deps + start frontend in background ---
if exist "%WEB_DIR%\node_modules" goto deps_ok
echo       Installing frontend deps...
pushd "%WEB_DIR%"
call pnpm install
if errorlevel 1 goto install_failed
popd
:deps_ok
echo [4/5] Starting frontend -^> %FRONTEND_URL%
pushd "%WEB_DIR%"
start "%FRONTEND_TITLE%" cmd /k "pnpm dev --port %FRONTEND_PORT%"
popd

rem --- 5. wait for frontend, open browser ---
echo [5/5] Waiting for frontend at %FRONTEND_URL%
set /a TRIES=0
:poll_front
curl -sf -o nul "%FRONTEND_URL%"
if not errorlevel 1 goto front_ok
set /a TRIES+=1
if %TRIES% geq %MAX_WAIT% goto frontend_failed
ping -n 2 127.0.0.1 >nul
goto poll_front
:front_ok
echo       Frontend up. Opening browser...
start "" "%FRONTEND_URL%"

echo.
echo All set. Backend window: "%BACKEND_TITLE%"  Frontend window: "%FRONTEND_TITLE%"
echo Close those two windows to stop the servers.
exit /b 0

rem ============================ failure handlers ============================
:no_dotnet
echo [ERROR] dotnet SDK not found on PATH.
exit /b 1

:no_pnpm
echo [ERROR] pnpm not found on PATH. Install it: npm i -g pnpm  or  corepack enable
exit /b 1

:no_curl
echo [ERROR] curl not found on PATH ^(needed for health polling^).
exit /b 1

:build_failed
echo [ERROR] Backend build failed. See output above.
exit /b 1

:backend_unhealthy
echo [ERROR] Backend not healthy after %MAX_WAIT%s. Check the "%BACKEND_TITLE%" window.
echo         Common cause: missing DB password. Set it once:
echo         dotnet user-secrets set "Database:Password" "your-local-password" --project src\Hify.Host
call :cleanup
exit /b 1

:install_failed
popd
echo [ERROR] pnpm install failed. See output above.
call :cleanup
exit /b 1

:frontend_failed
echo [ERROR] Frontend did not start within %MAX_WAIT%s. Check the "%FRONTEND_TITLE%" window.
call :cleanup
exit /b 1

:cleanup
echo       Cleaning up started windows...
taskkill /FI "WINDOWTITLE eq %BACKEND_TITLE%*" /T /F >nul 2>nul
taskkill /FI "WINDOWTITLE eq %FRONTEND_TITLE%*" /T /F >nul 2>nul
goto :eof
