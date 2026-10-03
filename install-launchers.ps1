# Builds Secret Base (if needed) and creates Start Menu + Desktop shortcuts
# to SecretBase.App.exe so you can launch without a terminal.
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
    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

$exe = Find-SecretBaseAppExe
if (-not $exe) {
    Write-Host "Building Secret Base (Debug | x64)…"
    & (Join-Path $root "build.ps1")
    $exe = Find-SecretBaseAppExe
}

if (-not $exe) {
    throw "SecretBase.App.exe was not found after build. Expected under src\SecretBase.App\bin\x64\Debug\...\win-x64\"
}

$workDir = Split-Path -Parent $exe
$launcherDir = Join-Path $env:LOCALAPPDATA "SecretBase"
New-Item -ItemType Directory -Force -Path $launcherDir | Out-Null
$vbsPath = Join-Path $launcherDir "launch-secretbase.vbs"
$dotnetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$dotnetBlock = ""
if (Test-Path (Join-Path $dotnetRoot "dotnet.exe")) {
    $dotnetBlock = "env(""DOTNET_ROOT"") = ""$dotnetRoot""`r`n" +
        "env(""DOTNET_ROOT(x64)"") = env(""DOTNET_ROOT"")`r`n" +
        "env(""PATH"") = env(""DOTNET_ROOT"") & "";"" & env(""PATH"")`r`n"
}
$template = @'
Option Explicit
' SecretBase.App.exe=__EXE__
Dim shell, env, cmd, i
Set shell = CreateObject("WScript.Shell")
Set env = shell.Environment("Process")
__DOTNET__shell.CurrentDirectory = "__WORKDIR__"
cmd = """" & "__EXE__" & """"
For i = 0 To WScript.Arguments.Count - 1
  cmd = cmd & " """ & Replace(WScript.Arguments(i), """", """""") & """"
Next
shell.Run cmd, 1, False
'@
$vbs = $template.Replace('__EXE__', $exe).Replace('__WORKDIR__', $workDir).Replace('__DOTNET__', $dotnetBlock)
Set-Content -Path $vbsPath -Value $vbs -Encoding ASCII

$wscript = Join-Path $env:SystemRoot "System32\wscript.exe"
$shell = New-Object -ComObject WScript.Shell

function New-SecretBaseShortcut([string]$path) {
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $wscript
    $shortcut.Arguments = "//B //Nologo `"$vbsPath`""
    $shortcut.WorkingDirectory = $workDir
    $shortcut.WindowStyle = 1
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
Write-Host "  Target:     $exe"
Write-Host "  Launcher:   $vbsPath"
Write-Host ""
Write-Host "Double-click either shortcut to launch. In the app, open Setup (gear) to enable Start at login."
