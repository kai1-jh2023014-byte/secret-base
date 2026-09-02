# Runs unit tests for the Secret Base solution.
# Usage: .\test.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $userDotnet "dotnet.exe")) {
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

$sln = Join-Path $root "SecretBase.sln"
if (-not (Test-Path $sln)) {
    throw "Solution not found at: $sln"
}

dotnet test $sln -c Debug -p:Platform=x64
