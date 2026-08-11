# Builds and runs Secret Base (same as the known-good foundation launch).
# Usage (from anywhere):  .\run.ps1
# Or from repo root:      .\run.ps1

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

Write-Host "Starting Secret Base..."
dotnet run --project $project -c Debug -p:Platform=x64
