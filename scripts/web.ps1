#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Hify frontend dev helper: run the Vite dev server / build / preview.

.DESCRIPTION
    Wraps common frontend commands so you don't retype the web/ path and pnpm
    invocations. Auto-runs 'pnpm install' when node_modules is missing.
    The 'dev' task proxies /api (incl. /api/v1/health) to the backend (see vite.config.ts);
    start the backend separately with scripts/dev.ps1.

.PARAMETER Task
    Task to execute: dev (default) | build | preview | install | lint.

.PARAMETER Port
    Listening port for the 'dev' / 'preview' tasks. Default 5173.

.EXAMPLE
    ./scripts/web.ps1               # Run the dev server at http://localhost:5173
.EXAMPLE
    ./scripts/web.ps1 build         # Type-check + production build to web/dist
.EXAMPLE
    ./scripts/web.ps1 preview       # Preview the production build
.EXAMPLE
    ./scripts/web.ps1 dev -Port 5180
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('dev', 'build', 'preview', 'install', 'lint')]
    [string]$Task = 'dev',

    [int]$Port = 5173
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    Write-Error "pnpm not found. Install it first: npm install -g pnpm (or 'corepack enable')."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $repoRoot 'web'

Push-Location $webDir
try {
    # First run (or after a dependency change) needs an install.
    if (-not (Test-Path (Join-Path $webDir 'node_modules')) -and $Task -ne 'install') {
        Write-Host "node_modules missing -> running pnpm install" -ForegroundColor Yellow
        pnpm install
    }

    switch ($Task) {
        'install' {
            pnpm install
        }
        'build' {
            pnpm build
        }
        'lint' {
            pnpm lint
        }
        'preview' {
            Write-Host "Previewing build -> http://localhost:$Port" -ForegroundColor Cyan
            pnpm preview --port $Port
        }
        'dev' {
            Write-Host "Starting Vite dev server -> http://localhost:$Port" -ForegroundColor Cyan
            Write-Host "Proxies /api -> backend (start it via scripts/dev.ps1)" -ForegroundColor DarkGray
            pnpm dev --port $Port
        }
    }
}
finally {
    Pop-Location
}
