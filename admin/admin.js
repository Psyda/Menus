/* ═══════════════════════════════════════════════════════════════════
   MENU EDITOR
   Vanilla JS single-page app. Talks to /api/* (Vercel functions).
   Drafts live in localStorage until "Publish" commits them to the
   repo; the TVs poll menu.json and reload themselves.
   ═══════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';

  /* ── option metadata (mirrors api/_lib/validate.js) ──────────── */

  var THEMES = [
    { id: 'neon', name: 'Neon', desc: 'Dark + electric green & purple', colors: ['#08090c', '#00e87c', '#b06ef3'] },
    { id: 'smokehouse', name: 'Smokehouse', desc: 'Charcoal + warm gold', colors: ['#1a1a1a', '#d4a843', '#b0461d'] },
    { id: 'chalkboard', name: 'Chalkboard', desc: 'Slate + hand-drawn chalk', colors: ['#1c2320', '#f7d488', '#a2c8e8'] },
    { id: 'bistro', name: 'Bistro', desc: 'Cream + terracotta (light)', colors: ['#f4eee2', '#b4552d', '#66753f'] },
    { id: 'midnight', name: 'Midnight', desc: 'Pure black + electric blue', colors: ['#000000', '#3b82f6', '#f43f5e'] }
  ];
  var LAYOUTS = [
    { id: 'feature-list', name: 'Feature list', desc: 'Menu columns + big rotating featured panel' },
    { id: 'showcase', name: 'Showcase', desc: 'Full-screen hero photos, 1–3 at a time' },
    { id: 'photo-grid', name: 'Photo grid', desc: 'A photo card for every item' },
    { id: 'cards', name: 'Price cards', desc: 'Boxed sections with price lists' },
    { id: 'classic', name: 'Classic columns', desc: 'Elegant text menu, dotted prices' }
  ];
  var FONTS = {
    display: ['Bebas Neue', 'Anton', 'Oswald', 'Archivo Black', 'Alfa Slab One', 'Playfair Display', 'Cormorant Garamond', 'Permanent Marker', 'Caveat', 'Lobster'],
    body: ['Barlow', 'Inter', 'Montserrat', 'Source Sans 3', 'Lora']
  };
  var ACCENTS = { 1: 'Accent 1', 2: 'Accent 2', 3: 'Accent 3' };

  /* ── state ───────────────────────────────────────────────────── */

  var S = {
    user: null,
    clients: [],
    client: null,        // active client id
    published: null,     // last loaded published menu (object)
    draft: null,         // working copy
    images: null,        // [{path,size}] cache
    localImages: {},     // path -> blob URL for images uploaded this session
    route: { view: 'home', screen: 1, tab: 'items' },
    apiDown: false
  };

  function draftKey() { return 'menus-draft-' + S.client; }
  function deep(o) { return JSON.parse(JSON.stringify(o)); }
  function isDirty() {
    return S.draft && S.published && JSON.stringify(S.draft) !== JSON.stringify(S.published);
  }
  function saveDraft() {
    try { localStorage.setItem(draftKey(), JSON.stringify({ baseVersion: S.published.version, draft: S.draft })); } catch (e) {}
  }
  function clearDraft() {
    try { localStorage.removeItem(draftKey()); } catch (e) {}
  }
  function uid(prefix) { return prefix + '-' + Math.random().toString(36).slice(2, 8); }

  /* ── api ─────────────────────────────────────────────────────── */

  function api(method, path, body) {
    return fetch(path, {
      method: method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      credentials: 'same-origin'
    }).then(function (res) {
      var ct = res.headers.get('content-type') || '';
      if (ct.indexOf('json') < 0) {
        S.apiDown = true;
        throw { status: res.status, error: 'The editor backend is not reachable here. Open the editor from its Vercel address.' };
      }
      return res.json().then(function (data) {
        if (!res.ok) throw { status: res.status, error: data.error || ('HTTP ' + res.status), data: data };
        return data;
      });
    }, function () {
      throw { status: 0, error: 'No connection. Check your internet and try again.' };
    });
  }

  /* ── dom helpers ─────────────────────────────────────────────── */

  var app = document.getElementById('app');

  function el(tag, attrs, children) {
    var n = document.createElement(tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      var v = attrs[k];
      if (v == null) return;
      if (k === 'class') n.className = v;
      else if (k === 'text') n.textContent = v;
      else if (k === 'html') n.innerHTML = v; // only used with ICONS constants below
      else if (k.indexOf('on') === 0) n.addEventListener(k.slice(2), v);
      else if (k === 'value') n.value = v;
      else if (k === 'checked') n.checked = !!v;
      else n.setAttribute(k, v);
    });
    (children || []).forEach(function (c) {
      if (c == null) return;
      n.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
    });
    return n;
  }

  var ICONS = {
    back: '<svg viewBox="0 0 24 24"><path d="M15 18l-6-6 6-6"/></svg>',
    chev: '<svg viewBox="0 0 24 24"><path d="M9 6l6 6-6 6"/></svg>',
    plus: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
    x: '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    trash: '<svg viewBox="0 0 24 24"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/></svg>',
    up: '<svg viewBox="0 0 24 24"><path d="M6 15l6-6 6 6"/></svg>',
    down: '<svg viewBox="0 0 24 24"><path d="M6 9l6 6 6-6"/></svg>',
    img: '<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="14" rx="2"/><circle cx="9" cy="10" r="1.6"/><path d="M4 18l5-5 3 3 4-4 4 4"/></svg>',
    photo: '<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="14" rx="2"/><circle cx="9" cy="10" r="1.6"/><path d="M4 18l5-5 3 3 4-4 4 4"/></svg>',
    upload: '<svg viewBox="0 0 24 24"><path d="M12 16V4M6 10l6-6 6 6M4 20h16"/></svg>',
    eye: '<svg viewBox="0 0 24 24"><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg>',
    paint: '<svg viewBox="0 0 24 24"><path d="M12 3l9 9-7 7a3 3 0 01-4 0l-5-5a3 3 0 010-4l7-7z"/><path d="M19 15c1 1.5 2 3 2 4a2 2 0 11-4 0c0-1 1-2.5 2-4z"/></svg>',
    tv: '<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="12" rx="2"/><path d="M8 21h8"/></svg>',
    out: '<svg viewBox="0 0 24 24"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9"/></svg>',
    expand: '<svg viewBox="0 0 24 24"><path d="M8 3H3v5M16 3h5v5M8 21H3v-5M16 21h5v-5"/></svg>'
  };
  function icon(name) {
    var s = el('span');
    s.innerHTML = ICONS[name] || '';
    return s.firstChild;
  }

  /* ── toast & confirm ─────────────────────────────────────────── */

  var toastEl = null, toastTimer = null;
  function toast(msg, kind) {
    if (!toastEl) { toastEl = el('div', { class: 'toast' }); document.body.appendChild(toastEl); }
    toastEl.textContent = msg;
    toastEl.className = 'toast on ' + (kind || '');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.className = 'toast ' + (kind || ''); }, 3200);
  }

  function confirmSheet(title, message, okLabel, danger) {
    return new Promise(function (resolve) {
      var ov = overlay();
      var sheet = el('div', { class: 'sheet' }, [
        el('div', { class: 'sheet-head' }, [el('div', { class: 't', text: title })]),
        el('div', { class: 'sheet-body' }, [el('div', { style: 'color:var(--sub);font-size:14.5px;line-height:1.5', text: message })]),
        el('div', { class: 'sheet-foot' }, [
          el('button', { class: 'btn', text: 'Cancel', onclick: function () { close(false); } }),
          el('button', { class: 'btn ' + (danger ? 'danger' : 'primary'), text: okLabel || 'OK', onclick: function () { close(true); } })
        ])
      ]);
      ov.appendChild(sheet);
      function close(v) { ov.remove(); resolve(v); }
      ov.addEventListener('click', function (e) { if (e.target === ov) close(false); });
    });
  }

  function overlay() {
    var ov = el('div', { class: 'overlay' });
    document.body.appendChild(ov);
    return ov;
  }

  /* ── boot & routing ──────────────────────────────────────────── */

  function boot() {
    api('GET', '/api/me').then(function (data) {
      S.user = data.user;
      S.clients = data.clients || [];
      var saved = null;
      try { saved = localStorage.getItem('menus-last-client'); } catch (e) {}
      S.client = (S.clients.find(function (c) { return c.id === saved; }) || S.clients[0] || {}).id || S.user.client;
      if (!S.client) { renderError('Your account has no client assigned. Ask your menu admin.'); return; }
      loadClient();
    }).catch(function (e) {
      if (e.status === 401) renderLogin();
      else renderLogin(e.error);
    });
  }

  function loadClient() {
    try { localStorage.setItem('menus-last-client', S.client); } catch (e) {}
    S.images = null;
    api('GET', '/api/menu?client=' + encodeURIComponent(S.client)).then(function (data) {
      S.published = data.menu;
      S.draft = null;
      try {
        var raw = localStorage.getItem(draftKey());
        if (raw) {
          var d = JSON.parse(raw);
          if (d.baseVersion === S.published.version && d.draft) S.draft = d.draft;
          else clearDraft();
        }
      } catch (e) {}
      if (!S.draft) S.draft = deep(S.published);
      S.route = { view: 'home', screen: 1, tab: 'items' };
      render();
    }).catch(function (e) {
      renderError(e.error || 'Could not load the menu.');
    });
  }

  function go(view, screen, tab) {
    S.route = { view: view, screen: screen || S.route.screen, tab: tab || 'items' };
    render();
    window.scrollTo(0, 0);
  }

  /* ── render root ─────────────────────────────────────────────── */

  function render() {
    app.innerHTML = '';
    var v = S.route.view;
    if (v === 'home') renderHome();
    else if (v === 'screen') renderScreen();
    else if (v === 'brand') renderBrand();
    else if (v === 'photos') renderPhotos();
    renderPublishBar();
  }

  function renderError(msg) {
    app.innerHTML = '';
    app.appendChild(el('div', { class: 'login-wrap' }, [
      el('div', { class: 'login-card' }, [
        el('div', { class: 'login-logo', text: '🍽️' }),
        el('div', { class: 'login-title', text: 'Menu Editor' }),
        el('div', { class: 'api-warn', text: msg }),
        el('button', { class: 'btn block', text: 'Try again', onclick: function () { location.reload(); } })
      ])
    ]));
  }

  /* ── login ───────────────────────────────────────────────────── */

  function renderLogin(notice) {
    app.innerHTML = '';
    var err = el('div', { class: 'login-err' + (notice ? ' on' : ''), text: notice || '' });
    var userIn = el('input', { type: 'text', autocomplete: 'username', autocapitalize: 'none', placeholder: 'e.g. greenroom', id: 'lu' });
    var passIn = el('input', { type: 'password', autocomplete: 'current-password', placeholder: '••••••••', id: 'lp' });
    var btn = el('button', { class: 'btn primary block', text: 'Sign in' });
    var form = el('form', {
      onsubmit: function (e) {
        e.preventDefault();
        btn.disabled = true; btn.textContent = 'Signing in…';
        api('POST', '/api/login', { username: userIn.value.trim(), password: passIn.value })
          .then(function () { boot(); })
          .catch(function (er) {
            btn.disabled = false; btn.textContent = 'Sign in';
            err.textContent = er.error; err.classList.add('on');
          });
      }
    }, [
      err,
      el('div', { class: 'field' }, [el('label', { text: 'Username' }), userIn]),
      el('div', { style: 'height:14px' }),
      el('div', { class: 'field' }, [el('label', { text: 'Password' }), passIn]),
      el('div', { style: 'height:20px' }),
      btn
    ]);
    app.appendChild(el('div', { class: 'login-wrap' }, [
      el('div', { class: 'login-card' }, [
        el('div', { class: 'login-logo', text: '🍽️' }),
        el('div', { class: 'login-title', text: 'Menu Editor' }),
        el('div', { class: 'login-sub', text: 'Sign in to update your menu screens' }),
        form
      ])
    ]));
  }

  /* ── topbar ──────────────────────────────────────────────────── */

  function clientObj() {
    return S.clients.find(function (c) { return c.id === S.client; }) || { id: S.client, name: S.client };
  }

  function topbar() {
    var brand = el('div', { class: 'brand' }, [
      el('img', { src: logoURL(), alt: '', onerror: function (e) { e.target.style.visibility = 'hidden'; } }),
      el('div', {}, [
        el('div', { class: 't', text: S.draft ? (S.draft.name || clientObj().name) : 'Menu Editor' }),
        el('div', { class: 's', text: 'Menu editor' })
      ])
    ]);
    var right = [];
    if (S.user && S.user.role === 'admin' && S.clients.length > 1) {
      var sel = el('select', {
        class: 'client-pick',
        onchange: function (e) { S.client = e.target.value; loadClient(); }
      }, S.clients.map(function (c) {
        var o = el('option', { value: c.id, text: c.name });
        if (c.id === S.client) o.selected = true;
        return o;
      }));
      right.push(sel);
    }
    right.push(el('button', {
      class: 'icon-btn', title: 'Sign out',
      onclick: function () {
        confirmSheet('Sign out?', 'Unpublished drafts stay saved on this device.', 'Sign out').then(function (ok) {
          if (ok) api('POST', '/api/logout').then(function () { location.reload(); });
        });
      }
    }, [icon('out')]));
    return el('div', { class: 'topbar' }, [brand].concat(right));
  }

  function logoURL() {
    var logo = S.draft && S.draft.logo;
    if (!logo) return '';
    return resolveImg(logo);
  }

  function resolveImg(path) {
    if (!path) return '';
    if (S.localImages[path]) return S.localImages[path];
    if (/^(https?:)?\/\//.test(path) || /^data:|^blob:/.test(path)) return path;
    return '../' + S.client + '/' + path;
  }

  /* ── home ────────────────────────────────────────────────────── */

  function renderHome() {
    app.appendChild(topbar());
    var page = el('div', { class: 'page' });
    if (S.apiDown) page.appendChild(el('div', { class: 'api-warn', text: 'Backend not reachable — this looks like the static site. Open the editor from its Vercel address to publish changes.' }));

    page.appendChild(el('div', { class: 'page-title', text: 'Screens' }));
    page.appendChild(el('div', { class: 'page-sub', text: 'Tap a screen to edit its items and prices.' }));

    var cards = el('div', { class: 'screen-cards' });
    (S.draft.screens || []).forEach(function (scr, i) {
      var lay = LAYOUTS.find(function (l) { return l.id === scr.layout; }) || LAYOUTS[0];
      var items = 0;
      (scr.sections || []).forEach(function (s) { items += (s.items || []).length; });
      cards.appendChild(el('button', { class: 'screen-card', onclick: function () { go('screen', i + 1, 'items'); } }, [
        el('div', { class: 'num', text: String(i + 1) }),
        el('div', { class: 'info' }, [
          el('div', { class: 'name', text: scr.title || ('Screen ' + (i + 1)) }),
          el('div', { class: 'meta', text: lay.name + ' · ' + items + ' items' })
        ]),
        el('span', { class: 'chev' }, [icon('chev')])
      ]));
    });
    page.appendChild(cards);

    var quick = el('div', { class: 'quick-links' });
    quick.appendChild(el('button', { class: 'screen-card', onclick: function () { go('brand'); } }, [
      el('div', { class: 'num' }, [icon('paint')]),
      el('div', { class: 'info' }, [
        el('div', { class: 'name', text: 'Look & Brand' }),
        el('div', { class: 'meta', text: 'Theme, fonts, ticker' })
      ])
    ]));
    quick.appendChild(el('button', { class: 'screen-card', onclick: function () { go('photos'); } }, [
      el('div', { class: 'num' }, [icon('photo')]),
      el('div', { class: 'info' }, [
        el('div', { class: 'name', text: 'Photos' }),
        el('div', { class: 'meta', text: 'Upload & manage' })
      ])
    ]));
    page.appendChild(quick);
    app.appendChild(page);
  }

  /* ── screen view (items + design tabs) ───────────────────────── */

  function curScreen() {
    return S.draft.screens[S.route.screen - 1];
  }

  function renderScreen() {
    var scr = curScreen();
    if (!scr) { go('home'); return; }
    app.appendChild(topbar());
    var page = el('div', { class: 'page' });

    page.appendChild(el('div', { class: 'back-row', style: 'padding:0' }, [
      el('button', { class: 'back-btn', onclick: function () { go('home'); } }, [icon('back'), 'All screens'])
    ]));
    page.appendChild(el('div', { class: 'page-title', text: scr.title || ('Screen ' + S.route.screen) }));
    page.appendChild(el('div', { class: 'page-sub', text: 'Screen ' + S.route.screen + ' · TV link ?page=' + S.client + S.route.screen }));

    var tab = S.route.tab;
    var tabs = el('div', { class: 'tabs' }, [
      el('button', { class: tab === 'items' ? 'on' : '', text: 'Items & prices', onclick: function () { go('screen', S.route.screen, 'items'); } }),
      el('button', { class: tab === 'design' ? 'on' : '', text: 'Layout & design', onclick: function () { go('screen', S.route.screen, 'design'); } }),
      el('button', { class: tab === 'preview' ? 'on' : '', text: 'Preview', onclick: function () { go('screen', S.route.screen, 'preview'); } })
    ]);
    page.appendChild(tabs);

    if (tab === 'items') renderItemsTab(page, scr);
    else if (tab === 'design') renderDesignTab(page, scr);
    else renderPreviewTab(page, scr);

    app.appendChild(page);
  }

  /* — items tab (the 99% mode) — */

  function renderItemsTab(page, scr) {
    (scr.sections || []).forEach(function (sec, si) {
      var block = el('div', { class: 'sec-block' });
      var head = el('div', { class: 'sec-head' }, [
        el('div', { class: 'st', text: sec.title || 'Untitled section' }),
        sec.role === 'featured' ? el('span', { class: 'badge', text: 'Promo pool' }) : null,
        el('span', { class: 'count', text: String((sec.items || []).length) })
      ]);
      block.appendChild(head);

      var list = el('div', { class: 'item-list' });
      (sec.items || []).forEach(function (it, ii) {
        list.appendChild(itemRow(sec, it, si, ii));
      });
      block.appendChild(list);

      block.appendChild(el('button', {
        class: 'add-item-btn',
        onclick: function () { openItemSheet(sec, null); }
      }, [icon('plus'), 'Add to ' + (sec.title || 'section')]));

      page.appendChild(block);
    });

    if (!(scr.sections || []).length) {
      page.appendChild(el('div', { class: 'empty-note', text: 'No sections yet — add one in Layout & design.' }));
    }
  }

  function priceSummary(it) {
    var ps = (it.prices || []).filter(function (p) { return p.value || p.label; });
    if (!ps.length) return el('span', { class: 'pr', text: '—' });
    var wrap = el('span', { class: 'pr' });
    wrap.appendChild(document.createTextNode(ps[0].label ? ps[0].label + ' ' + ps[0].value : ps[0].value));
    if (ps.length > 1) wrap.appendChild(el('span', { class: 'pl2', text: '+' + (ps.length - 1) + ' more' }));
    return wrap;
  }

  function itemRow(sec, it, si, ii) {
    var thumb = el('div', { class: 'thumb' }, it.image
      ? [el('img', { src: resolveImg(it.image), onerror: function (e) { e.target.remove(); } })]
      : [icon('img')]);
    var row = el('button', { class: 'item-row' + (it.hidden ? ' hidden-item' : ''), onclick: function () { openItemSheet(sec, it); } }, [
      thumb,
      el('div', { class: 'mid' }, [
        el('div', { class: 'nm' }, [
          it.featured ? el('span', { class: 'star', text: '★ ' }) : null,
          document.createTextNode(it.name || 'Untitled'),
          it.hidden ? document.createTextNode('  (hidden)') : null
        ]),
        it.detail ? el('div', { class: 'dt', text: it.detail }) : null
      ]),
      priceSummary(it)
    ]);
    var arrows = el('div', { class: 'row-arrows' }, [
      el('button', { title: 'Move up', onclick: function (e) { e.stopPropagation(); moveItem(sec, ii, -1); } }, [icon('up')]),
      el('button', { title: 'Move down', onclick: function (e) { e.stopPropagation(); moveItem(sec, ii, 1); } }, [icon('down')])
    ]);
    var wrap = el('div', { style: 'display:flex;align-items:center;gap:4px' }, [row, arrows]);
    row.style.flex = '1';
    row.style.minWidth = '0';
    return wrap;
  }

  function moveItem(sec, idx, dir) {
    var items = sec.items;
    var j = idx + dir;
    if (j < 0 || j >= items.length) return;
    var t = items[idx]; items[idx] = items[j]; items[j] = t;
    changed();
    render();
  }

  /* — item edit sheet — */

  function openItemSheet(sec, it) {
    var isNew = !it;
    var work = it ? deep(it) : { id: uid('i'), name: '', detail: '', prices: [{ label: '', value: '' }], image: '', bgImage: '', badge: '', featured: false, hidden: false };

    var ov = overlay();
    var nameIn = el('input', { type: 'text', value: work.name, placeholder: 'e.g. Bacon Jam Special' });
    var detailIn = el('textarea', { rows: 2, placeholder: 'Short description (optional)' });
    detailIn.value = work.detail || '';
    var badgeIn = el('input', { type: 'text', value: work.badge || '', placeholder: 'e.g. New! (optional)' });

    var priceWrap = el('div', { class: 'price-rows' });
    function priceRow(p) {
      var lab = el('input', { class: 'lab', type: 'text', value: p.label || '', placeholder: 'Label' });
      var val = el('input', { class: 'val', type: 'text', value: p.value || '', placeholder: '$0.00' });
      var row = el('div', { class: 'price-row-edit' }, [
        lab, val,
        el('button', { class: 'rm', title: 'Remove price', onclick: function () { row.remove(); } }, [icon('x')])
      ]);
      row._read = function () { return { label: lab.value.trim(), value: val.value.trim() }; };
      return row;
    }
    (work.prices && work.prices.length ? work.prices : [{ label: '', value: '' }]).forEach(function (p) {
      priceWrap.appendChild(priceRow(p));
    });

    var imgPrev = el('div', { class: 'prev' }, work.image ? [el('img', { src: resolveImg(work.image) })] : [icon('img')]);
    var imgBtn = el('button', { class: 'btn', text: work.image ? 'Change photo' : 'Add photo', onclick: function () {
      openImagePicker(work.image, function (path) {
        work.image = path || '';
        imgPrev.innerHTML = '';
        if (work.image) imgPrev.appendChild(el('img', { src: resolveImg(work.image) }));
        else imgPrev.appendChild(icon('img'));
        imgBtn.textContent = work.image ? 'Change photo' : 'Add photo';
      });
    } });

    var featSw = switchRow('Feature this item', 'Shows in the big rotating panel / showcase', work.featured, function (v) { work.featured = v; });
    var hideSw = switchRow('Hide from screen', 'Keeps it saved, stops showing it (sold out etc.)', work.hidden, function (v) { work.hidden = v; });

    var foot = [
      el('button', { class: 'btn', text: 'Cancel', onclick: close })
    ];
    if (!isNew) {
      foot.push(el('button', { class: 'btn danger', title: 'Delete', onclick: function () {
        confirmSheet('Delete "' + (work.name || 'this item') + '"?', 'This removes it from the menu. You can add it again later.', 'Delete', true).then(function (ok) {
          if (!ok) return;
          sec.items = sec.items.filter(function (x) { return x.id !== it.id; });
          changed(); close(); render();
        });
      } }, [icon('trash')]));
    }
    foot.push(el('button', { class: 'btn primary', text: isNew ? 'Add item' : 'Save', onclick: function () {
      work.name = nameIn.value.trim();
      work.detail = detailIn.value.trim();
      work.badge = badgeIn.value.trim();
      work.prices = Array.prototype.map.call(priceWrap.children, function (r) { return r._read(); })
        .filter(function (p) { return p.label || p.value; });
      if (!work.name) { toast('Give it a name first', 'err'); return; }
      if (isNew) sec.items.push(work);
      else {
        var idx = sec.items.findIndex(function (x) { return x.id === it.id; });
        if (idx >= 0) sec.items[idx] = work;
      }
      changed(); close(); render();
    } }));

    var sheet = el('div', { class: 'sheet' }, [
      el('div', { class: 'sheet-head' }, [
        el('div', { class: 't', text: isNew ? 'Add item — ' + (sec.title || 'section') : 'Edit item' }),
        el('button', { class: 'icon-btn', onclick: close }, [icon('x')])
      ]),
      el('div', { class: 'sheet-body' }, [
        el('div', { class: 'field' }, [el('label', { text: 'Name' }), nameIn]),
        el('div', { class: 'field' }, [el('label', { text: 'Description' }), detailIn]),
        el('div', { class: 'field' }, [
          el('label', { text: 'Prices' }),
          priceWrap,
          el('button', { class: 'btn small subtle', onclick: function () { priceWrap.appendChild(priceRow({ label: '', value: '' })); } }, [icon('plus'), 'Add another price']),
          el('div', { class: 'hint', text: 'Label is optional — use it for sizes or deals (Reg / Lg, 1, 2, 3+).' })
        ]),
        el('div', { class: 'field' }, [el('label', { text: 'Photo' }), el('div', { class: 'img-pick' }, [imgPrev, imgBtn])]),
        el('div', { class: 'field' }, [el('label', { text: 'Badge' }), badgeIn]),
        featSw, hideSw
      ]),
      el('div', { class: 'sheet-foot' }, foot)
    ]);
    ov.appendChild(sheet);
    ov.addEventListener('click', function (e) { if (e.target === ov) close(); });
    function close() { ov.remove(); }
    if (isNew) setTimeout(function () { nameIn.focus(); }, 250);
  }

  function switchRow(label, desc, value, onchange) {
    var sw = el('div', { class: 'switch' + (value ? ' on' : '') });
    var row = el('button', { class: 'switch-row', onclick: function () {
      value = !value;
      sw.classList.toggle('on', value);
      onchange(value);
    } }, [
      el('div', {}, [
        el('div', { class: 'sl', text: label }),
        desc ? el('div', { class: 'sd', text: desc }) : null
      ]),
      sw
    ]);
    return row;
  }

  /* — design tab — */

  function renderDesignTab(page, scr) {
    // screen basics
    var g1 = el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Screen heading' }),
      el('div', { class: 'field' }, [el('label', { text: 'Title' }),
        textInput(scr.title, function (v) { scr.title = v; }, 'e.g. Burgers & Sandwiches')]),
      el('div', { class: 'field' }, [el('label', { text: 'Subtitle' }),
        textInput(scr.subtitle, function (v) { scr.subtitle = v; }, 'Small line above/under the title')])
    ]);
    page.appendChild(g1);

    // layout preset
    var layGrid = el('div', { class: 'card-grid' });
    LAYOUTS.forEach(function (l) {
      layGrid.appendChild(el('button', {
        class: 'pick-card' + (scr.layout === l.id ? ' on' : ''),
        onclick: function () { scr.layout = l.id; changed(); render(); }
      }, [
        el('div', { class: 'pv' }, [layoutMock(l.id)]),
        el('div', { class: 'nm', text: l.name }),
        el('div', { class: 'ds', text: l.desc })
      ]));
    });
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Layout preset' }), layGrid
    ]));

    // layout options
    page.appendChild(layoutOptionsGroup(scr));

    // sections manager
    var secman = el('div', { class: 'secman' });
    (scr.sections || []).forEach(function (sec, si) {
      var row = el('button', { class: 'secman-row', onclick: function () { openSectionSheet(scr, sec); } }, [
        el('span', { class: 'accdot', style: 'background:' + accentColor(sec.accent) }),
        el('div', { style: 'flex:1;min-width:0' }, [
          el('div', { class: 't', text: sec.title || 'Untitled' }),
          el('div', { class: 'm', text: (sec.items || []).length + ' items' + (sec.role === 'featured' ? ' · promo pool' : '') + (sec.featured ? ' · large card' : '') })
        ]),
        el('span', { class: 'row-arrows' }, [
          el('button', { onclick: function (e) { e.stopPropagation(); moveSection(scr, si, -1); } }, [icon('up')]),
          el('button', { onclick: function (e) { e.stopPropagation(); moveSection(scr, si, 1); } }, [icon('down')])
        ])
      ]);
      secman.appendChild(row);
    });
    secman.appendChild(el('button', { class: 'add-item-btn', onclick: function () {
      var sec = { id: uid('s'), title: 'New section', subtitle: '', accent: (((scr.sections || []).length) % 3) + 1, featured: false, image: '', note: '', deals: [], items: [] };
      scr.sections.push(sec);
      changed(); openSectionSheet(scr, sec);
    } }, [icon('plus'), 'Add section']));

    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Sections' }), secman
    ]));
  }

  function accentColor(n) {
    var t = THEMES.find(function (x) { return x.id === (S.draft.theme || 'neon'); }) || THEMES[0];
    return t.colors[n === 3 ? 1 : n] || t.colors[1]; // rough mapping for the dot
  }

  function textInput(value, save, placeholder, type) {
    return el('input', { type: type || 'text', value: value || '', placeholder: placeholder || '', onchange: function (e) { save(e.target.value.trim()); changed(); } });
  }

  function layoutMock(id) {
    function blk(style) { var b = el('div', { class: 'blk' }); b.style.cssText += ';' + style; return b; }
    var wrap = el('div', { class: 'lay-pv' });
    if (id === 'feature-list') {
      var c1 = el('div', { class: 'lay-col' }, [blk('flex:1'), blk('flex:1'), blk('flex:1')]);
      var c2 = el('div', { class: 'lay-col' }, [blk('flex:1'), blk('flex:1'), blk('flex:1')]);
      var p = blk('flex:0 0 34%'); p.classList.add('accb');
      wrap.appendChild(c1); wrap.appendChild(c2); wrap.appendChild(p);
    } else if (id === 'showcase') {
      wrap.appendChild(blk('flex:1;')); wrap.appendChild(blk('flex:1')); var a = blk('flex:1'); a.classList.add('accb'); wrap.appendChild(a);
    } else if (id === 'photo-grid') {
      for (var i = 0; i < 4; i++) wrap.appendChild(el('div', { class: 'lay-col' }, [blk('flex:1'), blk('flex:1')]));
    } else if (id === 'cards') {
      wrap.appendChild(el('div', { class: 'lay-col' }, [blk('flex:1')]));
      wrap.appendChild(el('div', { class: 'lay-col' }, [blk('flex:1'), blk('flex:1')]));
    } else {
      wrap.appendChild(el('div', { class: 'lay-col' }, [blk('height:6px;width:60%;align-self:center'), blk('flex:1'), blk('flex:1')]));
      wrap.appendChild(el('div', { class: 'lay-col' }, [blk('height:6px;width:60%;align-self:center'), blk('flex:1'), blk('flex:1')]));
    }
    return wrap;
  }

  function layoutOptionsGroup(scr) {
    var o = scr.options = scr.options || {};
    var g = el('div', { class: 'design-group' }, [el('div', { class: 'g-title', text: 'Layout options' })]);
    var lay = scr.layout;

    function segRow(label, options, current, save) {
      var seg = el('div', { class: 'seg' }, options.map(function (opt) {
        return el('button', {
          class: String(opt.v) === String(current) ? 'on' : '', text: opt.t,
          onclick: function () { save(opt.v); changed(); render(); }
        });
      }));
      return el('div', { class: 'field' }, [el('label', { text: label }), seg]);
    }

    if (lay === 'feature-list' || lay === 'classic' || lay === 'cards') {
      segRow('', [], '', function () {}); // noop keeps structure simple
    }
    if (lay === 'feature-list') {
      g.appendChild(segRow('Menu columns', [{ v: 1, t: '1' }, { v: 2, t: '2' }, { v: 3, t: '3' }], o.columns || 2, function (v) { o.columns = v; }));
      g.appendChild(segRow('Featured panel', [{ v: 'left', t: 'Left' }, { v: 'right', t: 'Right' }, { v: 'off', t: 'Off' }], o.panel === undefined ? 'right' : o.panel, function (v) { o.panel = v; }));
      g.appendChild(switchRow('Item photos in the list', 'Small photo next to each item', o.thumbs !== false, function (v) { o.thumbs = v; changed(); }));
      g.appendChild(numField('Featured rotation (seconds per item)', o.intervalSec || 5, 2, 120, function (v) { o.intervalSec = v; }));
      var tk = o.takeover = o.takeover || { enabled: false, menuSec: 20, promoSec: 15 };
      g.appendChild(switchRow('Full-screen promo takeover', 'Now and then the menu fades into a full-screen promo of your featured items', !!tk.enabled, function (v) { tk.enabled = v; changed(); render(); }));
      if (tk.enabled) {
        g.appendChild(el('div', { class: 'fgrid' }, [
          numField('Menu shows for (sec)', tk.menuSec || 20, 6, 600, function (v) { tk.menuSec = v; }),
          numField('Promo shows for (sec)', tk.promoSec || 15, 5, 120, function (v) { tk.promoSec = v; })
        ]));
      }
    } else if (lay === 'showcase') {
      g.appendChild(segRow('Photos on screen at once', [{ v: 1, t: '1' }, { v: 2, t: '2' }, { v: 3, t: '3' }], o.slots || 3, function (v) { o.slots = v; }));
      g.appendChild(switchRow('Rotate through featured items', 'With more featured items than slots, they take turns. Off = always show the first ones.', o.rotate !== false, function (v) { o.rotate = v; changed(); }));
      g.appendChild(numField('Seconds per rotation', o.intervalSec || 8, 2, 120, function (v) { o.intervalSec = v; }));
      g.appendChild(el('div', { class: 'field' }, [el('div', { class: 'hint', text: 'Showcase pulls from items marked ★ Featured (or a “promo pool” section). Mark 3+ items to fill the screen.' })]));
    } else if (lay === 'photo-grid') {
      g.appendChild(segRow('Columns', [{ v: 2, t: '2' }, { v: 3, t: '3' }, { v: 4, t: '4' }, { v: 5, t: '5' }], o.columns || 4, function (v) { o.columns = v; }));
    } else if (lay === 'cards') {
      g.appendChild(segRow('Card columns', [{ v: 1, t: '1' }, { v: 2, t: '2' }, { v: 3, t: '3' }], o.columns || 2, function (v) { o.columns = v; }));
      g.appendChild(segRow('Left side panel', [{ v: 'brand', t: 'Brand panel' }, { v: 'off', t: 'Off' }], o.sidebar || 'off', function (v) { o.sidebar = v; }));
    } else if (lay === 'classic') {
      g.appendChild(segRow('Text columns', [{ v: 1, t: '1' }, { v: 2, t: '2' }, { v: 3, t: '3' }], o.columns || 2, function (v) { o.columns = v; }));
      g.appendChild(segRow('Left side panel', [{ v: 'brand', t: 'Brand panel' }, { v: 'off', t: 'Off' }], o.sidebar || 'off', function (v) { o.sidebar = v; }));
    }
    return g;
  }

  function numField(label, value, min, max, save) {
    return el('div', { class: 'field' }, [
      el('label', { text: label }),
      el('input', { type: 'number', value: value, min: min, max: max, onchange: function (e) {
        var v = parseInt(e.target.value, 10);
        if (isNaN(v)) v = value;
        v = Math.min(Math.max(v, min), max);
        e.target.value = v;
        save(v); changed();
      } })
    ]);
  }

  /* — section sheet — */

  function openSectionSheet(scr, sec) {
    var ov = overlay();
    var titleIn = el('input', { type: 'text', value: sec.title || '', placeholder: 'e.g. Burgers' });
    var subIn = el('input', { type: 'text', value: sec.subtitle || '', placeholder: 'Small kicker line (optional)' });
    var dealsIn = el('input', { type: 'text', value: (sec.deals || []).join(', '), placeholder: 'e.g. 1 for $30, 2 for $50' });
    var noteIn = el('input', { type: 'text', value: sec.note || '', placeholder: 'e.g. Includes Fries & Drink' });

    var accSeg = el('div', { class: 'seg' }, [1, 2, 3].map(function (n) {
      return el('button', {
        class: (sec.accent || 1) === n ? 'on' : '', text: ACCENTS[n],
        onclick: function (e) {
          sec.accent = n; changed();
          Array.prototype.forEach.call(accSeg.children, function (b, i) { b.classList.toggle('on', i === n - 1); });
        }
      });
    }));

    var imgPrev = el('div', { class: 'prev' }, sec.image ? [el('img', { src: resolveImg(sec.image) })] : [icon('img')]);
    var imgBtn = el('button', { class: 'btn', text: sec.image ? 'Change photo' : 'Add photo (optional)', onclick: function () {
      openImagePicker(sec.image, function (path) {
        sec.image = path || ''; changed();
        imgPrev.innerHTML = '';
        if (sec.image) imgPrev.appendChild(el('img', { src: resolveImg(sec.image) }));
        else imgPrev.appendChild(icon('img'));
        imgBtn.textContent = sec.image ? 'Change photo' : 'Add photo (optional)';
      });
    } });

    var featSw = switchRow('Large card', 'Cards layout: this section gets the big double-height card', !!sec.featured, function (v) { sec.featured = v; });
    var roleSw = switchRow('Promo pool only', 'Items here never show in menu lists — they only feed the featured panel, showcase and takeover', sec.role === 'featured', function (v) { if (v) sec.role = 'featured'; else delete sec.role; });

    var sheet = el('div', { class: 'sheet' }, [
      el('div', { class: 'sheet-head' }, [
        el('div', { class: 't', text: 'Section settings' }),
        el('button', { class: 'icon-btn', onclick: close }, [icon('x')])
      ]),
      el('div', { class: 'sheet-body' }, [
        el('div', { class: 'field' }, [el('label', { text: 'Section name' }), titleIn]),
        el('div', { class: 'field' }, [el('label', { text: 'Subtitle' }), subIn]),
        el('div', { class: 'field' }, [el('label', { text: 'Deal chips' }), dealsIn, el('div', { class: 'hint', text: 'Comma-separated. Shown as highlighted chips under the section title.' })]),
        el('div', { class: 'field' }, [el('label', { text: 'Bottom note' }), noteIn]),
        el('div', { class: 'field' }, [el('label', { text: 'Colour accent' }), accSeg]),
        el('div', { class: 'field' }, [el('label', { text: 'Section photo' }), el('div', { class: 'img-pick' }, [imgPrev, imgBtn])]),
        featSw, roleSw
      ]),
      el('div', { class: 'sheet-foot' }, [
        el('button', { class: 'btn', text: 'Cancel', onclick: close }),
        el('button', { class: 'btn danger', onclick: function () {
          confirmSheet('Delete section "' + (sec.title || '') + '"?', 'Deletes the section and its ' + (sec.items || []).length + ' items from this screen.', 'Delete section', true).then(function (ok) {
            if (!ok) return;
            scr.sections = scr.sections.filter(function (x) { return x.id !== sec.id; });
            changed(); close(); render();
          });
        } }, [icon('trash')]),
        el('button', { class: 'btn primary', text: 'Save', onclick: function () {
          sec.title = titleIn.value.trim();
          sec.subtitle = subIn.value.trim();
          sec.deals = dealsIn.value.split(',').map(function (s) { return s.trim(); }).filter(Boolean);
          sec.note = noteIn.value.trim();
          changed(); close(); render();
        } })
      ])
    ]);
    ov.appendChild(sheet);
    ov.addEventListener('click', function (e) { if (e.target === ov) close(); });
    function close() { ov.remove(); }
  }

  function moveSection(scr, idx, dir) {
    var j = idx + dir;
    if (j < 0 || j >= scr.sections.length) return;
    var t = scr.sections[idx]; scr.sections[idx] = scr.sections[j]; scr.sections[j] = t;
    changed(); render();
  }

  /* — preview tab — */

  var previewFrame = null;

  function renderPreviewTab(page, scr) {
    page.appendChild(el('div', { class: 'preview-bar' }, [
      el('div', { style: 'flex:1;font-size:13px;color:var(--sub)', text: 'Live preview with your unpublished changes.' }),
      el('button', { class: 'btn small', onclick: function () { openFullPreview(); } }, [icon('expand'), 'Big']),
      el('button', { class: 'btn small', onclick: function () {
        window.open('../' + S.client + '/page' + S.route.screen + '.html', '_blank');
      } }, [icon('tv'), 'Live page'])
    ]));
    page.appendChild(previewWrap());
  }

  function previewWrap() {
    var wrap = el('div', { class: 'preview-wrap' });
    var iframe = el('iframe', {
      src: '../' + S.client + '/page' + S.route.screen + '.html?preview=1',
      title: 'Menu preview'
    });
    previewFrame = iframe;
    iframe.addEventListener('load', function () { sendPreview(); });
    wrap.appendChild(iframe);
    requestAnimationFrame(function () { scalePreview(wrap, iframe); });
    window.addEventListener('resize', function () { scalePreview(wrap, iframe); });
    return wrap;
  }

  function scalePreview(wrap, iframe) {
    if (!wrap.isConnected) return;
    var w = wrap.clientWidth;
    iframe.style.transform = 'scale(' + (w / 1920) + ')';
  }

  function openFullPreview() {
    var fs = el('div', { class: 'preview-fs' });
    var wrap = previewWrap();
    fs.appendChild(wrap);
    fs.appendChild(el('button', { class: 'close-fs', onclick: function () { fs.remove(); previewFrame = null; } }, [icon('x')]));
    document.body.appendChild(fs);
    requestAnimationFrame(function () {
      var iframe = wrap.querySelector('iframe');
      var scale = Math.min(fs.clientWidth / 1920, fs.clientHeight / 1080);
      wrap.style.width = (1920 * scale) + 'px';
      wrap.style.height = (1080 * scale) + 'px';
      wrap.style.aspectRatio = 'auto';
      iframe.style.transform = 'scale(' + scale + ')';
    });
  }

  var previewT = null;
  function sendPreview() {
    if (!previewFrame || !previewFrame.contentWindow) return;
    clearTimeout(previewT);
    previewT = setTimeout(function () {
      if (!previewFrame || !previewFrame.contentWindow) return;
      var menu = deep(S.draft);
      // swap freshly-uploaded repo paths for local blob URLs (repo copy may still be deploying)
      var swap = function (p) { return (p && S.localImages[p]) ? S.localImages[p] : p; };
      menu.logo = swap(menu.logo);
      (menu.screens || []).forEach(function (scr) {
        (scr.sections || []).forEach(function (sec) {
          sec.image = swap(sec.image);
          (sec.items || []).forEach(function (it) { it.image = swap(it.image); it.bgImage = swap(it.bgImage); });
        });
      });
      previewFrame.contentWindow.postMessage({ type: 'menus:preview', menu: menu, screen: S.route.screen }, '*');
    }, 120);
  }
  window.addEventListener('message', function (ev) {
    if (ev.data && ev.data.type === 'menus:ready') sendPreview();
  });

  /* ── brand & theme view ──────────────────────────────────────── */

  function renderBrand() {
    app.appendChild(topbar());
    var page = el('div', { class: 'page' });
    page.appendChild(el('div', { class: 'back-row', style: 'padding:0' }, [
      el('button', { class: 'back-btn', onclick: function () { go('home'); } }, [icon('back'), 'All screens'])
    ]));
    page.appendChild(el('div', { class: 'page-title', text: 'Look & Brand' }));
    page.appendChild(el('div', { class: 'page-sub', text: 'Applies to all screens.' }));

    var d = S.draft;

    // theme
    var themeGrid = el('div', { class: 'card-grid' });
    THEMES.forEach(function (t) {
      themeGrid.appendChild(el('button', {
        class: 'pick-card' + (d.theme === t.id ? ' on' : ''),
        onclick: function () { d.theme = t.id; changed(); render(); }
      }, [
        el('div', { class: 'pv', style: 'background:' + t.colors[0] }, [
          el('div', { class: 'theme-pv' }, [
            el('span', { class: 'dotc big', style: 'background:' + t.colors[1] }),
            el('span', { class: 'dotc', style: 'background:' + t.colors[2] })
          ])
        ]),
        el('div', { class: 'nm', text: t.name }),
        el('div', { class: 'ds', text: t.desc })
      ]));
    });
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Theme' }), themeGrid
    ]));

    // fonts
    var fonts = d.fonts = d.fonts || {};
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Fonts' }),
      el('div', { class: 'fgrid' }, [
        el('div', { class: 'field' }, [el('label', { text: 'Headings' }),
          fontSelect(FONTS.display, fonts.display || '', function (v) { if (v) fonts.display = v; else delete fonts.display; })]),
        el('div', { class: 'field' }, [el('label', { text: 'Text' }),
          fontSelect(FONTS.body, fonts.body || '', function (v) { if (v) fonts.body = v; else delete fonts.body; })])
      ]),
      el('div', { class: 'hint', text: 'Leave on “Theme default” unless you want a specific look.' })
    ]));

    // identity
    var logoPrev = el('div', { class: 'prev' }, d.logo ? [el('img', { src: resolveImg(d.logo) })] : [icon('img')]);
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Business' }),
      el('div', { class: 'field' }, [el('label', { text: 'Name' }), textInput(d.name, function (v) { d.name = v; })]),
      el('div', { class: 'field' }, [el('label', { text: 'Tagline' }), textInput(d.tagline, function (v) { d.tagline = v; }, 'e.g. Smokehouse & Kitchen')]),
      el('div', { class: 'field' }, [el('label', { text: 'Logo' }), el('div', { class: 'img-pick' }, [
        logoPrev,
        el('button', { class: 'btn', text: 'Change logo', onclick: function () {
          openImagePicker(d.logo, function (path) {
            d.logo = path || ''; changed();
            logoPrev.innerHTML = '';
            if (d.logo) logoPrev.appendChild(el('img', { src: resolveImg(d.logo) }));
            else logoPrev.appendChild(icon('img'));
          });
        } })
      ])])
    ]));

    // ticker + header pills
    var b = d.brand = d.brand || {};
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Extras' }),
      el('div', { class: 'field' }, [el('label', { text: 'Scrolling ticker (bottom of screens)' }),
        textInput(d.ticker, function (v) { d.ticker = v; }, 'Leave empty for none — e.g. Open Daily 8AM – 10PM • Ask about specials'),
      ]),
      el('div', { class: 'field' }, [el('label', { text: 'Header badges' }),
        textInput((b.badges || []).join(', '), function (v) { b.badges = v.split(',').map(function (s) { return s.trim(); }).filter(Boolean); }, 'e.g. Lab Tested, Premium Quality')]),
    ]));

    // brand panel info
    page.appendChild(el('div', { class: 'design-group' }, [
      el('div', { class: 'g-title', text: 'Brand panel details (shown by the “Brand panel” sidebar)' }),
      el('div', { class: 'fgrid' }, [
        el('div', { class: 'field' }, [el('label', { text: 'Hours label' }), textInput(b.hoursLabel, function (v) { b.hoursLabel = v; }, 'Kitchen Hours')]),
        el('div', { class: 'field' }, [el('label', { text: 'Hours' }), textInput(b.hours, function (v) { b.hours = v; }, '11:00 AM — 9:00 PM')])
      ]),
      el('div', { class: 'field' }, [el('label', { text: 'Status line' }), textInput(b.status, function (v) { b.status = v; }, 'e.g. Now Smoking · Low & Slow')]),
      el('div', { class: 'fgrid' }, [
        el('div', { class: 'field' }, [el('label', { text: 'Social handle' }), textInput(b.social, function (v) { b.social = v; }, '@yourplace')]),
        el('div', { class: 'field' }, [el('label', { text: 'WiFi name' }), textInput(b.wifi, function (v) { b.wifi = v; }, 'Guest WiFi')])
      ])
    ]));

    app.appendChild(page);
  }

  function fontSelect(list, current, save) {
    var sel = el('select', { onchange: function (e) { save(e.target.value); changed(); } });
    sel.appendChild(el('option', { value: '', text: 'Theme default' }));
    list.forEach(function (f) {
      var o = el('option', { value: f, text: f });
      if (f === current) o.selected = true;
      sel.appendChild(o);
    });
    return sel;
  }

  /* ── photos view ─────────────────────────────────────────────── */

  function renderPhotos() {
    app.appendChild(topbar());
    var page = el('div', { class: 'page' });
    page.appendChild(el('div', { class: 'back-row', style: 'padding:0' }, [
      el('button', { class: 'back-btn', onclick: function () { go('home'); } }, [icon('back'), 'All screens'])
    ]));
    page.appendChild(el('div', { class: 'page-title', text: 'Photos' }));
    page.appendChild(el('div', { class: 'page-sub', text: 'Product photos available for this menu. Uploads are resized automatically.' }));

    var grid = el('div', { class: 'photo-grid' });
    page.appendChild(grid);
    buildPhotoGrid(grid, null, null, true);
    app.appendChild(page);
  }

  function loadImages() {
    if (S.images) return Promise.resolve(S.images);
    return api('GET', '/api/images?client=' + encodeURIComponent(S.client)).then(function (data) {
      S.images = data.images || [];
      return S.images;
    });
  }

  function buildPhotoGrid(grid, selectedPath, onPick, manage) {
    grid.innerHTML = '';
    var up = el('button', { class: 'upload-tile', onclick: function () { pickFiles(grid, selectedPath, onPick, manage); } }, [icon('upload'), 'Upload']);
    grid.appendChild(up);
    loadImages().then(function (images) {
      images.forEach(function (img) {
        var cell = el('div', { class: 'photo-cell' + (selectedPath === img.path ? ' on' : ''), role: onPick ? 'button' : null }, [
          el('img', { src: resolveImg(img.path), loading: 'lazy', onerror: function (e) { e.target.style.opacity = '0.25'; } }),
          el('div', { class: 'nm', text: img.path.replace(/^images\//, '') })
        ]);
        if (onPick) cell.addEventListener('click', function () { onPick(img.path); });
        if (manage) {
          cell.appendChild(el('button', { class: 'del', title: 'Delete photo', onclick: function (e) {
            e.stopPropagation();
            confirmSheet('Delete this photo?', img.path + '\n\nIf a menu item is still using it, that spot goes empty.', 'Delete', true).then(function (ok) {
              if (!ok) return;
              api('DELETE', '/api/images', { client: S.client, path: img.path }).then(function () {
                S.images = S.images.filter(function (x) { return x.path !== img.path; });
                toast('Photo deleted', 'ok');
                buildPhotoGrid(grid, selectedPath, onPick, manage);
              }).catch(function (er) { toast(er.error, 'err'); });
            });
          } }, [icon('trash')]));
        }
        grid.appendChild(cell);
      });
    }).catch(function (e) {
      grid.appendChild(el('div', { class: 'empty-note', text: e.error || 'Could not load photos' }));
    });
  }

  function pickFiles(grid, selectedPath, onPick, manage) {
    var input = el('input', { type: 'file', accept: 'image/*', multiple: onPick ? null : 'multiple', style: 'display:none' });
    document.body.appendChild(input);
    input.addEventListener('change', function () {
      var files = Array.prototype.slice.call(input.files || []);
      input.remove();
      if (!files.length) return;
      toast('Uploading…');
      var done = 0, lastPath = null;
      var next = function () {
        if (!files.length) {
          toast(done + ' photo' + (done > 1 ? 's' : '') + ' uploaded', 'ok');
          buildPhotoGrid(grid, lastPath || selectedPath, onPick, manage);
          if (onPick && lastPath && files.length === 0 && done === 1) onPick(lastPath);
          return;
        }
        var f = files.shift();
        resizeImage(f).then(function (out) {
          return api('POST', '/api/images', { client: S.client, name: out.name, data: out.dataURL });
        }).then(function (res) {
          done++;
          lastPath = res.path;
          S.images = null; // refetch
          try {
            S.localImages[res.path] = URL.createObjectURL(f);
          } catch (e) {}
          next();
        }).catch(function (er) {
          toast(er.error || 'Upload failed', 'err');
          next();
        });
      };
      next();
    });
    input.click();
  }

  /* client-side resize: max 1600px, jpeg/webp/png preserved reasonably */
  function resizeImage(file) {
    return new Promise(function (resolve, reject) {
      var MAX = 1600;
      var img = new Image();
      var url = URL.createObjectURL(file);
      img.onload = function () {
        var w = img.naturalWidth, hgt = img.naturalHeight;
        var scale = Math.min(1, MAX / Math.max(w, hgt));
        var type = file.type === 'image/png' || file.type === 'image/webp' ? file.type : 'image/jpeg';
        if (scale === 1 && file.size < 900 * 1024) {
          // small enough — send original bytes
          var fr = new FileReader();
          fr.onload = function () { resolve({ name: file.name, dataURL: fr.result }); };
          fr.onerror = reject;
          fr.readAsDataURL(file);
          return;
        }
        var canvas = document.createElement('canvas');
        canvas.width = Math.round(w * scale);
        canvas.height = Math.round(hgt * scale);
        var ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        var dataURL = canvas.toDataURL(type, 0.86);
        var name = file.name.replace(/\.[^.]+$/, '') + (type === 'image/png' ? '.png' : type === 'image/webp' ? '.webp' : '.jpg');
        URL.revokeObjectURL(url);
        resolve({ name: name, dataURL: dataURL });
      };
      img.onerror = function () { reject({ error: 'That file is not an image' }); };
      img.src = url;
    });
  }

  /* ── image picker modal ──────────────────────────────────────── */

  function openImagePicker(currentPath, done) {
    var ov = overlay();
    var grid = el('div', { class: 'photo-grid' });
    var urlIn = el('input', { type: 'url', placeholder: 'https://… (paste an image link instead)', value: /^https?:/.test(currentPath || '') ? currentPath : '' });

    var sheet = el('div', { class: 'sheet' }, [
      el('div', { class: 'sheet-head' }, [
        el('div', { class: 't', text: 'Choose a photo' }),
        el('button', { class: 'icon-btn', onclick: close }, [icon('x')])
      ]),
      el('div', { class: 'sheet-body' }, [
        grid,
        el('div', { class: 'field' }, [el('label', { text: 'Or use a web link' }), urlIn])
      ]),
      el('div', { class: 'sheet-foot' }, [
        el('button', { class: 'btn', text: 'No photo', onclick: function () { done(''); close(); } }),
        el('button', { class: 'btn primary', text: 'Use link', onclick: function () {
          var v = urlIn.value.trim();
          if (v && !/^https:\/\//.test(v)) { toast('Links must start with https://', 'err'); return; }
          if (v) { done(v); close(); }
          else toast('Paste a link first, or tap a photo', 'err');
        } })
      ])
    ]);
    ov.appendChild(sheet);
    ov.addEventListener('click', function (e) { if (e.target === ov) close(); });
    function close() { ov.remove(); }

    buildPhotoGrid(grid, /^https?:/.test(currentPath || '') ? null : currentPath, function (path) {
      done(path); close();
    }, false);
  }

  /* ── publish bar ─────────────────────────────────────────────── */

  var pubBar = null;

  function renderPublishBar() {
    if (!pubBar) {
      pubBar = el('div', { class: 'publish-bar' });
      document.body.appendChild(pubBar);
    }
    pubBar.innerHTML = '';
    if (!S.draft || !S.published) { pubBar.classList.remove('on'); return; }
    if (!isDirty()) { pubBar.classList.remove('on'); return; }
    pubBar.classList.add('on');
    pubBar.appendChild(el('div', { class: 'msg', text: 'Unpublished changes' }));
    pubBar.appendChild(el('button', { class: 'btn subtle', text: 'Discard', onclick: function () {
      confirmSheet('Discard changes?', 'Your unpublished edits will be thrown away and the menu goes back to what is live now.', 'Discard', true).then(function (ok) {
        if (!ok) return;
        S.draft = deep(S.published);
        clearDraft();
        render(); sendPreview();
        toast('Back to the live menu');
      });
    } }));
    var pubBtn = el('button', { class: 'btn primary', text: 'Publish' });
    pubBtn.addEventListener('click', function () { publish(pubBtn); });
    pubBar.appendChild(pubBtn);
  }

  function changed() {
    saveDraft();
    renderPublishBar();
    sendPreview();
  }

  function publish(btn) {
    btn.disabled = true; btn.textContent = 'Publishing…';
    api('PUT', '/api/menu', { client: S.client, baseVersion: S.published.version, menu: S.draft })
      .then(function (data) {
        S.published = data.menu;
        S.draft = deep(data.menu);
        clearDraft();
        render(); sendPreview();
        toast('Published! Screens update in a minute or two.', 'ok');
      })
      .catch(function (e) {
        btn.disabled = false; btn.textContent = 'Publish';
        if (e.status === 409) {
          confirmSheet('Someone else published first', 'The live menu changed while you were editing. Load the latest version? Your current edits will be discarded — note them down first if they were big.', 'Load latest', true)
            .then(function (ok) { if (ok) { clearDraft(); loadClient(); } });
        } else {
          toast(e.error || 'Publish failed', 'err');
        }
      });
  }

  /* ── go ──────────────────────────────────────────────────────── */

  boot();
})();
