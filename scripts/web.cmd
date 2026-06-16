@echo off
rem Hify frontend dev helper: run the Vite dev server / build / preview.
rem Usage: web.cmd [dev|build|preview|install|lint] [port]
rem   web.cmd              Run the dev server at http://localhost:5173
rem   web.cmd build        Type-check + production build to web\dist
rem   web.cmd preview      Preview the production build
rem   web.cmd install      Install dependencies
rem   web.cmd dev 5180     Run the dev server on a custom port
setlocal

set "TASK=%~1"
if "%TASK%"=="" set "TASK=dev"
set "PORT=%~2"
if "%PORT%"=="" set "PORT=5173"

set "SCRIPT_DIR=%~dp0"
set "WEB_DIR=%SCRIPT_DIR%..\web"

where pnpm >nul 2>nul
if errorlevel 1 (
    echo pnpm not found. Install it first: npm install -g pnpm ^(or "corepack enable"^).
    exit /b 1
)

pushd "%WEB_DIR%"

if /I "%TASK%"=="install" goto :install
if not exist "node_modules" (
    echo node_modules missing -^> running pnpm install
    call pnpm install
)

if /I "%TASK%"=="build" goto :build
if /I "%TASK%"=="lint" goto :lint
if /I "%TASK%"=="preview" goto :preview
if /I "%TASK%"=="dev" goto :dev

echo Unknown task: %TASK%
echo Usage: web.cmd [dev^|build^|preview^|install^|lint] [port]
popd
exit /b 1

:install
call pnpm install
goto :done

:build
call pnpm build
goto :done

:lint
call pnpm lint
goto :done

:preview
echo Previewing build -^> http://localhost:%PORT%
call pnpm preview --port %PORT%
goto :done

:dev
echo Starting Vite dev server -^> http://localhost:%PORT%
echo Proxies /api and /health  -^> backend (start it via scripts\dev.cmd)
call pnpm dev --port %PORT%
goto :done

:done
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%
