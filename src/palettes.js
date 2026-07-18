/**
 * Built-in grid line palettes: 10 per style × 3 roles (grid, major, accent).
 * Styles: dark, light, neon, pastel.
 */

const dark = [
  { name: "Slate Ember", grid: "#6b7280", major: "#9ca3af", accent: "#f59e0b" },
  { name: "Midnight Copper", grid: "#78716c", major: "#a8a29e", accent: "#d9773a" },
  { name: "Graphite Teal", grid: "#57534e", major: "#a1a1aa", accent: "#2dd4bf" },
  { name: "Charcoal Blue", grid: "#64748b", major: "#94a3b8", accent: "#60a5fa" },
  { name: "Obsidian Moss", grid: "#52525b", major: "#a1a1aa", accent: "#84cc16" },
  { name: "Gunmetal Rose", grid: "#71717a", major: "#a1a1aa", accent: "#fb7185" },
  { name: "Iron Violet", grid: "#6b7280", major: "#9ca3af", accent: "#a78bfa" },
  { name: "Night Amber", grid: "#57534e", major: "#a8a29e", accent: "#fbbf24" },
  { name: "Carbon Ice", grid: "#4b5563", major: "#9ca3af", accent: "#67e8f9" },
  { name: "Shadow Coral", grid: "#6b7280", major: "#d4d4d8", accent: "#f97316" },
];

const light = [
  { name: "Paper Ink", grid: "#94a3b8", major: "#64748b", accent: "#0f172a" },
  { name: "Cloud Navy", grid: "#bfdbfe", major: "#60a5fa", accent: "#1e3a8a" },
  { name: "Parchment Sepia", grid: "#d6d3d1", major: "#a8a29e", accent: "#78350f" },
  { name: "Mist Sage", grid: "#bbf7d0", major: "#86efac", accent: "#14532d" },
  { name: "Porcelain Plum", grid: "#e9d5ff", major: "#c4b5fd", accent: "#581c87" },
  { name: "Snow Coral", grid: "#fecaca", major: "#fca5a5", accent: "#9f1239" },
  { name: "Ivory Teal", grid: "#99f6e4", major: "#5eead4", accent: "#115e59" },
  { name: "Linen Rust", grid: "#fed7aa", major: "#fdba74", accent: "#9a3412" },
  { name: "Chalk Sky", grid: "#bae6fd", major: "#7dd3fc", accent: "#0c4a6e" },
  { name: "Cream Graphite", grid: "#e7e5e4", major: "#a8a29e", accent: "#292524" },
];

const neon = [
  { name: "Cyber Cyan", grid: "#0891b2", major: "#22d3ee", accent: "#67e8f9" },
  { name: "Hot Magenta", grid: "#db2777", major: "#f472b6", accent: "#f0abfc" },
  { name: "Acid Lime", grid: "#65a30d", major: "#a3e635", accent: "#d9f99d" },
  { name: "Electric Blue", grid: "#2563eb", major: "#3b82f6", accent: "#93c5fd" },
  { name: "Plasma Orange", grid: "#ea580c", major: "#fb923c", accent: "#fdba74" },
  { name: "Volt Purple", grid: "#7c3aed", major: "#a78bfa", accent: "#e9d5ff" },
  { name: "Laser Red", grid: "#dc2626", major: "#f87171", accent: "#fecaca" },
  { name: "Grid Runner", grid: "#0ea5e9", major: "#22d3ee", accent: "#f0abfc" },
  { name: "Retro Arcade", grid: "#06b6d4", major: "#f472b6", accent: "#facc15" },
  { name: "Toxic Green", grid: "#16a34a", major: "#4ade80", accent: "#fef08a" },
];

const pastel = [
  { name: "Soft Peach", grid: "#fdba74", major: "#fed7aa", accent: "#ffedd5" },
  { name: "Lavender Mist", grid: "#c4b5fd", major: "#ddd6fe", accent: "#ede9fe" },
  { name: "Mint Cream", grid: "#86efac", major: "#bbf7d0", accent: "#dcfce7" },
  { name: "Baby Blue", grid: "#7dd3fc", major: "#bae6fd", accent: "#e0f2fe" },
  { name: "Blush Pink", grid: "#f9a8d4", major: "#fbcfe8", accent: "#fce7f3" },
  { name: "Butter Yellow", grid: "#fde047", major: "#fef08a", accent: "#fef9c3" },
  { name: "Seafoam", grid: "#5eead4", major: "#99f6e4", accent: "#ccfbf1" },
  { name: "Lilac Fog", grid: "#d8b4fe", major: "#e9d5ff", accent: "#f3e8ff" },
  { name: "Apricot", grid: "#fb923c", major: "#fdba74", accent: "#ffedd5" },
  { name: "Powder Rose", grid: "#fda4af", major: "#fecdd3", accent: "#fff1f2" },
];

const BUILTIN = { dark, light, neon, pastel };
const CATEGORIES = ["dark", "light", "neon", "pastel", "custom"];

function normalizeHex(value, fallback = "#d9773a") {
  const raw = String(value || "").trim();
  if (/^#[0-9a-fA-F]{6}$/.test(raw)) return raw.toLowerCase();
  if (/^#[0-9a-fA-F]{3}$/.test(raw)) {
    const [, a, b, c] = raw;
    return `#${a}${a}${b}${b}${c}${c}`.toLowerCase();
  }
  return fallback;
}

function normalizePalette(entry, index = 0) {
  return {
    id: entry?.id || `custom-${Date.now()}-${index}`,
    name: String(entry?.name || `Custom ${index + 1}`).slice(0, 40),
    grid: normalizeHex(entry?.grid, "#d9773a"),
    major: normalizeHex(entry?.major, "#f0c49a"),
    accent: normalizeHex(entry?.accent, "#ffe6c8"),
  };
}

function getBuiltinList(category) {
  return BUILTIN[category] || [];
}

function getList(category, customPalettes = []) {
  if (category === "custom") {
    return (customPalettes || []).map((p, i) => normalizePalette(p, i));
  }
  return getBuiltinList(category).map((p, i) => ({
    id: `${category}-${i}`,
    ...p,
  }));
}

function resolvePalette(category, index, customPalettes = []) {
  const list = getList(category, customPalettes);
  if (!list.length) return null;
  const i = ((Number(index) % list.length) + list.length) % list.length;
  return { palette: list[i], index: i, count: list.length };
}

module.exports = {
  BUILTIN,
  CATEGORIES,
  normalizeHex,
  normalizePalette,
  getBuiltinList,
  getList,
  resolvePalette,
};
