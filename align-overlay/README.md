# AlignOverlay

Standalone transparent Windows desktop overlay for aligning UI against other apps.

This tool is **not** part of the CopperHead routing app. It lives in its own folder (`align-overlay/`) with its own package name, window title, and build output so the two projects do not share branding or runtime code.

## Features

- **Transparent always-on-top overlay** — resize freely over any app
- **Adjustable grid** — resolution, line thickness, colors, major-line interval
- **Measure tool** — click two points for straight-line distance in pixels, plus approximate mm/cm/inches (Windows 96 DPI mapping, with optional PPI override)
- **Sticky alignment** — pin a vertical or horizontal gridline to the screen; move/resize other edges while that line stays fixed until released
- **Click-through mode** — optionally pass clicks through the grid to apps beneath (panel stays interactive)

## Requirements

- Windows 10/11 for day-to-day use
- Node.js 20+ to run from source or build

## Run from source

```bash
cd align-overlay
npm install
npm start
```

## Build Windows installers

On a Windows machine:

```bash
cd align-overlay
npm install
npm run dist
```

Outputs land in `align-overlay/dist/`.

## Usage

1. Position AlignOverlay over the app you want to align.
2. Tune **Resolution**, **thickness**, and **colors** in the right panel.
3. **Measure** — switch to Measure mode (or press `2`), click point A then B.
4. **Sticky** — choose Vertical or Horizontal, switch to Sticky mode (or press `3`), click a gridline to pin it. Use **Release sticky** or `Esc` to clear.
5. Drag the top strip to move; drag frame edges/corners to resize.

### Shortcuts

| Key | Action |
| --- | --- |
| `1` | Pan mode |
| `2` | Measure mode |
| `3` | Sticky mode |
| `Esc` | Clear measure / release sticky |

## Metric conversion note

Pixel → metric uses the Windows logical mapping of **96 PPI** (adjusted by an optional override). This matches OS layout units; true physical size varies by monitor.

## Layout

```
align-overlay/
  src/main.js       Electron main process
  src/preload.js    Secure IPC bridge
  src/index.html    Overlay shell + control panel
  src/styles.css
  src/renderer.js   Grid, measure, sticky UI
  assets/           App icons
```
