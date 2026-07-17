'use strict';

/* Deep sanitizer for menu.json. Rebuilds the document keeping only
   known fields, capped lengths, and safe image references — whatever
   a client publishes, the repo only ever stores this shape. */

var THEMES = ['neon', 'smokehouse', 'chalkboard', 'bistro', 'midnight'];
var LAYOUTS = ['feature-list', 'cards', 'classic', 'photo-grid', 'showcase'];
var FONTS = ['Bebas Neue', 'Anton', 'Oswald', 'Archivo Black', 'Alfa Slab One',
  'Playfair Display', 'Cormorant Garamond', 'Permanent Marker', 'Caveat', 'Lobster',
  'Barlow', 'Inter', 'Montserrat', 'Source Sans 3', 'Lora'];

var MAX = {
  screens: 8, sections: 24, items: 80, prices: 4, deals: 6, badges: 4,
  short: 160, medium: 400, ticker: 600, id: 60
};

function str(v, cap) {
  if (typeof v !== 'string') return '';
  // strip control chars, keep newlines
  return v.replace(/[\x00-\x09\x0b-\x1f\x7f]/g, '').slice(0, cap);
}
function bool(v) { return v === true; }
function pick(v, list, dflt) { return list.indexOf(v) >= 0 ? v : dflt; }
function int(v, lo, hi, dflt) {
  var n = parseInt(v, 10);
  if (isNaN(n)) return dflt;
  return Math.min(Math.max(n, lo), hi);
}
function id(v, fallback) {
  var s = str(v, MAX.id).replace(/[^a-zA-Z0-9_-]/g, '');
  return s || fallback;
}

/* image refs: repo-relative within the client's images/ dir, or https URL */
function imageRef(v) {
  var s = str(v, 500).trim();
  if (!s) return '';
  if (/^https:\/\/[^\s'"<>]+$/i.test(s)) return s;
  s = s.replace(/^\/+/, '').replace(/\\/g, '/');
  if (s.indexOf('..') >= 0) return '';
  if (!/^images\//.test(s)) return '';
  if (!/^[a-zA-Z0-9 _\-./()+]+$/.test(s)) return '';
  return s;
}

function prices(v) {
  if (!Array.isArray(v)) return [];
  return v.slice(0, MAX.prices).map(function (p) {
    p = p || {};
    return { label: str(p.label, 12), value: str(p.value, 24) };
  }).filter(function (p) { return p.label || p.value; });
}

function item(v, idx) {
  v = v || {};
  return {
    id: id(v.id, 'i' + idx + '-' + Math.random().toString(36).slice(2, 8)),
    name: str(v.name, MAX.short),
    detail: str(v.detail, MAX.medium),
    prices: prices(v.prices),
    image: imageRef(v.image),
    bgImage: imageRef(v.bgImage),
    badge: str(v.badge, 40),
    featured: bool(v.featured),
    hidden: bool(v.hidden)
  };
}

function section(v, idx) {
  v = v || {};
  var out = {
    id: id(v.id, 's' + idx + '-' + Math.random().toString(36).slice(2, 8)),
    title: str(v.title, MAX.short),
    subtitle: str(v.subtitle, MAX.short),
    accent: int(v.accent, 1, 3, ((idx % 3) + 1)),
    featured: bool(v.featured),
    image: imageRef(v.image),
    note: str(v.note, MAX.medium),
    deals: Array.isArray(v.deals) ? v.deals.slice(0, MAX.deals).map(function (d) { return str(d, 60); }).filter(Boolean) : [],
    items: Array.isArray(v.items) ? v.items.slice(0, MAX.items).map(item) : []
  };
  if (v.role === 'featured') out.role = 'featured';
  return out;
}

function screenOptions(v) {
  v = v || {};
  var out = {};
  if (v.columns !== undefined) out.columns = int(v.columns, 1, 3, 2);
  if (v.panel !== undefined) out.panel = pick(v.panel, ['left', 'right', 'off'], 'right');
  if (v.thumbs !== undefined) out.thumbs = v.thumbs !== false;
  if (v.intervalSec !== undefined) out.intervalSec = int(v.intervalSec, 2, 120, 5);
  if (v.panelLabel !== undefined) out.panelLabel = str(v.panelLabel, 40);
  if (v.sidebar !== undefined) out.sidebar = pick(v.sidebar, ['brand', 'off'], 'off');
  if (v.slots !== undefined) out.slots = int(v.slots, 1, 3, 3);
  if (v.rotate !== undefined) out.rotate = v.rotate !== false;
  if (v.takeover && typeof v.takeover === 'object') {
    out.takeover = {
      enabled: bool(v.takeover.enabled),
      menuSec: int(v.takeover.menuSec, 6, 600, 20),
      promoSec: int(v.takeover.promoSec, 5, 120, 15)
    };
  }
  return out;
}

function screen(v, idx) {
  v = v || {};
  return {
    id: id(v.id, 'scr' + idx),
    title: str(v.title, MAX.short),
    subtitle: str(v.subtitle, MAX.short),
    layout: pick(v.layout, LAYOUTS, 'feature-list'),
    options: screenOptions(v.options),
    sections: Array.isArray(v.sections) ? v.sections.slice(0, MAX.sections).map(section) : []
  };
}

/* → sanitized menu or { error } */
function validateMenu(input, clientId) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return { error: 'Menu must be an object' };
  var raw = JSON.stringify(input);
  if (raw.length > 400 * 1024) return { error: 'Menu too large' };
  var fonts = input.fonts || {};
  var out = {
    schema: 1,
    version: int(input.version, 0, 1e9, 0),
    client: clientId,
    name: str(input.name, MAX.short) || clientId,
    tagline: str(input.tagline, MAX.short),
    logo: imageRef(input.logo),
    theme: pick(input.theme, THEMES, 'neon'),
    fonts: {
      display: pick(fonts.display, FONTS, ''),
      body: pick(fonts.body, FONTS, '')
    },
    ticker: str(input.ticker, MAX.ticker),
    brand: {},
    screens: Array.isArray(input.screens) ? input.screens.slice(0, MAX.screens).map(screen) : []
  };
  if (!out.fonts.display) delete out.fonts.display;
  if (!out.fonts.body) delete out.fonts.body;
  var b = input.brand || {};
  out.brand = {
    hours: str(b.hours, MAX.short),
    hoursLabel: str(b.hoursLabel, 60),
    status: str(b.status, MAX.short),
    social: str(b.social, MAX.short),
    wifi: str(b.wifi, MAX.short),
    badges: Array.isArray(b.badges) ? b.badges.slice(0, MAX.badges).map(function (x) { return str(x, 40); }).filter(Boolean) : []
  };
  if (!out.screens.length) return { error: 'Menu needs at least one screen' };
  return { menu: out };
}

module.exports = { validateMenu: validateMenu, THEMES: THEMES, LAYOUTS: LAYOUTS, FONTS: FONTS };
