# Rebuilds Secret Base (Debug | x64) and creates Start Menu + Desktop shortcuts
# to that SecretBase.App.exe so you can launch without a terminal.
# Always rebuilds. An existing exe from an older checkout is not reused.
# Usage: .\install-launchers.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $userDotnet "dotnet.exe")) {
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

function Find-SecretBaseAppExe {
    $candidates = @(
        (Join-Path $root "src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\SecretBase.App.exe"),
        (Join-Path $root "src\SecretBase.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\SecretBase.App.exe"),
        # Older layouts without an RID folder (kept for compatibility)
        (Join-Path $root "src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\SecretBase.App.exe"),
        (Join-Path $root "src\SecretBase.App\bin\x64\Release\net10.0-windows10.0.26100.0\SecretBase.App.exe")
    )
    $found = @(
        $candidates |
            Where-Object { Test-Path $_ } |
            ForEach-Object { Get-Item $_ } |
            Sort-Object LastWriteTime -Descending
    )
    if ($found.Count -eq 0) {
        return $null
    }

    return $found[0].FullName
}

$branch = ""
$commit = ""
try {
    $branch = (git rev-parse --abbrev-ref HEAD 2>$null)
    $commit = (git log -1 --format="%h %s" 2>$null)
} catch {
    $branch = ""
    $commit = ""
}

$running = @(Get-Process -Name "SecretBase.App" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "Stopping running SecretBase.App so build outputs are not locked…"
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    $still = @(Get-Process -Name "SecretBase.App" -ErrorAction SilentlyContinue)
    if ($still.Count -gt 0) {
        throw "SecretBase.App is still running and locking DLLs. Close it (Ctrl+Shift+Q), then run .\install-launchers.ps1 again."
    }
}

Write-Host "Cleaning App and Widgets so a stale XAML binary is not reused…"
dotnet clean (Join-Path $root "src\SecretBase.App\SecretBase.App.csproj") -c Debug -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Clean failed (exit $LASTEXITCODE). Shortcuts were not updated."
}
dotnet clean (Join-Path $root "src\SecretBase.Widgets\SecretBase.Widgets.csproj") -c Debug -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Clean failed (exit $LASTEXITCODE). Shortcuts were not updated."
}

Write-Host "Building Secret Base (Debug | x64) from this checkout…"
if ($branch) {
    Write-Host "  Branch: $branch"
    Write-Host "  Commit: $commit"
}
& (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit $LASTEXITCODE). Shortcuts were not updated."
}

$exe = Find-SecretBaseAppExe

if (-not $exe) {
    throw "SecretBase.App.exe was not found after build. Expected under src\SecretBase.App\bin\x64\Debug\...\win-x64\"
}

$workDir = Split-Path -Parent $exe
$launcherDir = Join-Path $env:LOCALAPPDATA "SecretBase"
New-Item -ItemType Directory -Force -Path $launcherDir | Out-Null
$cmdPath = Join-Path $launcherDir "launch-secretbase.cmd"
$legacyVbs = Join-Path $launcherDir "launch-secretbase.vbs"

function Test-Net10Runtime([string]$root) {
    $shared = Join-Path $root "shared\Microsoft.NETCore.App"
    if (-not (Test-Path $shared)) {
        return $false
    }

    return [bool](Get-ChildItem $shared -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "10.*" })
}

