const fs = require("fs");
const path = require("path");

function prefsPath(userData) {
  return path.join(userData, "gridfinder-prefs.json");
}

function loadPrefs(userData) {
  try {
    const raw = fs.readFileSync(prefsPath(userData), "utf8");
    const data = JSON.parse(raw);
    return data && typeof data === "object" ? data : {};
  } catch (_) {
    return {};
  }
}

function savePrefs(userData, prefs) {
  try {
    fs.mkdirSync(userData, { recursive: true });
    fs.writeFileSync(
      prefsPath(userData),
      JSON.stringify(prefs, null, 2),
      "utf8"
    );
    return true;
  } catch (_) {
    return false;
  }
}

module.exports = { loadPrefs, savePrefs, prefsPath };
