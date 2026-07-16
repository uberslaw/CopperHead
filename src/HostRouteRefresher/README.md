# Host Route Refresher

Small Windows admin utility that keeps **/32 host routes** pointed at a chosen NIC (for example a phone tether). It re-resolves hostnames on a timer and adds/removes routes as DNS A records change.

This is **destination-based routing**, not true per-process routing. For a license server (or a Cursor connectivity test) that is usually enough: only those host IPs leave via the tether; everything else stays on the corporate default route.

## What it does / does not do

- Uses built-in `route.exe` only (no DLL injection, no packet divert, no hooks).
- Tracks routes **it** created and removes them on Stop.
- Requires **Administrator** (UAC prompt via manifest).
- Does not hide traffic from corporate monitoring — it only changes where selected destinations egress.

## Build (on Windows)

```bat
cd src\HostRouteRefresher
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\publish
```

Run `publish\HostRouteRefresher.exe` elevated.

## Use

1. Stop any dual-connection / dock switcher that interferes.
2. Enable phone tether; confirm it appears under **Egress adapter** (Refresh NICs if needed).
3. Enter hostnames (one per line), e.g. for a Cursor smoke test:
   - `api2.cursor.sh`
   - `api5.cursor.sh`
4. Set refresh interval (30s is fine).
5. **Start**. Watch the log for `DNS` / `ROUTE` lines.
6. Confirm with `tracert -d <ip>` that those IPs go via the phone gateway.
7. **Stop** when finished (clears managed routes).

Config is saved next to the exe as `config.json`.

## Finding license hostnames

Use vendor firewall/allowlist docs, or Resource Monitor → Network while the app fails, then put those hostnames in the list.

## Notes

- IPv4 A records only.
- If the tether adapter index or gateway changes, the next refresh picks that up.
- CDN hostnames can return several IPs; all current A records are routed. Stale ones are deleted.
- Persistent (`route -p`) routes are intentionally **not** used.