$machineDotnet = Join-Path $env:ProgramFiles "dotnet"
$userDotnetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$machineHasNet10 = Test-Net10Runtime $machineDotnet
$userHasNet10 = Test-Net10Runtime $userDotnetRoot
# Same rule as AppHostLaunchScript.SelectDotNetRoot: only force DOTNET_ROOT when the
# user-local folder is the one that actually has .NET 10 and Program Files does not.
$dotnetRoot = $null
if ((-not $machineHasNet10) -and $userHasNet10) {
    $dotnetRoot = $userDotnetRoot.TrimEnd('\')
}

if (Test-Path $legacyVbs) {
    Remove-Item -Force $legacyVbs
}

$shortcutTarget = $exe
$shortcutArgs = ""
$windowStyle = 1
$launchMode = "direct exe"
if ($dotnetRoot) {
    $logDir = Join-Path $env:LOCALAPPDATA "SecretBase\logs"
    $template = @"
@echo off
setlocal EnableExtensions
rem SecretBase.App.exe=$exe
set "SB_EXE=$exe"
set "SB_DIR=$workDir"
set "DOTNET_ROOT=$dotnetRoot"
set "DOTNET_ROOT(x64)=%DOTNET_ROOT%"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "SB_LOG=$logDir"
if not exist "%SB_LOG%" mkdir "%SB_LOG%"
> "%SB_LOG%\launch-last.txt" echo Secret Base launcher
>> "%SB_LOG%\launch-last.txt" echo exe=%SB_EXE%
>> "%SB_LOG%\launch-last.txt" echo DOTNET_ROOT=%DOTNET_ROOT%
if not exist "%SB_EXE%" (
  echo Secret Base executable was not found:
  echo %SB_EXE%
  pause
  exit /b 1
)
start "" /D "%SB_DIR%" "%SB_EXE%" %*
exit /b 0
"@
    # Shift-JIS, no BOM. cmd.exe on Japanese Windows reads this. UTF-16 looks corrupt in Notepad.
    $shiftJis = [System.Text.Encoding]::GetEncoding(932)
    $cmdText = ($template -replace "`r`n", "`n" -replace "`n", "`r`n")
    [System.IO.File]::WriteAllText($cmdPath, $cmdText, $shiftJis)
    $shortcutTarget = Join-Path $env:SystemRoot "System32\cmd.exe"
    $shortcutArgs = "/d /c `"$cmdPath`""
    $windowStyle = 7
    $launchMode = "cmd script sets DOTNET_ROOT"
} elseif (Test-Path $cmdPath) {
    Remove-Item -Force $cmdPath
}

$shell = New-Object -ComObject WScript.Shell

function New-SecretBaseShortcut([string]$path) {
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $shortcutTarget
    $shortcut.Arguments = $shortcutArgs
    $shortcut.WorkingDirectory = $workDir
    $shortcut.WindowStyle = $windowStyle
    $shortcut.Description = "Secret Base — personal desktop overlay"
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
}

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Secret Base.lnk"
$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "Secret Base.lnk"
New-SecretBaseShortcut $startMenu
New-SecretBaseShortcut $desktop

Write-Host ""
Write-Host "Shortcuts created:"
Write-Host "  Start Menu: $startMenu"
Write-Host "  Desktop:    $desktop"
$built = (Get-Item $exe).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
Write-Host "  Shortcut:   $shortcutTarget"
if ($shortcutArgs) {
    Write-Host "  Arguments:  $shortcutArgs"
}
Write-Host "  Starts:     $exe"
Write-Host "  Built:      $built"
Write-Host "  Launch:     $launchMode"
if ($machineHasNet10) {
    Write-Host "  .NET 10:    $machineDotnet"
} elseif ($userHasNet10) {
    Write-Host "  .NET 10:    $userDotnetRoot (Explorer does not see this unless DOTNET_ROOT is set)"
} else {
    Write-Host "  .NET 10:    NOT FOUND under Program Files or %LocalAppData%\Microsoft\dotnet"
    Write-Host "              The exe shows Windows' own '.NET is required' dialog when the runtime is missing."
}
if ($commit) {
    Write-Host "  Commit:     $commit"
}
Write-Host ""
Write-Host "Double-click either shortcut to launch. In the app, open Setup (gear) to enable Start at login."
Write-Host "Opening SecretBase.App.exe in Notepad always looks garbled. It is a program, not a text file."
Write-Host "If the shortcut still flashes: end any SecretBase.App.exe in Task Manager, then open the shortcut again."
Write-Host "After a launch, read %LocalAppData%\SecretBase\logs\startup-last.txt and launch-attempt.txt."
