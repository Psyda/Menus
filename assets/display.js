/* ═══════════════════════════════════════════════════════════════════
   MENU DISPLAY ENGINE
   Reads <client>/menu.json and renders one screen of it using the
   theme + layout preset chosen in the data. Polls for version changes
   and reloads so published edits appear on the TV automatically.

   Page shells set:
     window.MENU_CLIENT = 'pinecone';
     window.MENU_SCREEN = 1;          // 1-based
     window.MENU_BASE   = '..';       // path from the shell to repo root

   Preview mode (?preview=1): no polling; renders drafts received via
   postMessage({ type:'menus:preview', menu:{...}, screen:1 }).
   ═══════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';

  var CLIENT = window.MENU_CLIENT;
  var SCREEN = window.MENU_SCREEN || 1;
  var BASE = (window.MENU_BASE || '..').replace(/\/+$/, '');
  var PREVIEW = /[?&]preview=1/.test(window.location.search);
  var POLL_MS = 60000;

  var THEMES = ['neon', 'smokehouse', 'chalkboard', 'bistro', 'midnight'];

  var FONT_URLS = {
    'Bebas Neue': 'Bebas+Neue',
    'Anton': 'Anton',
    'Oswald': 'Oswald:wght@400;500;600;700',
    'Archivo Black': 'Archivo+Black',
    'Alfa Slab One': 'Alfa+Slab+One',
    'Playfair Display': 'Playfair+Display:wght@500;600;700',
    'Cormorant Garamond': 'Cormorant+Garamond:wght@500;600;700',
    'Permanent Marker': 'Permanent+Marker',
    'Caveat': 'Caveat:wght@500;700',
    'Lobster': 'Lobster',
    'Barlow': 'Barlow:wght@300;400;500;600;700',
    'Inter': 'Inter:wght@300;400;500;600;700',
    'Montserrat': 'Montserrat:wght@300;400;500;600;700',
    'Source Sans 3': 'Source+Sans+3:wght@300;400;600;700',
    'Lora': 'Lora:wght@400;500;600;700',
    'Barlow Condensed': 'Barlow+Condensed:wght@400;600;700;800'
  };
  var THEME_FONTS = {
    neon: { display: 'Bebas Neue', body: 'Barlow' },
    smokehouse: { display: 'Bebas Neue', body: 'Barlow' },
    chalkboard: { display: 'Permanent Marker', body: 'Barlow' },
    bistro: { display: 'Playfair Display', body: 'Barlow' },
    midnight: { display: 'Anton', body: 'Inter' }
  };

  var timers = [];
  var currentVersion = null;

  /* ── helpers ─────────────────────────────────────────────────── */

  function h(tag, cls, text) {
    var el = document.createElement(tag);
    if (cls) el.className = cls;
    if (text != null && text !== '') setML(el, String(text));
    return el;
  }
  // multi-line safe text (supports \n), no HTML injection possible
  function setML(el, text) {
    var lines = String(text).split('\n');
    for (var i = 0; i < lines.length; i++) {
      if (i > 0) el.appendChild(document.createElement('br'));
      el.appendChild(document.createTextNode(lines[i]));
    }
  }
  function imgURL(path) {
    if (!path) return '';
    if (/^(https?:)?\/\//.test(path) || /^data:|^blob:/.test(path)) return path;
    return BASE + '/' + CLIENT + '/' + path.replace(/^\/+/, '');
  }
  function mkImg(path, alt) {
    var img = document.createElement('img');
    img.alt = alt || '';
    img.src = imgURL(path);
    img.onerror = function () { img.style.display = 'none'; };
    return img;
  }
  function setAccent(el, n) {
    var slot = (n >= 1 && n <= 3) ? n : 1;
    el.style.setProperty('--ac', 'var(--a' + slot + ')');
    el.style.setProperty('--ac-rgb', 'var(--a' + slot + '-rgb)');
  }
  function every(ms, fn) { timers.push(setInterval(fn, ms)); }
  function clearTimers() {
    while (timers.length) clearInterval(timers.pop());
  }
  function visibleItems(sec) {
    return (sec.items || []).filter(function (it) { return !it.hidden; });
  }
  function listSections(screen) {
    return (screen.sections || []).filter(function (s) { return (s.role || 'list') !== 'featured'; });
  }
  // Pool of items for featured panels / showcases / takeovers:
  // items in "featured"-role sections + items flagged featured; fallback = items with images
  function featuredPool(screen) {
    var pool = [], seen = {};
    (screen.sections || []).forEach(function (sec, si) {
      visibleItems(sec).forEach(function (it) {
        var isFeat = (sec.role === 'featured') || it.featured;
        if (isFeat && !seen[it.id || it.name]) {
          seen[it.id || it.name] = 1;
          pool.push({ item: it, accent: sec.accent || ((si % 3) + 1) });
        }
      });
    });
    if (!pool.length) {
      (screen.sections || []).forEach(function (sec, si) {
        visibleItems(sec).forEach(function (it) {
          if (it.image && pool.length < 8) pool.push({ item: it, accent: sec.accent || ((si % 3) + 1) });
        });
      });
    }
    return pool;
  }
  function labelText(l) { return /^\d+\+?$/.test(l) ? l + '/' : l + ' '; }
  function pricesEl(prices, cls, lineCls) {
    var wrap = h('div', cls + ' n' + Math.min((prices || []).length || 1, 3));
    (prices || []).forEach(function (p) {
      if (!p || (!p.value && !p.label)) return;
      var line = h('div', lineCls || 'fl-price-line');
      if (p.label) line.appendChild(h('span', 'fl-price-label', labelText(p.label)));
      line.appendChild(document.createTextNode(p.value || ''));
      wrap.appendChild(line);
    });
    return wrap;
  }
  function inlinePricesEl(prices, cls) {
    var wrap = h('span', cls);
    (prices || []).forEach(function (p, i) {
      if (!p || (!p.value && !p.label)) return;
      if (i > 0) wrap.appendChild(document.createTextNode('  '));
      if (p.label) wrap.appendChild(h('span', 'plabel', labelText(p.label)));
      wrap.appendChild(document.createTextNode(p.value || ''));
    });
    return wrap;
  }
  function firstPrice(prices) {
    return (prices && prices.length) ? [prices[0]] : [];
  }

  /* ── auto-fit ────────────────────────────────────────────────────
     Containers marked data-fit have em-based text on a vh base font.
     Shrink the base until the marked content fits (signage never
     scrolls, so overflowing text must scale down instead). */
  function stackH(el) {
    var total = 0, ch = el.children;
    for (var i = 0; i < ch.length; i++) {
      var cs = getComputedStyle(ch[i]);
      if (cs.position === 'absolute' || cs.position === 'fixed') continue;
      total += ch[i].offsetHeight + parseFloat(cs.marginTop || 0) + parseFloat(cs.marginBottom || 0);
    }
    return total;
  }
  function fitAll() {
    var containers = document.querySelectorAll('[data-fit]');
    for (var c = 0; c < containers.length; c++) {
      var el = containers[c];
      el.style.fontSize = '';
      var mode = el.getAttribute('data-fit') || 'scroll';
      var watchSel = el.getAttribute('data-fit-watch');
      var targets = watchSel ? el.querySelectorAll(watchSel) : [el];
      if (!targets.length) targets = [el];
      var overflowing = function () {
        for (var t = 0; t < targets.length; t++) {
          var tg = targets[t];
          if (mode === 'rows') {
            var rh = tg.clientHeight + 2, ch = tg.children;
            for (var k = 0; k < ch.length; k++) if (ch[k].offsetHeight > rh) return true;
          } else if (mode === 'stack') {
            if (stackH(tg) > tg.clientHeight + 2) return true;
          } else {
            if (tg.scrollHeight > tg.clientHeight + 2) return true;
          }
        }
        return false;
      };
      var scale = 1;
      while (overflowing() && scale > 0.55) {
        scale -= 0.05;
        el.style.fontSize = (2 * scale).toFixed(3) + 'vh';
      }
    }
  }
  var fitQueued = false;
  function queueFit() {
    if (fitQueued) return;
    fitQueued = true;
    requestAnimationFrame(function () { fitQueued = false; fitAll(); });
  }

  /* ── fonts + theme ───────────────────────────────────────────── */

  function applyTheme(menu) {
    var theme = THEMES.indexOf(menu.theme) >= 0 ? menu.theme : 'neon';
    document.body.className = 'theme-' + theme;
    var defaults = THEME_FONTS[theme];
    var display = (menu.fonts && menu.fonts.display) || defaults.display;
    var body = (menu.fonts && menu.fonts.body) || defaults.body;
    if (!FONT_URLS[display]) display = defaults.display;
    if (!FONT_URLS[body]) body = defaults.body;
    document.body.style.setProperty('--f-display', "'" + display + "'");
    document.body.style.setProperty('--f-body', "'" + body + "'");
    loadFonts([display, body, 'Barlow Condensed', defaults.display, defaults.body]);
  }
  var loadedFonts = {};
  function loadFonts(families) {
    var need = [];
    families.forEach(function (f) {
      if (f && FONT_URLS[f] && !loadedFonts[f]) { loadedFonts[f] = 1; need.push(FONT_URLS[f]); }
    });
    if (!need.length) return;
    var link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=' + need.join('&family=') + '&display=swap';
    document.head.appendChild(link);
  }

  /* ── chrome (header / marquee / pips) ────────────────────────── */

  function buildHeader(menu, screen) {
    var hdr = h('header', 'hdr');
    var left = h('div', 'hdr-left');
    if (menu.logo) left.appendChild((function () { var i = mkImg(menu.logo, menu.name); i.className = 'hdr-logo'; return i; })());
    left.appendChild(h('div', 'hdr-vr'));
    var twrap = h('div');
    twrap.appendChild(h('div', 'hdr-title', screen.title || menu.name));
    if (screen.subtitle) twrap.appendChild(h('div', 'hdr-sub', screen.subtitle));
    left.appendChild(twrap);
    hdr.appendChild(left);
    var right = h('div', 'hdr-right');
    ((menu.brand && menu.brand.badges) || []).forEach(function (b) {
      if (b) right.appendChild(h('div', 'pill', b));
    });
    hdr.appendChild(right);
    return hdr;
  }
  function buildMarquee(menu) {
    if (!menu.ticker) return null;
    var bar = h('div', 'marquee');
    var track = h('div', 'marquee-track');
    var text = menu.ticker + '  •  ';
    var span1 = h('span', 'marquee-text'); setML(span1, text + text);
    var span2 = h('span', 'marquee-text'); setML(span2, text + text);
    track.appendChild(span1); track.appendChild(span2);
    bar.appendChild(track);
    return bar;
  }
  function buildPips(menu) {
    var total = (menu.screens || []).length;
    if (total < 2) return null;
    var pips = h('div', 'pips');
    for (var i = 0; i < total; i++) pips.appendChild(h('div', 'pip' + (i === SCREEN - 1 ? ' on' : '')));
    return pips;
  }

  /* ── crossfade rotator ───────────────────────────────────────── */

  function rotate(slides, dots, intervalSec, offsetMs) {
    if (slides.length < 2) return;
    var cur = 0;
    setTimeout(function () {
      every(Math.max(2, intervalSec || 5) * 1000, function () {
        slides[cur].classList.remove('on');
        if (dots && dots[cur]) dots[cur].classList.remove('on');
        cur = (cur + 1) % slides.length;
        slides[cur].classList.add('on');
        if (dots && dots[cur]) dots[cur].classList.add('on');
      });
    }, offsetMs || 0);
  }

  /* ── LAYOUT: feature-list ────────────────────────────────────── */

  function renderFeatureList(root, menu, screen) {
    var opts = screen.options || {};
    var cols = Math.min(Math.max(opts.columns || 2, 1), 3);
    var panel = opts.panel === undefined ? 'right' : opts.panel;
    var showThumbs = opts.thumbs !== false;

    root.appendChild(buildHeader(menu, screen));
    var body = h('div', 'fl-body');

    var template = [];
    for (var i = 0; i < cols; i++) template.push('35fr');
    if (panel === 'left') template.unshift('30fr');
    else if (panel === 'right') template.push('30fr');
    body.style.gridTemplateColumns = template.join(' ');

    // distribute sections over columns, keeping order & balancing row counts
    var secs = listSections(screen);
    var weight = function (s) { return visibleItems(s).length + 2; };
    var totalRows = secs.reduce(function (n, s) { return n + weight(s); }, 0);
    var perCol = totalRows / cols;
    var colWraps = [], used = 0, colIdx = 0;
    for (i = 0; i < cols; i++) {
      var cw = h('div', 'fl-col');
      cw.setAttribute('data-fit', 'rows');
      cw.setAttribute('data-fit-watch', '.fl-row');
      colWraps.push(cw);
    }
    secs.forEach(function (sec, si) {
      if (colIdx < cols - 1 && used + weight(sec) / 2 > perCol * (colIdx + 1)) colIdx++;
      used += weight(sec);
      colWraps[colIdx].appendChild(buildFLSection(sec, si, showThumbs));
    });

    var panelEl = panel === 'off' ? null : buildFeaturePanel(menu, screen, opts);
    if (panel === 'left' && panelEl) body.appendChild(panelEl);
    colWraps.forEach(function (c) { body.appendChild(c); });
    if (panel === 'right' && panelEl) body.appendChild(panelEl);

    root.appendChild(body);

    var mq = buildMarquee(menu);
    if (mq) root.appendChild(mq);
    else { var pips = buildPips(menu); if (pips) root.appendChild(pips); }

    if (opts.takeover && opts.takeover.enabled) buildTakeover(root, menu, screen, opts.takeover);
  }

  function buildFLSection(sec, si, showThumbs) {
    var el = h('section', 'fl-sec');
    el.style.flex = String(Math.max(visibleItems(sec).length, 1));
    setAccent(el, sec.accent || ((si % 3) + 1));

    var hdr = h('div', 'fl-sec-hdr');
    hdr.appendChild(h('div', 'fl-sec-title', sec.title));
    if (sec.subtitle) hdr.appendChild(h('div', 'fl-sec-sub', sec.subtitle));
    if (sec.deals && sec.deals.length) {
      var deals = h('div', 'fl-deals');
      sec.deals.forEach(function (d) { if (d) deals.appendChild(h('span', 'deal-chip', d)); });
      hdr.appendChild(deals);
    }
    el.appendChild(hdr);

    var list = h('div', 'fl-list');
    visibleItems(sec).forEach(function (it) {
      var row = h('div', 'fl-row');
      if (showThumbs && it.image) {
        var t = h('div', 'fl-thumb');
        t.appendChild(mkImg(it.image, it.name));
        row.appendChild(t);
      }
      var info = h('div', 'fl-info');
      var nameEl = h('div', 'fl-name', it.name);
      if (it.badge) nameEl.appendChild(h('span', 'fl-badge-inline', it.badge));
      info.appendChild(nameEl);
      if (it.detail) info.appendChild(h('div', 'fl-detail', it.detail));
      row.appendChild(info);
      row.appendChild(pricesEl(it.prices, 'fl-prices'));
      list.appendChild(row);
    });
    el.appendChild(list);
    return el;
  }

  function buildFeaturePanel(menu, screen, opts) {
    var pool = featuredPool(screen);
    if (!pool.length) return null;
    var panel = h('aside', 'fl-panel');
    panel.setAttribute('data-fit', 'stack');
    panel.setAttribute('data-fit-watch', '.fl-slide');
    panel.appendChild(h('div', 'fl-panel-label', opts.panelLabel || 'Featured'));
    var stage = h('div', 'fl-stage');
    var dotsWrap = h('div', 'fl-dots');
    var slides = [], dots = [];
    pool.forEach(function (entry, i) {
      var it = entry.item;
      var s = h('div', 'fl-slide' + (i === 0 ? ' on' : ''));
      setAccent(s, entry.accent);
      if (it.image) {
        var iw = h('div', 'fl-slide-imgwrap');
        iw.appendChild(mkImg(it.image, it.name));
        s.appendChild(iw);
      }
      if (it.badge) { var b = h('div', 'fl-slide-badge'); b.appendChild(h('span', 'badge-chip', it.badge)); s.appendChild(b); }
      s.appendChild(h('div', 'fl-slide-name', it.name));
      if (it.detail) s.appendChild(h('div', 'fl-slide-detail', it.detail));
      s.appendChild(pricesEl(firstPrice(it.prices), 'fl-slide-price'));
      stage.appendChild(s);
      slides.push(s);
      var d = h('div', 'dot' + (i === 0 ? ' on' : ''));
      dotsWrap.appendChild(d); dots.push(d);
    });
    panel.appendChild(stage);
    panel.appendChild(dotsWrap);
    rotate(slides, dots, (opts.intervalSec || 5));
    return panel;
  }

  /* takeover: periodically fades a fullscreen promo over the menu */
  function buildTakeover(root, menu, screen, tk) {
    var pool = featuredPool(screen);
    if (!pool.length) return;
    var layer = h('div', 'tk-layer');
    var bg = h('div', 'tk-bg');
    var bgImg = h('div', 'tk-bg-img');
    bg.appendChild(bgImg);
    bg.appendChild(h('div', 'tk-bg-overlay'));
    layer.appendChild(bg);

    var top = h('div', 'tk-top');
    if (menu.logo) { var lg = mkImg(menu.logo, menu.name); lg.className = 'tk-logo'; top.appendChild(lg); }
    var dots = h('div', 'tk-dots');
    pool.forEach(function (_, i) { dots.appendChild(h('div', 'dot' + (i === 0 ? ' on' : ''))); });
    top.appendChild(dots);
    layer.appendChild(top);

    var main = h('div', 'tk-main');
    main.setAttribute('data-fit', 'stack');
    main.setAttribute('data-fit-watch', '.tk-text');
    var imgwrap = h('div', 'tk-imgwrap');
    imgwrap.appendChild(h('div', 'tk-img-glow'));
    var img = document.createElement('img');
    img.className = 'tk-img';
    img.onerror = function () { img.style.display = 'none'; };
    imgwrap.appendChild(img);
    main.appendChild(imgwrap);
    var text = h('div', 'tk-text');
    main.appendChild(text);
    layer.appendChild(main);
    root.appendChild(layer);

    var idx = -1;
    function showPromo(i) {
      var entry = pool[i], it = entry.item;
      setAccent(layer, entry.accent);
      bgImg.style.backgroundImage = (it.bgImage || it.image) ? "url('" + imgURL(it.bgImage || it.image).replace(/'/g, "%27") + "')" : 'none';
      img.style.display = '';
      if (it.image) img.src = imgURL(it.image); else img.style.display = 'none';
      text.innerHTML = '';
      if (it.badge) { var bw = h('div'); bw.appendChild(h('span', 'badge-chip', it.badge)); text.appendChild(bw); }
      text.appendChild(h('div', 'tk-name', it.name));
      if (it.detail) text.appendChild(h('div', 'tk-detail', it.detail));
      var pr = h('div', 'tk-prices');
      (it.prices || []).slice(0, 2).forEach(function (p) {
        pr.appendChild(pricesEl([p], 'tk-price'));
      });
      text.appendChild(pr);
      var ds = dots.children;
      for (var k = 0; k < ds.length; k++) ds[k].className = 'dot' + (k === i ? ' on' : '');
      queueFit();
    }
    var menuMs = Math.max(6, tk.menuSec || 20) * 1000;
    var promoMs = Math.max(5, tk.promoSec || 15) * 1000;
    function cycle() {
      timers.push(setTimeout(function () {
        idx = (idx + 1) % pool.length;
        showPromo(idx);
        layer.classList.add('on');
        timers.push(setTimeout(function () {
          layer.classList.remove('on');
          cycle();
        }, promoMs));
      }, menuMs));
    }
    cycle();
  }

  /* ── LAYOUT: cards ───────────────────────────────────────────── */

  function renderCards(root, menu, screen) {
    var opts = screen.options || {};
    var body = h('div', 'cd-body');

    if (opts.sidebar === 'brand') body.appendChild(buildBrandPanel(menu));
    else root.appendChild(buildHeader(menu, screen));

    var main = h('div', 'cd-main');
    if (opts.sidebar === 'brand') {
      var tb = h('div');
      if (screen.subtitle) tb.appendChild(h('div', 'cd-kicker', screen.subtitle));
      tb.appendChild(h('div', 'cd-title', screen.title));
      main.appendChild(tb);
    }

    var grid = h('div', 'cd-grid');
    var cols = Math.min(Math.max(opts.columns || 2, 1), 3);
    grid.style.gridTemplateColumns = 'repeat(' + cols + ', minmax(0,1fr))';

    listSections(screen).forEach(function (sec, si) {
      grid.appendChild(buildCardSection(sec, si));
    });
    main.appendChild(grid);
    body.appendChild(main);
    root.appendChild(body);

    var mq = buildMarquee(menu);
    if (mq) root.appendChild(mq);
    else { var pips = buildPips(menu); if (pips) root.appendChild(pips); }
  }

  function buildCardSection(sec, si) {
    var card = h('div', 'cd-card' + (sec.featured ? ' featured' : ''));
    card.setAttribute('data-fit', 'scroll');
    setAccent(card, sec.accent || ((si % 3) + 1));
    card.appendChild(h('div', 'cd-sec-title', sec.title));
    if (sec.subtitle) card.appendChild(h('div', 'cd-sec-sub', sec.subtitle));
    if (sec.deals && sec.deals.length) {
      var deals = h('div', 'cd-deals');
      sec.deals.forEach(function (d) { if (d) deals.appendChild(h('span', 'deal-chip', d)); });
      card.appendChild(deals);
    }
    var rows = h('div', 'cd-rows');
    visibleItems(sec).forEach(function (it) {
      var row = h('div', 'cd-row');
      var nameWrap = h('div');
      var name = h('div', 'cd-row-name', it.name);
      nameWrap.appendChild(name);
      if (it.badge) nameWrap.appendChild(h('div', 'cd-row-badge', it.badge));
      if (it.detail) nameWrap.appendChild(h('div', 'cd-row-detail', it.detail));
      row.appendChild(nameWrap);
      row.appendChild(h('div', 'cd-dots'));
      row.appendChild(inlinePricesEl(it.prices, 'cd-row-prices'));
      rows.appendChild(row);
    });
    card.appendChild(rows);
    if (sec.image) {
      var iw = h('div', 'cd-img');
      iw.appendChild(mkImg(sec.image, sec.title));
      card.appendChild(iw);
    }
    if (sec.note) {
      var note = h('div', 'cd-note');
      var m = /^(includes)\s+(.*)$/i.exec(sec.note);
      if (m) { note.appendChild(h('b', null, m[1])); setML(note.appendChild(h('span')), m[2]); }
      else setML(note, sec.note);
      card.appendChild(note);
    }
    return card;
  }

  function buildBrandPanel(menu) {
    var brand = menu.brand || {};
    var bp = h('aside', 'bp');
    bp.setAttribute('data-fit', 'stack');
    var rule1 = h('div', 'bp-rule');
    rule1.appendChild(h('div', 'line')); rule1.appendChild(h('div', 'diamond')); rule1.appendChild(h('div', 'line'));
    bp.appendChild(rule1);
    if (menu.logo) {
      var lw = h('div', 'bp-logowrap');
      lw.appendChild(mkImg(menu.logo, menu.name));
      bp.appendChild(lw);
    }
    var nm = h('div');
    nm.appendChild(h('div', 'bp-name', menu.name));
    if (menu.tagline) nm.appendChild(h('div', 'bp-tagline', menu.tagline));
    bp.appendChild(nm);
    if (brand.status) {
      var st = h('div', 'bp-status');
      st.appendChild(h('div', 'dot'));
      st.appendChild(h('span', null, brand.status));
      bp.appendChild(st);
    }
    if (brand.hours) {
      var hb = h('div', 'bp-block');
      hb.appendChild(h('div', 'lbl', brand.hoursLabel || 'Hours'));
      hb.appendChild(h('div', 'val', brand.hours));
      bp.appendChild(hb);
    }
    if (brand.wifi) {
      var wb = h('div', 'bp-block');
      wb.appendChild(h('div', 'lbl', 'Free WiFi'));
      wb.appendChild(h('div', 'val', brand.wifi));
      bp.appendChild(wb);
    }
    if (brand.social) bp.appendChild(h('div', 'bp-social', brand.social));
    var rule2 = h('div', 'bp-rule');
    rule2.appendChild(h('div', 'line')); rule2.appendChild(h('div', 'diamond')); rule2.appendChild(h('div', 'line'));
    bp.appendChild(rule2);
    return bp;
  }

  /* ── LAYOUT: classic ─────────────────────────────────────────── */

  function renderClassic(root, menu, screen) {
    var opts = screen.options || {};
    var body = h('div', 'cl-body');
    if (opts.sidebar === 'brand') body.appendChild(buildBrandPanel(menu));

    var main = h('div', 'cl-main');
    var head = h('div', 'cl-head');
    if (screen.subtitle) head.appendChild(h('div', 'cl-kicker', screen.subtitle));
    head.appendChild(h('div', 'cl-title', screen.title));
    var rule = h('div', 'cl-rule');
    rule.appendChild(h('div', 'line')); rule.appendChild(h('div', 'diamond')); rule.appendChild(h('div', 'line'));
    head.appendChild(rule);
    main.appendChild(head);

    var cols = Math.min(Math.max(opts.columns || 2, 1), 3);
    var colsWrap = h('div', 'cl-cols');
    colsWrap.style.gridTemplateColumns = 'repeat(' + cols + ', minmax(0,1fr))';
    var colEls = [];
    for (var i = 0; i < cols; i++) {
      var ce = h('div', 'cl-col');
      ce.setAttribute('data-fit', 'scroll');
      colEls.push(ce);
    }

    var secs = listSections(screen);
    var weight = function (s) { return visibleItems(s).length + 1.5; };
    var totalRows = secs.reduce(function (n, s) { return n + weight(s); }, 0);
    var perCol = totalRows / cols, used = 0, colIdx = 0;
    secs.forEach(function (sec, si) {
      if (colIdx < cols - 1 && used + weight(sec) / 2 > perCol * (colIdx + 1)) colIdx++;
      used += weight(sec);
      colEls[colIdx].appendChild(buildClassicSection(sec, si));
    });
    colEls.forEach(function (c) { colsWrap.appendChild(c); });
    main.appendChild(colsWrap);
    body.appendChild(main);
    root.appendChild(body);

    var mq = buildMarquee(menu);
    if (mq) root.appendChild(mq);
    else { var pips = buildPips(menu); if (pips) root.appendChild(pips); }
  }

  function buildClassicSection(sec, si) {
    var el = h('section', 'cl-sec');
    setAccent(el, sec.accent || ((si % 3) + 1));
    var hdr = h('div', 'cl-sec-hdr');
    hdr.appendChild(h('div', 'cl-sec-title', sec.title));
    hdr.appendChild(h('div', 'cl-sec-line'));
    el.appendChild(hdr);
    if (sec.deals && sec.deals.length) {
      var deals = h('div', 'cl-deals');
      sec.deals.forEach(function (d) { if (d) deals.appendChild(h('span', 'deal-chip', d)); });
      el.appendChild(deals);
    }
    var items = h('div', 'cl-items');
    visibleItems(sec).forEach(function (it) {
      var item = h('div', 'cl-item');
      var line = h('div', 'cl-item-line');
      var nm = h('span', 'cl-item-name', it.name);
      line.appendChild(nm);
      if (it.badge) line.appendChild(h('span', 'cl-item-badge', it.badge));
      line.appendChild(h('span', 'cl-item-dots'));
      line.appendChild(inlinePricesEl(it.prices, 'cl-item-prices'));
      item.appendChild(line);
      if (it.detail) item.appendChild(h('div', 'cl-item-desc', it.detail));
      items.appendChild(item);
    });
    el.appendChild(items);
    if (sec.note) {
      var note = h('div', 'cl-note');
      var m = /^(includes)\s+(.*)$/i.exec(sec.note);
      if (m) { note.appendChild(h('b', null, m[1])); setML(note.appendChild(h('span')), m[2]); }
      else setML(note, sec.note);
      el.appendChild(note);
    }
    return el;
  }

  /* ── LAYOUT: photo-grid ──────────────────────────────────────── */

  function renderPhotoGrid(root, menu, screen) {
    var opts = screen.options || {};
    root.appendChild(buildHeader(menu, screen));
    var body = h('div', 'pg-body');
    var secs = listSections(screen);
    secs.forEach(function (sec, si) {
      var wrap = h('div', 'pg-sec');
      wrap.setAttribute('data-fit', 'scroll');
      wrap.setAttribute('data-fit-watch', '.pg-card');
      wrap.style.flex = String(Math.max(visibleItems(sec).length, 1));
      setAccent(wrap, sec.accent || ((si % 3) + 1));
      var hdr = h('div', 'pg-sec-hdr');
      hdr.appendChild(h('div', 'pg-sec-title', sec.title));
      hdr.appendChild(h('div', 'pg-sec-line'));
      if (sec.deals && sec.deals.length) {
        var deals = h('div', 'pg-deals');
        sec.deals.forEach(function (d) { if (d) deals.appendChild(h('span', 'deal-chip', d)); });
        hdr.appendChild(deals);
      }
      wrap.appendChild(hdr);
      var grid = h('div', 'pg-grid');
      var items = visibleItems(sec);
      var cols = Math.min(Math.max(opts.columns || Math.max(items.length, 3), 2), 6);
      grid.style.gridTemplateColumns = 'repeat(' + cols + ', minmax(0,1fr))';
      items.forEach(function (it) {
        var card = h('div', 'pg-card');
        if (it.badge) { var b = h('div', 'pg-card-badge'); b.appendChild(h('span', 'badge-chip', it.badge)); card.appendChild(b); }
        var iw = h('div', 'pg-card-img');
        if (it.image) iw.appendChild(mkImg(it.image, it.name));
        card.appendChild(iw);
        var info = h('div', 'pg-card-info');
        info.appendChild(h('div', 'pg-card-name', it.name));
        if (it.detail) info.appendChild(h('div', 'pg-card-detail', it.detail));
        info.appendChild(inlinePricesEl(it.prices, 'pg-card-prices'));
        card.appendChild(info);
        grid.appendChild(card);
      });
      wrap.appendChild(grid);
      body.appendChild(wrap);
    });
    root.appendChild(body);
    var mq = buildMarquee(menu);
    if (mq) root.appendChild(mq);
  }

  /* ── LAYOUT: showcase ────────────────────────────────────────── */

  function renderShowcase(root, menu, screen) {
    var opts = screen.options || {};
    var pool = featuredPool(screen);
    var slots = Math.min(Math.max(opts.slots || 3, 1), 3);
    slots = Math.min(slots, Math.max(pool.length, 1));
    var doRotate = opts.rotate !== false && pool.length > slots;

    var body = h('div', 'sc-body');
    body.style.gridTemplateColumns = 'repeat(' + slots + ', 1fr)';

    // partition pool round-robin across slots
    var bySlot = [];
    for (var i = 0; i < slots; i++) bySlot.push([]);
    pool.forEach(function (entry, i) { bySlot[i % slots].push(entry); });

    bySlot.forEach(function (entries, slotIdx) {
      var slot = h('div', 'sc-slot');
      slot.setAttribute('data-fit', 'stack');
      slot.setAttribute('data-fit-watch', '.sc-content');
      var slides = [], dots = [];
      var shown = doRotate ? entries : entries.slice(0, 1);
      shown.forEach(function (entry, i) {
        var it = entry.item;
        var s = h('div', 'sc-slide' + (i === 0 ? ' on' : ''));
        setAccent(s, entry.accent);
        var bg = h('div', 'sc-bg' + (it.bgImage ? '' : ' blurred'));
        var bgSrc = it.bgImage || it.image;
        if (bgSrc) bg.style.backgroundImage = "url('" + imgURL(bgSrc).replace(/'/g, '%27') + "')";
        s.appendChild(bg);
        s.appendChild(h('div', 'sc-shade'));
        var content = h('div', 'sc-content');
        if (it.image) {
          var iw = h('div', 'sc-imgwrap');
          iw.appendChild(h('div', 'glow'));
          iw.appendChild(mkImg(it.image, it.name));
          content.appendChild(iw);
        }
        if (it.badge) { var bw = h('div', 'sc-badge'); bw.appendChild(h('span', 'badge-chip', it.badge)); content.appendChild(bw); }
        content.appendChild(h('div', 'sc-name', it.name));
        if (it.detail) content.appendChild(h('div', 'sc-detail', it.detail));
        content.appendChild(inlinePricesEl(it.prices && it.prices.slice(0, 2), 'sc-prices'));
        s.appendChild(content);
        slot.appendChild(s);
        slides.push(s);
      });
      if (doRotate && shown.length > 1) {
        var dotsWrap = h('div', 'sc-dots');
        shown.forEach(function (_, i) {
          var d = h('div', 'dot' + (i === 0 ? ' on' : ''));
          dotsWrap.appendChild(d); dots.push(d);
        });
        slot.appendChild(dotsWrap);
        rotate(slides, dots, opts.intervalSec || 8, slotIdx * 1200);
      }
      body.appendChild(slot);
    });

    root.appendChild(body);

    var hdr = h('div', 'sc-hdr');
    if (menu.logo) { var lg = mkImg(menu.logo, menu.name); lg.className = 'hdr-logo'; hdr.appendChild(lg); }
    hdr.appendChild(h('div', 'sc-hdr-title', screen.title || menu.name));
    root.appendChild(hdr);

    var mq = buildMarquee(menu);
    if (mq) root.appendChild(mq);
  }

  /* ── main render ─────────────────────────────────────────────── */

  var LAYOUTS = {
    'feature-list': renderFeatureList,
    'cards': renderCards,
    'classic': renderClassic,
    'photo-grid': renderPhotoGrid,
    'showcase': renderShowcase
  };

  function render(menu, screenIdx) {
    clearTimers();
    var screen = (menu.screens || [])[screenIdx - 1];
    document.title = (menu.name || 'Menu') + (screen && screen.title ? ' — ' + screen.title : '');
    applyTheme(menu);

    var old = document.querySelector('.screen');
    if (old) old.remove();
    var oldTk = document.querySelector('.tk-layer');
    if (oldTk) oldTk.remove();
    if (!document.querySelector('.bg-fx')) {
      document.body.appendChild(h('div', 'bg-fx'));
      document.body.appendChild(h('div', 'noise'));
    }
    var root = h('div', 'screen');
    document.body.appendChild(root);

    if (!screen) {
      showError('Screen ' + screenIdx + ' is not configured', 'Add it in the menu editor');
      return;
    }
    var fn = LAYOUTS[screen.layout] || renderFeatureList;
    fn(root, menu, screen);
    queueFit();
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(queueFit);
  }

  function showError(msg, sub) {
    var el = h('div', 'boot-error');
    el.appendChild(h('div', null, msg));
    if (sub) el.appendChild(h('div', 'be-sub', sub));
    document.body.appendChild(el);
  }

  /* ── data loading ────────────────────────────────────────────── */

  function fetchMenu(cb) {
    fetch(BASE + '/' + CLIENT + '/menu.json?t=' + Date.now())
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(cb)
      .catch(function (err) { console.warn('menu fetch failed:', err); cb(null); });
  }

  function boot() {
    if (!CLIENT) { showError('No client configured', 'window.MENU_CLIENT is missing'); return; }

    if (PREVIEW) {
      window.addEventListener('message', function (ev) {
        var d = ev.data;
        if (!d || d.type !== 'menus:preview' || !d.menu) return;
        if (typeof d.screen === 'number') SCREEN = d.screen;
        try { render(d.menu, SCREEN); } catch (e) { console.error(e); }
      });
      // initial paint from published data while waiting for a draft
      fetchMenu(function (menu) {
        if (menu && !document.querySelector('.screen .hdr, .screen .cd-body, .screen .cl-body, .screen .sc-body, .screen .pg-body')) {
          currentVersion = menu.version;
          render(menu, SCREEN);
        }
      });
      if (window.parent !== window) {
        window.parent.postMessage({ type: 'menus:ready', client: CLIENT, screen: SCREEN }, '*');
      }
      return;
    }

    fetchMenu(function (menu) {
      if (!menu) {
        showError('Menu failed to load', 'Retrying automatically…');
        setTimeout(boot, 15000);
        return;
      }
      currentVersion = menu.version;
      render(menu, SCREEN);
      every(POLL_MS, function () {
        fetchMenu(function (fresh) {
          if (fresh && fresh.version !== currentVersion) location.reload();
        });
      });
    });
  }

  var resizeT = null;
  window.addEventListener('resize', function () {
    clearTimeout(resizeT);
    resizeT = setTimeout(fitAll, 150);
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
