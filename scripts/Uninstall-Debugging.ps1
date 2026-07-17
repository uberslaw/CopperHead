#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Uninstalls the Debugging Windows service and tray application.
#>

param(
    [string]$InstallRoot = "${env:ProgramFiles}\Debugging",
    [string]$ServiceName = "Debugging",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping and removing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $runKey -Name "Debugging" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name "Debugging"
}

Get-Process -Name "Debugging.Tray" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $InstallRoot) {
    Write-Host "Removing $InstallRoot..."
    Remove-Item -Path $InstallRoot -Recurse -Force
}

if ($RemoveData) {
    $dataRoot = Join-Path $env:ProgramData "Debugging"
    if (Test-Path $dataRoot) {
        Write-Host "Removing $dataRoot..."
        Remove-Item -Path $dataRoot -Recurse -Force
    }
}

Write-Host "Uninstall complete."
