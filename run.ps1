# Builds (via dotnet run) and launches Secret Base.
# Prefer install-launchers.ps1 + the Start Menu / Desktop shortcut for daily use.
# Usage (from anywhere):  .\run.ps1
# Or from repo root:      .\run.ps1
#
# Optional:
#   .\run.ps1 -ExeOnly     # launch the built SecretBase.App.exe (required for autostart registration)
#   .\run.ps1 -InstallLaunchers

param(
    [switch]$ExeOnly,
    [switch]$InstallLaunchers
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $userDotnet "dotnet.exe")) {
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

$project = Join-Path $root "src\SecretBase.App\SecretBase.App.csproj"
if (-not (Test-Path $project)) {
    throw "SecretBase.App project not found at: $project"
}

if ($InstallLaunchers) {
    & (Join-Path $root "install-launchers.ps1")
}

function Find-SecretBaseAppExe {
    $candidates = @(
        (Join-Path $root "src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\SecretBase.App.exe"),
        (Join-Path $root "src\SecretBase.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\SecretBase.App.exe"),
        (Join-Path $root "src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\SecretBase.App.exe"),
        (Join-Path $root "src\SecretBase.App\bin\x64\Release\net10.0-windows10.0.26100.0\SecretBase.App.exe")
    )
    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ($ExeOnly) {
    Write-Host "Building Secret Base…"
    & (Join-Path $root "build.ps1")
    $exe = Find-SecretBaseAppExe
    if (-not $exe) {
        throw "SecretBase.App.exe missing after build. Expected under src\SecretBase.App\bin\x64\Debug\...\win-x64\"
    }

    Write-Host "Starting SecretBase.App.exe (autostart-compatible)…"
    Write-Host "  $exe"
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
    return
}

Write-Host "Starting Secret Base…"
Write-Host "Tip: use .\run.ps1 -ExeOnly for Start-at-login, or .\install-launchers.ps1 for Start Menu / Desktop shortcuts."
dotnet run --project $project -c Debug -p:Platform=x64
