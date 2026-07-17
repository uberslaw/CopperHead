#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs the Debugging Windows service and tray application.

.DESCRIPTION
    - Copies binaries to C:\Program Files\Debugging
    - Registers "Debugging" as a Windows service running as LocalSystem
    - Adds the tray app to the current user's login startup
    - Requires local administrator rights
#>

param(
    [string]$InstallRoot = "${env:ProgramFiles}\Debugging",
    [string]$ServiceName = "Debugging",
    [string]$ServiceDisplayName = "Debugging Cisco Service Monitor"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$nugetConfig = Join-Path $repoRoot "nuget.config"

Write-Host "Restoring NuGet packages..."
Push-Location $repoRoot
try {
    if (-not (Test-Path $nugetConfig)) {
        Write-Host "Adding nuget.org package source..."
        dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org | Out-Null
    }

    dotnet restore Debugging.sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed. Ensure this machine can reach https://api.nuget.org."
    }

    Write-Host "Building release..."
    dotnet publish src\Debugging.Service\Debugging.Service.csproj -c Release -r win-x64 --self-contained false -o $InstallRoot
    if ($LASTEXITCODE -ne 0) { throw "Service publish failed." }

    dotnet publish src\Debugging.Tray\Debugging.Tray.csproj -c Release -r win-x64 --self-contained false -o $InstallRoot
    if ($LASTEXITCODE -ne 0) { throw "Tray publish failed." }
}
finally {
    Pop-Location
}

$serviceExe = Join-Path $InstallRoot "Debugging.Service.exe"
$trayExe = Join-Path $InstallRoot "Debugging.Tray.exe"

if (-not (Test-Path $serviceExe)) {
    throw "Service executable not found at $serviceExe"
}

if (-not (Test-Path $trayExe)) {
    throw "Tray executable not found at $trayExe"
}

$dataRoot = Join-Path $env:ProgramData "Debugging"
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating Windows service (LocalSystem)..."
sc.exe create $ServiceName binPath= "`"$serviceExe`"" start= auto DisplayName= "$ServiceDisplayName" obj= LocalSystem | Out-Null
sc.exe description $ServiceName "Monitors Cisco Secure Client services and stops them when they start. Toggle via Debugging tray icon." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "Starting service..."
Start-Service -Name $ServiceName

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
New-ItemProperty -Path $runKey -Name "Debugging" -Value "`"$trayExe`"" -PropertyType String -Force | Out-Null

Write-Host ""
Write-Host "Installation complete."
Write-Host "  Service: $ServiceName (LocalSystem)"
Write-Host "  Files:   $InstallRoot"
Write-Host "  Log:     $(Join-Path $dataRoot 'events.log')"
Write-Host ""
Write-Host "The tray icon will appear after you sign in again, or run:"
Write-Host "  `"$trayExe`""
