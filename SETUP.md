# Menu System — Setup & Operations

How the pieces fit together, how to deploy the editor backend, and how to
run day-to-day operations (new clients, new users, password resets).

## How it works

```
                 ┌────────────────────────────────────────────┐
                 │  GitHub repo  (this repo — the database)   │
                 │  <client>/menu.json   <client>/images/…    │
                 └────────┬──────────────────────┬────────────┘
                    serves │                     │ commits via GitHub API
                          ▼                      │
      GitHub Pages (displays)          Vercel (editor + API)
      psyda.github.io/Menus            menus-xyz.vercel.app
      TVs point here, unchanged        clients log in here
      pages poll menu.json every       /admin  → editor UI
      60s and reload on change         /api/*  → auth + publish
```

- **Displays** stay on GitHub Pages. Every TV URL that worked before still
  works (`?page=pinecone1`, `pinecone/page1.html`, …). Each page is a thin
  shell that loads `assets/display.js`, which renders `menu.json` with the
  client's chosen theme + layout, then polls for version changes.
- **The repo is the database.** Publishing from the editor commits a new
  `menu.json` (and uploaded images) through the GitHub API. GitHub Pages
  redeploys automatically, and the TVs pick it up on their next poll.
  **A change is live on screen roughly 1–2 minutes after Publish.** Every
  publish is a commit, so you have full history and rollback via git.
- **The editor** is a static page (`admin/`) plus serverless functions
  (`api/`) that run on Vercel. Clients sign in with a username/password,
  edit a draft (saved on their device), preview it live, and hit Publish.

## Deploy the editor on Vercel (one-time, ~10 minutes)

1. **Create the GitHub token** the backend commits with:
   - GitHub → Settings → Developer settings → Fine-grained personal access
     tokens → *Generate new token*.
   - Repository access: **Only select repositories** → `Psyda/Menus`.
   - Permissions → Repository permissions → **Contents: Read and write**.
     Nothing else.
   - Expiry: up to you (set a calendar reminder — publishing breaks when it
     expires; generating a fresh one and updating the env var fixes it).

2. **Generate password hashes** for each account (never store plaintext):
   ```bash
   npm install
   npm run hash-password        # prompts, input hidden
   ```
   Copy the printed JSON user entry for each account.

3. **Generate a session secret**:
   ```bash
   node -e "console.log(require('crypto').randomBytes(48).toString('base64url'))"
   ```

4. **Create the Vercel project**:
   - vercel.com → Add New → Project → import `Psyda/Menus`.
   - Framework preset: **Other**. Build command: none. Output directory:
     default. (`vercel.json` in the repo already configures this.)
   - Add Environment Variables (Production):

     | Name | Value |
     |---|---|
     | `GITHUB_TOKEN` | the fine-grained PAT from step 1 |
     | `GITHUB_REPO` | `Psyda/Menus` |
     | `GITHUB_BRANCH` | `main` |
     | `SESSION_SECRET` | output of step 3 |
     | `MENUS_USERS` | JSON array of user entries from step 2, e.g. `[{"username":"pinecone","hash":"$2a$12$…","client":"pinecone","name":"The Pine Cone","role":"client"},{"username":"travis","hash":"$2a$12$…","client":null,"name":"Travis","role":"admin"}]` |

   - Deploy. The editor is now at `https://<project>.vercel.app/admin/`.
   - Optional: Settings → Domains → add something friendly like
     `menus.yourdomain.com`.

5. **Send each client their link + login.** That's the whole client setup.

Notes:
- Do **not** set `LOCAL_REPO_PATH` on Vercel — that's the local-dev switch.
- `role: "admin"` accounts (you) can edit every client; `role: "client"`
  accounts are locked to their own folder, enforced server-side.
