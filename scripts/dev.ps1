#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Hify local dev helper: run the Host / run tests / build.

.DESCRIPTION
    Wraps common dev commands so you don't retype env vars and project paths.
    The 'run' task starts the Host in the Development environment
    (loads the database password from User Secrets).

.PARAMETER Task
    Task to execute: run (default) | test | build.

.PARAMETER Port
    Listening port for the 'run' task. Default 5080.

.EXAMPLE
    ./scripts/dev.ps1               # Run the Host at http://localhost:5080
.EXAMPLE
    ./scripts/dev.ps1 test          # Run all tests
.EXAMPLE
    ./scripts/dev.ps1 build         # Build the solution
.EXAMPLE
    ./scripts/dev.ps1 run -Port 5090
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('run', 'test', 'build')]
    [string]$Task = 'run',

    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Hify.sln'
$hostProject = Join-Path $repoRoot 'src/Hify.Host/Hify.Host.csproj'

switch ($Task) {
    'build' {
        dotnet build $solution
    }
    'test' {
        dotnet test $solution
    }
    'run' {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ASPNETCORE_URLS = "http://localhost:$Port"

        Write-Host "Starting Hify.Host (Development) -> http://localhost:$Port" -ForegroundColor Cyan
        Write-Host "Health check                    -> http://localhost:$Port/health" -ForegroundColor Cyan
        Write-Host "If startup fails due to a missing DB password, run:" -ForegroundColor DarkGray
        Write-Host "  dotnet user-secrets set `"Database:Password`" `"<local-password>`" --project src/Hify.Host" -ForegroundColor DarkGray

        dotnet run --project $hostProject --no-launch-profile
    }
}
