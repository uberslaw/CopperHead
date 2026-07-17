# NoFilter

A lightweight Windows application that monitors four Cisco Secure Client services, stops them when they start, and learns their restart interval so it does not need to poll constantly.

Repository: [NoFilter](https://github.com/uberslaw/NoFilter)

## What it does

**NoFilter** runs as a real Windows service under **Local System** (full local admin rights for stopping services). A small **system tray app** starts at login so you can toggle monitoring on/off and view the event log.

Monitored Cisco services:

| Display name | Typical service name |
|---|---|
| Cisco Secure Client - ThousandEyes Endpoint Agent | `csc_te_agent` |
| Cisco Secure Client - Umbrella Agent | `csc_umbrellaagent` |
| Cisco Secure Client - Umbrella SWG Agent | `csc_swgagent` |
| Cisco Secure Client - Zero Trust Access Agent | `csc_zta_agent` |

If a service name differs on your machine, the app falls back to matching by display name.

## Features

- **Low resource usage**: starts with 15-second polling while learning restart patterns, then switches to adaptive sleep (up to 5 minutes between checks) once it detects a regular interval.
- **Event log**: records when each service was stopped, when it next tried to start, and the gap between those events.
- **Interval learning**: after 3 restart samples, estimates the median restart interval per service.
- **Tray toggle**: right-click the tray icon to enable/disable monitoring without uninstalling.
- **Login startup**: tray app is registered in `HKCU\...\Run` during install.

## Requirements

- Windows 10/11
- .NET 8 Runtime (installed automatically if you use the publish output on a machine with .NET)
- Local administrator rights to install the Windows service

## Build

```powershell
dotnet restore
dotnet build Debugging.sln -c Release
```

## Install (run PowerShell as Administrator)

```powershell
.\scripts\Install-Debugging.ps1
```

This will:

1. Publish `Debugging.Service.exe` and `Debugging.Tray.exe` to `C:\Program Files\Debugging`
2. Register the **Debugging** Windows service (`LocalSystem`, automatic start)
3. Start the service
4. Add the tray app to your login startup

## Uninstall

```powershell
.\scripts\Uninstall-Debugging.ps1
```

Add `-RemoveData` to also delete logs and state under `%ProgramData%\Debugging`.

## Tray usage

- **Double-click** tray icon: open log viewer
- **Disable monitoring**: pauses stop actions (service keeps running)
- **Enable monitoring**: resumes stop actions
- **Open log file**: opens `%ProgramData%\Debugging\events.log`

## Logs and state

| Path | Purpose |
|---|---|
| `%ProgramData%\Debugging\events.log` | Human-readable event log |
| `%ProgramData%\Debugging\state.json` | Monitoring enabled/disabled state |

Example log lines:

```text
2026-07-12 14:05:10 | Umbrella Agent: startup detected. Gap since last stop: 5.2m. Estimated restart interval: 5.1m. Samples: 5.0m, 5.2m, 5.1m
2026-07-12 14:05:10 | Umbrella Agent: stopped.
```

## Architecture

```text
+---------------------+       named pipe        +----------------------+
| Debugging.Tray.exe  | <---------------------> | Debugging.Service    |
| (runs at login)     |       + state.json      | (Windows Service,    |
| system tray UI      |                         |  LocalSystem)        |
+---------------------+                         +----------------------+
                                                         |
                                                         v
                                              Stop Cisco services when running
```

## Manual service commands

```powershell
Get-Service Debugging
Start-Service Debugging
Stop-Service Debugging
```

## Notes

- Stopping Cisco services may conflict with corporate endpoint policy or tamper protection. Use only where permitted.
- If Cisco lockdown scripts protect service stop permissions, Local System may still be able to stop them, but your environment may vary.
- Verify exact service names on your PC:

```powershell
Get-Service | Where-Object { $_.DisplayName -like '*Cisco Secure Client*' } | Select-Object Name, DisplayName, Status
```
