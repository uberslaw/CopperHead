const { app, BrowserWindow, ipcMain, screen } = require("electron");
const path = require("path");

/** @type {BrowserWindow | null} */
let mainWindow = null;

/**
 * Sticky guide pins one axis to a fixed screen coordinate.
 * localOffset is distance from the window's left (x) or top (y) to that line.
 * @type {{ axis: 'x' | 'y', screenPos: number, localOffset: number } | null}
 */
let stickyGuide = null;

const MIN_WIDTH = 420;
const MIN_HEIGHT = 280;

function createWindow() {
  const display = screen.getPrimaryDisplay();
  const { width: sw, height: sh } = display.workAreaSize;
  const width = Math.min(1100, sw - 80);
  const height = Math.min(700, sh - 80);

  mainWindow = new BrowserWindow({
    width,
    height,
    minWidth: MIN_WIDTH,
    minHeight: MIN_HEIGHT,
    x: Math.round((sw - width) / 2),
    y: Math.round((sh - height) / 2),
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    hasShadow: false,
    resizable: true,
    maximizable: false,
    fullscreenable: false,
    backgroundColor: "#00000000",
    title: "GridFinder",
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  mainWindow.setAlwaysOnTop(true, "screen-saver");
  mainWindow.loadFile(path.join(__dirname, "index.html"));

  mainWindow.on("will-move", (event, newBounds) => {
    if (!stickyGuide || !mainWindow) return;
    const constrained = constrainMove(newBounds);
    if (boundsChanged(constrained, newBounds)) {
      event.preventDefault();
      mainWindow.setBounds(constrained, false);
    }
  });

  mainWindow.on("will-resize", (event, newBounds, details) => {
    if (!stickyGuide || !mainWindow) return;
    const constrained = constrainResize(newBounds, details?.edge);
    if (boundsChanged(constrained, newBounds)) {
      event.preventDefault();
      mainWindow.setBounds(constrained, false);
    }
  });

  mainWindow.on("closed", () => {
    mainWindow = null;
    stickyGuide = null;
  });
}

function boundsChanged(a, b) {
  return (
    a.x !== b.x || a.y !== b.y || a.width !== b.width || a.height !== b.height
  );
}

function constrainMove(bounds) {
  if (!stickyGuide) return bounds;
  const next = { ...bounds };

  if (stickyGuide.axis === "x") {
    // Keep the pinned vertical line at screenPos; block free horizontal moves.
    next.x = Math.round(stickyGuide.screenPos - stickyGuide.localOffset);
  } else {
    next.y = Math.round(stickyGuide.screenPos - stickyGuide.localOffset);
  }
  return next;
}

function constrainResize(bounds, edge = "") {
  if (!stickyGuide) return bounds;

  const next = {
    x: bounds.x,
    y: bounds.y,
    width: Math.max(MIN_WIDTH, bounds.width),
    height: Math.max(MIN_HEIGHT, bounds.height),
  };
  const edgeLower = String(edge || "").toLowerCase();
  const screenPos = stickyGuide.screenPos;

  if (stickyGuide.axis === "x") {
    const resizingLeft =
      edgeLower.includes("left") ||
      edgeLower.includes("west") ||
      edgeLower.includes("w");
    const resizingRight =
      edgeLower.includes("right") ||
      edgeLower.includes("east") ||
      edgeLower.includes("e");

    if (resizingLeft && !resizingRight) {
      // Expand/contract from the left; keep sticky screen X, update offset.
      const right = bounds.x + bounds.width;
      let left = bounds.x;
      left = Math.min(left, screenPos - 1);
      left = Math.max(left, screenPos - (right - MIN_WIDTH));
      next.x = Math.round(left);
      next.width = Math.max(MIN_WIDTH, right - next.x);
      stickyGuide.localOffset = screenPos - next.x;
    } else {
      // Right (or unspecified): keep left edge so sticky screen X holds.
      next.x = Math.round(screenPos - stickyGuide.localOffset);
      const minWidth = Math.ceil(stickyGuide.localOffset + 1);
      next.width = Math.max(MIN_WIDTH, minWidth, next.width);
      if (next.x + next.width <= screenPos) {
        next.width = screenPos - next.x + 1;
      }
    }
  } else {
    const resizingTop =
      edgeLower.includes("top") ||
      edgeLower.includes("north") ||
      edgeLower.includes("n");
    const resizingBottom =
      edgeLower.includes("bottom") ||
      edgeLower.includes("south") ||
      edgeLower.includes("s");

    if (resizingTop && !resizingBottom) {
      const bottom = bounds.y + bounds.height;
      let top = bounds.y;
      top = Math.min(top, screenPos - 1);
      top = Math.max(top, screenPos - (bottom - MIN_HEIGHT));
      next.y = Math.round(top);
      next.height = Math.max(MIN_HEIGHT, bottom - next.y);
      stickyGuide.localOffset = screenPos - next.y;
    } else {
      next.y = Math.round(screenPos - stickyGuide.localOffset);
      const minHeight = Math.ceil(stickyGuide.localOffset + 1);
      next.height = Math.max(MIN_HEIGHT, minHeight, next.height);
      if (next.y + next.height <= screenPos) {
        next.height = screenPos - next.y + 1;
      }
    }
  }

  return next;
}

function applyStickyToBounds(bounds) {
  if (!stickyGuide) return bounds;
  // Re-apply after programmatic setBounds from renderer resize handles.
  const edgeGuess = guessResizeEdge(mainWindow?.getBounds(), bounds);
  return constrainResize(constrainMove(bounds), edgeGuess);
}

function guessResizeEdge(prev, next) {
  if (!prev || !next) return "";
  let edge = "";
  if (next.x !== prev.x) edge += "left";
  if (next.y !== prev.y) edge += "top";
  if (next.x + next.width !== prev.x + prev.width) edge += "right";
  if (next.y + next.height !== prev.y + prev.height) edge += "bottom";
  return edge;
}

function getDpiInfo() {
  const display = screen.getDisplayMatching(
    mainWindow ? mainWindow.getBounds() : screen.getPrimaryDisplay().bounds
  );
  const scaleFactor = display.scaleFactor || 1;
  const logicalPpi = 96;
  return {
    scaleFactor,
    logicalPpi,
    physicalPpi: logicalPpi * scaleFactor,
    size: display.size,
    bounds: display.bounds,
  };
}

ipcMain.handle("window:minimize", () => {
  mainWindow?.minimize();
});

ipcMain.handle("window:close", () => {
  mainWindow?.close();
});

ipcMain.handle("window:get-bounds", () => mainWindow?.getBounds() ?? null);

ipcMain.handle("window:set-bounds", (_event, bounds) => {
  if (!mainWindow || !bounds) return null;
  const current = mainWindow.getBounds();
  const next = {
    x: bounds.x ?? current.x,
    y: bounds.y ?? current.y,
    width: Math.max(MIN_WIDTH, bounds.width ?? current.width),
    height: Math.max(MIN_HEIGHT, bounds.height ?? current.height),
  };
  const constrained = applyStickyToBounds(next);
  mainWindow.setBounds(constrained, false);
  return {
    bounds: mainWindow.getBounds(),
    sticky: stickyGuide ? { ...stickyGuide } : null,
  };
});

ipcMain.handle("window:set-always-on-top", (_event, flag) => {
  if (!mainWindow) return;
  mainWindow.setAlwaysOnTop(Boolean(flag), "screen-saver");
});

ipcMain.handle("window:set-ignore-mouse-events", (_event, ignore, options) => {
  if (!mainWindow) return;
  mainWindow.setIgnoreMouseEvents(Boolean(ignore), options || { forward: true });
});

ipcMain.handle("display:get-dpi", () => getDpiInfo());

ipcMain.handle("sticky:set", (_event, guide) => {
  if (!guide || !mainWindow) {
    stickyGuide = null;
    return null;
  }

  const bounds = mainWindow.getBounds();
  const axis = guide.axis === "y" ? "y" : "x";
  const localOffset = Number(guide.localOffset);

  if (!Number.isFinite(localOffset)) {
    stickyGuide = null;
    return null;
  }

  stickyGuide = {
    axis,
    localOffset,
    screenPos:
      axis === "x" ? bounds.x + localOffset : bounds.y + localOffset,
  };

  return { ...stickyGuide };
});

ipcMain.handle("sticky:clear", () => {
  stickyGuide = null;
  return null;
});

ipcMain.handle("sticky:get", () => (stickyGuide ? { ...stickyGuide } : null));

app.whenReady().then(() => {
  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
