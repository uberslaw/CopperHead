# CopperHead

Windows utility that periodically resolves hostnames and updates `/32` routes so selected destinations egress via a chosen adapter (for example a phone tether).

## Project

| Path | Description |
|---|---|
| [`src/CopperHead`](src/CopperHead) | WinForms app — hostname list, route refresh, tracert, discovery, tray icon |
| [`docs/hosts-cursor.txt`](docs/hosts-cursor.txt) | Optional shared Cursor hostname list for **Fetch list** |

See [`src/CopperHead/README.md`](src/CopperHead/README.md) for build and usage.

## Quick start

```powershell
cd C:\Users\today\Cursor\CopperHead\src\CopperHead
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\publish
Start-Process ..\..\publish\CopperHead.exe -Verb RunAs
```

Repository: https://github.com/uberslaw/CopperHead
