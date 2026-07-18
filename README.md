# CopperHead

Windows utility that periodically resolves hostnames and updates `/32` routes so selected destinations egress via a chosen adapter (for example a phone tether).

## Quick start

```powershell
cd C:\Users\today\Cursor\CopperHead
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
Start-Process .\publish\CopperHead.exe -Verb RunAs
```

## Features

- Configurable hostname list (edit anytime while running)
- **Apply now** — refresh routes immediately after editing domains
- Periodic DNS refresh
- **Tracert** / **Stop tracert**
- **Discover** — newly vs previously discovered endpoints; paired with Traffic
- **Traffic** — TX/s, RX/s, session/all-time totals; sort + pin favourites
- **Logs** — per-process history with HTML reports
- **Fetch list** — merge hosts from a raw git URL
- Custom tray / window icon

## Shared host list

Starter Cursor list: [`docs/hosts-cursor.txt`](docs/hosts-cursor.txt)

More detail: [`docs/USAGE.md`](docs/USAGE.md)

Repository: https://github.com/uberslaw/CopperHead
