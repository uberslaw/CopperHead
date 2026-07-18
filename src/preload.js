const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("copperhead", {
  minimize: () => ipcRenderer.invoke("window:minimize"),
  close: () => ipcRenderer.invoke("window:close"),
  getBounds: () => ipcRenderer.invoke("window:get-bounds"),
  setBounds: (bounds) => ipcRenderer.invoke("window:set-bounds", bounds),
  setAlwaysOnTop: (flag) => ipcRenderer.invoke("window:set-always-on-top", flag),
  setIgnoreMouseEvents: (ignore, options) =>
    ipcRenderer.invoke("window:set-ignore-mouse-events", ignore, options),
  getDpi: () => ipcRenderer.invoke("display:get-dpi"),
  setSticky: (guide) => ipcRenderer.invoke("sticky:set", guide),
  clearSticky: () => ipcRenderer.invoke("sticky:clear"),
  getSticky: () => ipcRenderer.invoke("sticky:get"),
});
