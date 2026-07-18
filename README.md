# GridFinder

Transparent Windows desktop overlay for checking alignment and spacing against other apps.

You see a window frame, a right-side control panel, and a grid. Everything under the grid stays visible.

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
npm install
npm start
```

## Build Windows installers

On a Windows machine:

```bash
npm install
npm run dist
```

Outputs land in `dist/`:

- NSIS installer
- Portable `.exe`

## Usage

1. Position GridFinder over the app you want to align.
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

Pixel → metric uses the Windows logical mapping of **96 PPI** (adjusted by an optional override). This matches OS layout units; true physical size varies by monitor. Use **PPI override** when you know your display’s real pixels-per-inch.

## Project layout

```
src/
  main.js       Electron main process (transparent window, sticky constraints)
  preload.js    Secure IPC bridge
  index.html    Overlay shell + control panel
  styles.css    Panel / frame styling
  renderer.js   Grid, measure, sticky UI logic
assets/         App icons
```
