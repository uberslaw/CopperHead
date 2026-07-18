# CopperHead — usage notes

Windows admin utility that keeps **/32 host routes** pointed at a chosen NIC (for example a phone tether). It re-resolves hostnames on a timer and adds/removes routes as DNS A records change.

## Build (Windows)

From the repo root (`C:\Users\today\Cursor\CopperHead`):

```bat
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
```

Run elevated:

```bat
.\publish\CopperHead.exe
```

## Use

1. Stop any dual-connection / dock switcher.
2. Enable phone tether; pick it under **Egress adapter**.
3. Enter hostnames (one per line). Edit freely; click **Apply now** or wait for the next interval.
4. **Start** for continuous refresh (Discover/Traffic pair when watching processes).
5. Enter a target and click **Tracert** — confirm the first hop is the phone gateway.
6. **Stop** when finished.

Config is saved next to the exe as `config.json`. Logs go under `publish\logs\{ProcessName}\`.

## Shared host list

[`hosts-cursor.txt`](hosts-cursor.txt) — raw URL example:

`https://raw.githubusercontent.com/uberslaw/CopperHead/master/docs/hosts-cursor.txt`