- Vercel will also redeploy on each publish commit (it's git-connected).
  That's harmless — displays run off GitHub Pages either way.

## Day-to-day

### A client edits their menu
1. Open the editor link on their phone → sign in (30-day session).
2. Tap the screen → tap the item → change the price → **Publish**.
3. On screen within ~2 minutes. That's the whole flow.

The **Layout & design** tab is where you (or an adventurous client) switch
layout presets, rotation settings, sections, theme, and fonts. The
**Preview** tab renders the real display page with their unpublished draft.

### Add a new client
1. Create `<clientid>/` (lowercase, digits, dashes) with:
   - `menu.json` — copy one from an existing client, change `"client"`,
     `"name"`, reset `"version": 1`, and swap in their content.
   - `images/` — their logo + product shots.
   - `page1.html`, `page2.html`, `page3.html` — copy from an existing
     client and change `MENU_CLIENT` (and add more pages if they have more
     screens; set `MENU_SCREEN` accordingly).
2. Add them to `clients.json` (id, name, screens, accent for the router).
3. Commit + push — displays are live at `?page=<clientid>1` etc.
4. Create their login: `npm run hash-password`, add the entry to the
   `MENUS_USERS` env var on Vercel, redeploy (Vercel → project →
   Deployments → Redeploy, or just push any commit).

### Add a user / reset a password
Run `npm run hash-password`, update that user's entry in `MENUS_USERS` on
Vercel, redeploy. Sessions survive redeploys (they're signed cookies);
changing `SESSION_SECRET` signs everyone out immediately — that's your
"revoke all sessions" lever.

### Roll back a bad menu change
Every publish is a commit named like
`greenroom: menu update v12 by greenroom (menu editor)`. Revert it:
```bash
git revert <commit> && git push
```
Screens roll back on their next poll. (Then bump `version` if you edit by
hand — displays only reload when `version` changes.)

## Local development

```bash
npm install
npm run dev            # http://localhost:3000
```
- Serves the repo + API with storage pointed at your working copy
  (no GitHub token needed; publishes just edit local files).
- Dev logins (only when `MENUS_USERS` isn't set): `demo`/`demo` (pinecone),
  `admin`/`admin` (all clients).
- Displays: `http://localhost:3000/?page=pinecone1`, editor at `/admin/`.

## Security model

- Passwords: bcrypt (cost 12), stored only as hashes in the `MENUS_USERS`
  env var. Login is rate-limited and timing-safe.
- Sessions: signed JWT in an `HttpOnly; Secure; SameSite=Lax` cookie —
  no tokens in the page, nothing for XSS to steal.
- CSRF: state-changing requests must be same-origin (Origin /
  Sec-Fetch-Site checks).
- Authorization: client accounts can only read/write their own
  `<client>/menu.json` and `<client>/images/**` — enforced in the API, not
  the UI.
- Input: published menus are rebuilt server-side against a strict schema
  (whitelisted fields, length caps, theme/layout/font whitelists, image
  paths confined to `images/` or `https://`). Uploads are extension- and
  size-checked with sanitized filenames. The display renderer never
  injects HTML — all content is text-node based.
- The GitHub token lives only in Vercel env vars, scoped to this single
  repo, contents-only.

## menu.json reference

```jsonc
{
  "schema": 1,
  "version": 12,              // bumped automatically on publish; TVs reload on change
  "client": "pinecone",
  "name": "The Pine Cone",
  "tagline": "Smokehouse & Kitchen",
  "logo": "images/logo.png",
  "theme": "smokehouse",      // neon | smokehouse | chalkboard | bistro | midnight
  "fonts": { "display": "Bebas Neue", "body": "Barlow" },   // optional overrides
  "ticker": "Open Daily …",   // scrolling bottom bar; "" = none
  "brand": {                   // used by header pills + the brand sidebar
    "hours": "11:00 AM — 9:00 PM", "hoursLabel": "Kitchen Hours",
    "status": "Now Smoking · Low & Slow",
    "social": "@thepineconekitchen", "wifi": "PineCone-Guest",
    "badges": ["Hand-Pressed", "Slow-Smoked"]
  },
  "screens": [
    {
      "id": "s1",
      "title": "Smokehouse", "subtitle": "From The Pit",
      "layout": "cards",       // feature-list | showcase | photo-grid | cards | classic
      "options": {
        // feature-list: columns (1-3), panel (left|right|off), thumbs, intervalSec,
        //               takeover { enabled, menuSec, promoSec }
        // showcase:     slots (1-3), rotate, intervalSec
        // photo-grid:   columns (2-5)
        // cards/classic: columns (1-3), sidebar (brand|off)
      },
      "sections": [
        {
          "id": "sec1", "title": "Beef Brisket", "subtitle": "Slow-smoked",
          "accent": 1,                 // 1-3 → theme accent colours
          "featured": true,            // cards layout: big double card
          "role": "featured",          // optional: promo-pool only (items never list)
          "image": "images/…",         // section photo (cards layout)
          "note": "Includes Fries & Drink",
          "deals": ["1 for $30", "2 for $50"],
          "items": [
            {
              "id": "i1", "name": "Quarter Pound", "detail": "4 oz",
              "prices": [ { "label": "", "value": "$13.99" } ],   // label: Reg/Lg/1/2/3+…
              "image": "images/…",     // item photo (or https:// URL)
              "bgImage": "images/…",   // showcase/takeover background (optional)
              "badge": "New!",
              "featured": false,       // ★ include in featured panel/showcase/takeover
              "hidden": false          // keep but don't show (sold out)
            }
          ]
        }
      ]
    }
  ]
}
```

**Where featured photos come from:** the rotating panel, showcase slots and
promo takeover all pull from items marked ★ featured plus any section with
`role: "featured"` (a "promo pool" that never renders in the lists). If
nothing is marked, they fall back to items that have photos. With more
featured items than photo slots, showcase **rotates** through them — the
per-screen `rotate` toggle turns that off.
