(() => {
  const api = window.alignOverlay;

  const canvas = document.getElementById("gridCanvas");
  const ctx = canvas.getContext("2d");
  const measureHud = document.getElementById("measureHud");
  const stickyHud = document.getElementById("stickyHud");
  const measureReadout = document.getElementById("measureReadout");
  const modeHelp = document.getElementById("modeHelp");
  const stickyStatus = document.getElementById("stickyStatus");
  const stickyDetail = document.getElementById("stickyDetail");
  const dpiNote = document.getElementById("dpiNote");

  const spacingSlider = document.getElementById("spacingSlider");
  const thicknessSlider = document.getElementById("thicknessSlider");
  const majorEverySlider = document.getElementById("majorEverySlider");
  const ppiSlider = document.getElementById("ppiSlider");
  const useAutoPpi = document.getElementById("useAutoPpi");
  const gridColorInput = document.getElementById("gridColor");
  const majorColorInput = document.getElementById("majorColor");
  const accentColorInput = document.getElementById("accentColor");
  const showOrigin = document.getElementById("showOrigin");
  const alwaysOnTop = document.getElementById("alwaysOnTop");
  const clickThrough = document.getElementById("clickThrough");

  const spacingValue = document.getElementById("spacingValue");
  const thicknessValue = document.getElementById("thicknessValue");
  const majorEveryValue = document.getElementById("majorEveryValue");
  const ppiValue = document.getElementById("ppiValue");

  const state = {
    mode: "pan", // pan | measure | sticky
    spacing: 20,
    thickness: 1,
    majorEvery: 5,
    gridColor: "#d9773a",
    majorColor: "#f0c49a",
    accentColor: "#ffe6c8",
    showOrigin: true,
    measurePoints: [],
    hoverPoint: null,
    stickyAxis: "x", // x = vertical line, y = horizontal line
    stickyPreviewOffset: null,
    stickyActive: null, // { axis, localOffset, screenPos }
    dpi: null,
    useAutoPpi: true,
    ppiOverride: 96,
    dpr: window.devicePixelRatio || 1,
  };

  const MODE_HELP = {
    pan: "Drag the top strip or resize from the frame edges. The grid stays see-through.",
    measure:
      "Click point A, then point B. A guide line shows pixel distance and approximate mm/cm.",
    sticky:
      "Pick Vertical or Horizontal, then click a gridline to pin it. The window keeps that line fixed on screen until released.",
  };

  function hexToRgba(hex, alpha) {
    const raw = hex.replace("#", "");
    const full =
      raw.length === 3
        ? raw
            .split("")
            .map((c) => c + c)
            .join("")
        : raw;
    const num = parseInt(full, 16);
    const r = (num >> 16) & 255;
    const g = (num >> 8) & 255;
    const b = num & 255;
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  function snapToGrid(value, spacing) {
    return Math.round(value / spacing) * spacing;
  }

  function effectivePpi() {
    if (state.useAutoPpi && state.dpi) {
      return state.dpi.logicalPpi || 96;
    }
    return state.ppiOverride || 96;
  }

  function pxToMetric(px) {
    const inches = px / effectivePpi();
    const mm = inches * 25.4;
    const cm = mm / 10;
    return { mm, cm, inches };
  }

  function formatDistance(px) {
    const metric = pxToMetric(px);
    const mmText =
      metric.mm >= 10
        ? `${metric.cm.toFixed(2)} cm (${metric.mm.toFixed(1)} mm)`
        : `${metric.mm.toFixed(2)} mm`;
    return {
      px: `${px.toFixed(1)} px`,
      metric: mmText,
      inches: `${metric.inches.toFixed(3)} in`,
    };
  }

  function resizeCanvas() {
    const rect = canvas.getBoundingClientRect();
    state.dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.floor(rect.width * state.dpr));
    canvas.height = Math.max(1, Math.floor(rect.height * state.dpr));
    ctx.setTransform(state.dpr, 0, 0, state.dpr, 0, 0);
    draw();
  }

  function drawGrid(width, height) {
    const { spacing, thickness, majorEvery, gridColor, majorColor } = state;

    ctx.save();
    ctx.clearRect(0, 0, width, height);

    // Subtle tint so the frame area is readable without hiding the desktop
    ctx.fillStyle = "rgba(20, 16, 12, 0.04)";
    ctx.fillRect(0, 0, width, height);

    const minorWidth = thickness;
    const majorWidth = Math.max(thickness * 1.6, thickness + 0.5);

    // Vertical lines
    for (let x = 0, i = 0; x <= width + 0.5; x += spacing, i += 1) {
      const isMajor = i % majorEvery === 0;
      ctx.beginPath();
      ctx.strokeStyle = isMajor
        ? hexToRgba(majorColor, 0.72)
        : hexToRgba(gridColor, 0.45);
      ctx.lineWidth = isMajor ? majorWidth : minorWidth;
      ctx.moveTo(Math.round(x) + 0.5, 0);
      ctx.lineTo(Math.round(x) + 0.5, height);
      ctx.stroke();
    }

    // Horizontal lines
    for (let y = 0, i = 0; y <= height + 0.5; y += spacing, i += 1) {
      const isMajor = i % majorEvery === 0;
      ctx.beginPath();
      ctx.strokeStyle = isMajor
        ? hexToRgba(majorColor, 0.72)
        : hexToRgba(gridColor, 0.45);
      ctx.lineWidth = isMajor ? majorWidth : minorWidth;
      ctx.moveTo(0, Math.round(y) + 0.5);
      ctx.lineTo(width, Math.round(y) + 0.5);
      ctx.stroke();
    }

    if (state.showOrigin) {
      const cx = Math.round(width / 2) + 0.5;
      const cy = Math.round(height / 2) + 0.5;
      ctx.beginPath();
      ctx.strokeStyle = hexToRgba(state.accentColor, 0.9);
      ctx.lineWidth = Math.max(1.5, thickness + 0.5);
      ctx.moveTo(cx, 0);
      ctx.lineTo(cx, height);
      ctx.moveTo(0, cy);
      ctx.lineTo(width, cy);
      ctx.stroke();
    }

    ctx.restore();
  }

  function drawMeasure(width, height) {
    const points = state.measurePoints;
    if (!points.length && !state.hoverPoint) return;

    ctx.save();
    ctx.strokeStyle = hexToRgba(state.accentColor, 0.95);
    ctx.fillStyle = hexToRgba(state.accentColor, 0.95);
    ctx.lineWidth = Math.max(1.5, state.thickness);

    const drawPoint = (p, label) => {
      ctx.beginPath();
      ctx.arc(p.x, p.y, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.font = "11px Segoe UI, sans-serif";
      ctx.fillText(label, p.x + 8, p.y - 8);
    };

    if (points[0]) drawPoint(points[0], "A");
    if (points[1]) drawPoint(points[1], "B");

    const end =
      points.length === 1 && state.hoverPoint
        ? state.hoverPoint
        : points[1] || null;

    if (points[0] && end) {
      ctx.beginPath();
      ctx.setLineDash([5, 4]);
      ctx.moveTo(points[0].x, points[0].y);
      ctx.lineTo(end.x, end.y);
      ctx.stroke();
      ctx.setLineDash([]);

      // Axis helpers
      ctx.strokeStyle = hexToRgba(state.majorColor, 0.55);
      ctx.beginPath();
      ctx.moveTo(points[0].x, points[0].y);
      ctx.lineTo(end.x, points[0].y);
      ctx.lineTo(end.x, end.y);
      ctx.stroke();

      const dx = end.x - points[0].x;
      const dy = end.y - points[0].y;
      const dist = Math.hypot(dx, dy);
      const midX = (points[0].x + end.x) / 2;
      const midY = (points[0].y + end.y) / 2;
      const formatted = formatDistance(dist);

      measureHud.textContent = `${formatted.px} · ${formatted.metric}`;
      measureHud.classList.remove("hidden");
      measureHud.style.left = `${Math.min(width - 180, Math.max(8, midX))}px`;
      measureHud.style.top = `${Math.min(height - 40, Math.max(8, midY))}px`;
    } else {
      measureHud.classList.add("hidden");
    }

    ctx.restore();
  }

  function drawStickyPreview(width, height) {
    const offset =
      state.stickyActive?.localOffset ?? state.stickyPreviewOffset;
    if (offset == null) {
      stickyHud.classList.add("hidden");
      return;
    }

    const axis = state.stickyActive?.axis ?? state.stickyAxis;
    ctx.save();
    ctx.strokeStyle = state.stickyActive
      ? hexToRgba("#8fad6e", 0.95)
      : hexToRgba(state.accentColor, 0.85);
    ctx.lineWidth = Math.max(2, state.thickness + 1);
    ctx.setLineDash(state.stickyActive ? [] : [6, 4]);
    ctx.beginPath();
    if (axis === "x") {
      const x = Math.round(offset) + 0.5;
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      stickyHud.textContent = state.stickyActive
        ? `Sticky V @ ${Math.round(offset)} px`
        : `Pin vertical @ ${Math.round(offset)} px`;
      stickyHud.style.left = `${Math.min(width - 160, Math.max(8, offset + 8))}px`;
      stickyHud.style.top = "36px";
    } else {
      const y = Math.round(offset) + 0.5;
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      stickyHud.textContent = state.stickyActive
        ? `Sticky H @ ${Math.round(offset)} px`
        : `Pin horizontal @ ${Math.round(offset)} px`;
      stickyHud.style.left = "12px";
      stickyHud.style.top = `${Math.min(height - 40, Math.max(36, offset + 8))}px`;
    }
    ctx.stroke();
    ctx.restore();
    stickyHud.classList.remove("hidden");
  }

  function draw() {
    const rect = canvas.getBoundingClientRect();
    const width = rect.width;
    const height = rect.height;
    drawGrid(width, height);
    if (state.mode === "measure" || state.measurePoints.length) {
      drawMeasure(width, height);
    } else {
      measureHud.classList.add("hidden");
    }
    if (state.mode === "sticky" || state.stickyActive) {
      drawStickyPreview(width, height);
    } else if (!state.stickyActive) {
      stickyHud.classList.add("hidden");
    }
  }

  function canvasPointFromEvent(event) {
    const rect = canvas.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top,
    };
  }

  function updateMeasureReadout() {
    const points = state.measurePoints;
    const rows = measureReadout.querySelectorAll("strong");
    if (points.length < 2) {
      rows[0].textContent = points.length === 1 ? "Pick point B" : "—";
      rows[1].textContent = "—";
      rows[2].textContent = "—";
      return;
    }
    const dx = points[1].x - points[0].x;
    const dy = points[1].y - points[0].y;
    const dist = Math.hypot(dx, dy);
    const formatted = formatDistance(dist);
    rows[0].textContent = formatted.px;
    rows[1].textContent = `${dx.toFixed(1)} / ${dy.toFixed(1)}`;
    rows[2].textContent = `${formatted.metric} · ${formatted.inches}`;
  }

  function updateStickyReadout() {
    if (!state.stickyActive) {
      stickyStatus.textContent = "Off";
      stickyDetail.textContent = "—";
      return;
    }
    const axisLabel = state.stickyActive.axis === "x" ? "Vertical" : "Horizontal";
    stickyStatus.textContent = "Pinned";
    stickyDetail.textContent = `${axisLabel} · local ${Math.round(
      state.stickyActive.localOffset
    )} px · screen ${Math.round(state.stickyActive.screenPos)} px`;
  }

  function setMode(mode) {
    state.mode = mode;
    document.body.classList.remove("mode-pan", "mode-measure", "mode-sticky");
    document.body.classList.add(`mode-${mode}`);
    document.querySelectorAll(".mode-btn[data-mode]").forEach((btn) => {
      btn.classList.toggle("active", btn.dataset.mode === mode);
    });
    modeHelp.textContent = MODE_HELP[mode];
    if (mode !== "measure") {
      state.hoverPoint = null;
    }
    if (mode !== "sticky") {
      state.stickyPreviewOffset = null;
    }
    draw();
  }

  async function refreshDpi() {
    if (!api?.getDpi) return;
    state.dpi = await api.getDpi();
    const scale = state.dpi?.scaleFactor ?? 1;
    const logical = state.dpi?.logicalPpi ?? 96;
    dpiNote.textContent = `DPI: ${logical} logical PPI · scale ${scale.toFixed(
      2
    )} · ~${(logical * scale).toFixed(0)} physical PPI`;
    if (state.useAutoPpi) {
      ppiValue.textContent = `auto (${logical})`;
      ppiSlider.value = String(logical);
    }
    updateMeasureReadout();
    draw();
  }

  async function applySticky(localOffset) {
    if (!api?.setSticky) return;
    const guide = await api.setSticky({
      axis: state.stickyAxis,
      localOffset,
    });
    state.stickyActive = guide;
    state.stickyPreviewOffset = localOffset;
    updateStickyReadout();
    draw();
  }

  async function clearSticky() {
    if (api?.clearSticky) await api.clearSticky();
    state.stickyActive = null;
    state.stickyPreviewOffset = null;
    updateStickyReadout();
    draw();
  }

  function clearMeasure() {
    state.measurePoints = [];
    state.hoverPoint = null;
    updateMeasureReadout();
    draw();
  }

  // Controls
  spacingSlider.addEventListener("input", () => {
    state.spacing = Number(spacingSlider.value);
    spacingValue.textContent = `${state.spacing} px`;
    draw();
  });

  thicknessSlider.addEventListener("input", () => {
    state.thickness = Number(thicknessSlider.value);
    thicknessValue.textContent = `${state.thickness.toFixed(1)} px`;
    draw();
  });

  majorEverySlider.addEventListener("input", () => {
    state.majorEvery = Number(majorEverySlider.value);
    majorEveryValue.textContent = `${state.majorEvery} lines`;
    draw();
  });

  gridColorInput.addEventListener("input", () => {
    state.gridColor = gridColorInput.value;
    draw();
  });

  majorColorInput.addEventListener("input", () => {
    state.majorColor = majorColorInput.value;
    draw();
  });

  accentColorInput.addEventListener("input", () => {
    state.accentColor = accentColorInput.value;
    draw();
  });

  showOrigin.addEventListener("change", () => {
    state.showOrigin = showOrigin.checked;
    draw();
  });

  ppiSlider.addEventListener("input", () => {
    state.ppiOverride = Number(ppiSlider.value);
    if (!state.useAutoPpi) {
      ppiValue.textContent = `${state.ppiOverride} PPI`;
    }
    updateMeasureReadout();
    draw();
  });

  useAutoPpi.addEventListener("change", () => {
    state.useAutoPpi = useAutoPpi.checked;
    ppiSlider.disabled = state.useAutoPpi;
    if (state.useAutoPpi) {
      const logical = state.dpi?.logicalPpi ?? 96;
      ppiValue.textContent = `auto (${logical})`;
    } else {
      ppiValue.textContent = `${state.ppiOverride} PPI`;
    }
    updateMeasureReadout();
    draw();
  });

  alwaysOnTop.addEventListener("change", () => {
    api?.setAlwaysOnTop?.(alwaysOnTop.checked);
  });

  clickThrough.addEventListener("change", async () => {
    document.body.classList.toggle("click-through-armed", clickThrough.checked);
    // When enabled, ignore mouse on the transparent window except the panel.
    // Electron forward mode lets us re-enable over the panel via mousemove.
    if (!api?.setIgnoreMouseEvents) return;
    if (!clickThrough.checked) {
      await api.setIgnoreMouseEvents(false);
      return;
    }
    await api.setIgnoreMouseEvents(true, { forward: true });
  });

  document.addEventListener("mousemove", async (event) => {
    if (!clickThrough.checked || !api?.setIgnoreMouseEvents) return;
    const panel = document.getElementById("controlPanel");
    const overPanel = panel.contains(event.target);
    await api.setIgnoreMouseEvents(!overPanel, { forward: true });
  });

  document.querySelectorAll(".mode-btn[data-mode]").forEach((btn) => {
    btn.addEventListener("click", () => setMode(btn.dataset.mode));
  });

  document.getElementById("stickyAxisX").addEventListener("click", () => {
    state.stickyAxis = "x";
    document.getElementById("stickyAxisX").classList.add("active");
    document.getElementById("stickyAxisY").classList.remove("active");
    if (state.mode !== "sticky") setMode("sticky");
    draw();
  });

  document.getElementById("stickyAxisY").addEventListener("click", () => {
    state.stickyAxis = "y";
    document.getElementById("stickyAxisY").classList.add("active");
    document.getElementById("stickyAxisX").classList.remove("active");
    if (state.mode !== "sticky") setMode("sticky");
    draw();
  });

  document.getElementById("btnClearMeasure").addEventListener("click", clearMeasure);
  document.getElementById("btnClearSticky").addEventListener("click", clearSticky);
  document.getElementById("btnMinimize").addEventListener("click", () => api?.minimize?.());
  document.getElementById("btnClose").addEventListener("click", () => api?.close?.());

  canvas.addEventListener("mousemove", (event) => {
    const p = canvasPointFromEvent(event);

    if (state.mode === "measure" && state.measurePoints.length === 1) {
      state.hoverPoint = p;
      draw();
      return;
    }

    if (state.mode === "sticky" && !state.stickyActive) {
      state.stickyPreviewOffset =
        state.stickyAxis === "x"
          ? snapToGrid(p.x, state.spacing)
          : snapToGrid(p.y, state.spacing);
      draw();
    }
  });

  canvas.addEventListener("mouseleave", () => {
    state.hoverPoint = null;
    if (!state.stickyActive) state.stickyPreviewOffset = null;
    draw();
  });

  canvas.addEventListener("click", async (event) => {
    const p = canvasPointFromEvent(event);

    if (state.mode === "measure") {
      if (state.measurePoints.length >= 2) {
        state.measurePoints = [];
      }
      state.measurePoints.push({ x: p.x, y: p.y });
      if (state.measurePoints.length === 2) {
        state.hoverPoint = null;
      }
      updateMeasureReadout();
      draw();
      return;
    }

    if (state.mode === "sticky") {
      const offset =
        state.stickyAxis === "x"
          ? snapToGrid(p.x, state.spacing)
          : snapToGrid(p.y, state.spacing);
      await applySticky(offset);
    }
  });

  // Custom resize via handles (frameless window)
  (() => {
    const handles = document.querySelectorAll(".resize-handles .handle");
    let resizing = null;

    handles.forEach((handle) => {
      handle.addEventListener("mousedown", (event) => {
        event.preventDefault();
        event.stopPropagation();
        resizing = {
          edge: handle.dataset.edge,
          startX: event.screenX,
          startY: event.screenY,
        };
        api?.getBounds?.().then((bounds) => {
          if (!bounds) return;
          resizing.startBounds = { ...bounds };
        });
      });
    });

    window.addEventListener("mousemove", async (event) => {
      if (!resizing?.startBounds) return;
      const dx = event.screenX - resizing.startX;
      const dy = event.screenY - resizing.startY;
      const b = { ...resizing.startBounds };
      const edge = resizing.edge;

      if (edge.includes("e")) b.width = resizing.startBounds.width + dx;
      if (edge.includes("s")) b.height = resizing.startBounds.height + dy;
      if (edge.includes("w")) {
        b.x = resizing.startBounds.x + dx;
        b.width = resizing.startBounds.width - dx;
      }
      if (edge.includes("n")) {
        b.y = resizing.startBounds.y + dy;
        b.height = resizing.startBounds.height - dy;
      }

      const result = await api?.setBounds?.(b);
      if (result?.sticky) {
        state.stickyActive = result.sticky;
        updateStickyReadout();
        draw();
      }
    });

    window.addEventListener("mouseup", () => {
      resizing = null;
    });
  })();

  window.addEventListener("resize", resizeCanvas);

  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      if (state.mode === "measure") clearMeasure();
      if (state.mode === "sticky" && state.stickyActive) clearSticky();
    }
    if (event.key === "1") setMode("pan");
    if (event.key === "2") setMode("measure");
    if (event.key === "3") setMode("sticky");
  });

  // Init
  spacingValue.textContent = `${state.spacing} px`;
  thicknessValue.textContent = `${state.thickness.toFixed(1)} px`;
  majorEveryValue.textContent = `${state.majorEvery} lines`;
  ppiSlider.disabled = true;
  setMode("pan");
  updateMeasureReadout();
  updateStickyReadout();
  resizeCanvas();
  refreshDpi();

  // Keep sticky readout in sync if window is moved externally
  setInterval(async () => {
    if (!api?.getSticky) return;
    const guide = await api.getSticky();
    const prev = state.stickyActive;
    const changed =
      (!!guide !== !!prev) ||
      (guide &&
        prev &&
        (guide.axis !== prev.axis ||
          guide.localOffset !== prev.localOffset ||
          guide.screenPos !== prev.screenPos));
    if (changed) {
      state.stickyActive = guide;
      updateStickyReadout();
      draw();
    }
  }, 500);
})();
