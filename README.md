# CopperHead

Windows utility that periodically resolves hostnames and updates `/32` routes so selected destinations egress via a chosen adapter (for example a phone tether).

## Project

| Path | Description |
|---|---|
| [`src/CopperHead`](src/CopperHead) | WinForms app — hostname list, route refresh, tracert, tray icon |

Full docs live on the `cursor/hostname-route-refresher-4c5d` branch until merged:

https://github.com/uberslaw/CopperHead/tree/cursor/hostname-route-refresher-4c5d

## Quick start (after checking out the app branch)

```powershell
git checkout cursor/hostname-route-refresher-4c5d
cd src\CopperHead
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\publish
Start-Process ..\..\publish\CopperHead.exe -Verb RunAs
```

Repository: https://github.com/uberslaw/CopperHead
