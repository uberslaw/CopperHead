# CopperHead

Windows admin utility that keeps **/32 host routes** pointed at a chosen NIC (for example a phone tether). It re-resolves hostnames on a timer and adds/removes routes as DNS A records change.

## Features

- Configurable hostname list (edit anytime while running)
- **Apply now** — refresh routes immediately after editing domains
- Periodic DNS refresh
- **Tracert** — pins the target via the selected NIC, then streams live `tracert -d` output (first hop should be your tether gateway)
- **Processes** tab — detect running processes (name, PID, path), filter, track selected
- **Discover** tab — watch tracked process TCP connections + DNS cache; newly vs previously discovered; paired with Traffic
- **Traffic** tab — TX/s, RX/s, session and all-time totals per IP:port; sortable columns; pin favourites to top
- **Logs** tab — per-process JSONL history with HTML reports
- **Fetch list** — pull a shared hostname file from a raw git URL and merge
- Custom CopperHead tray / window icon (`Assets/copperhead.ico`)
- Stop clears only routes CopperHead created

## Shared host list (optional)

A starter Cursor list lives at [`docs/hosts-cursor.txt`](../../docs/hosts-cursor.txt). After push, point **Host list URL** at the raw GitHub URL for that file (or your own fork/repo).

## Build (Windows)

```bat
cd src\CopperHead
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\publish
```

Run elevated:

```bat
..\..\publish\CopperHead.exe
```

## Use

1. Stop any dual-connection / dock switcher.
2. Enable phone tether; pick it under **Egress adapter**.
3. Enter hostnames (one per line). Edit freely; click **Apply now** or wait for the next interval.
4. **Start** for continuous refresh.
5. Enter a target and click **Tracert** — confirm the first hop is the phone gateway.
6. **Stop** when finished.

Config is saved next to the exe as `config.json`.

## MSI packaging (not included)

Straightforward options later if you want an installer:

| Approach | Effort | Notes |
|---|---|---|
| **Single-folder publish** (what you have) | None | Zip `publish\` and run `CopperHead.exe` |
| **WiX Toolset** v4 / HeatWave | Medium | Free, industry standard MSI; ~1 afternoon for a basic install |
| **Advanced Installer / InstallShield** | Easy UI | Paid; drag-drop MSI with shortcuts/UAC |
| `dotnet` + **MSIX** | Medium | Modern store-style package; different from classic MSI |

For a short deadline, zipping the publish folder is usually enough. MSI is worth it when you need Add/Remove Programs, Start Menu shortcuts, or IT-friendly deployment.
