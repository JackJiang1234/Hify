@echo off
rem Hify local dev helper: run the Host / run tests / build.
rem Usage: dev.cmd [run|test|build] [port]
rem   dev.cmd              Run the Host at http://localhost:5155
rem   dev.cmd test         Run all tests
rem   dev.cmd build        Build the solution
rem   dev.cmd run 5090     Run the Host on a custom port
setlocal

set "TASK=%~1"
if "%TASK%"=="" set "TASK=run"
set "PORT=%~2"
if "%PORT%"=="" set "PORT=5155"

set "SCRIPT_DIR=%~dp0"
set "SOLUTION=%SCRIPT_DIR%..\Hify.sln"
set "HOST_PROJECT=%SCRIPT_DIR%..\src\Hify.Host\Hify.Host.csproj"

if /I "%TASK%"=="build" goto :build
if /I "%TASK%"=="test" goto :test
if /I "%TASK%"=="run" goto :run

echo Unknown task: %TASK%
echo Usage: dev.cmd [run^|test^|build] [port]
exit /b 1

:build
dotnet build "%SOLUTION%"
exit /b %ERRORLEVEL%

:test
dotnet test "%SOLUTION%"
exit /b %ERRORLEVEL%

:run
set "ASPNETCORE_ENVIRONMENT=Development"
set "ASPNETCORE_URLS=http://localhost:%PORT%"
echo Starting Hify.Host (Development) -^> http://localhost:%PORT%
echo Health check -^> http://localhost:%PORT%/api/v1/health
echo If startup fails due to a missing DB password, run:
echo   dotnet user-secrets set "Database:Password" "<local-password>" --project src/Hify.Host
dotnet run --project "%HOST_PROJECT%" --no-launch-profile
exit /b %ERRORLEVEL%
