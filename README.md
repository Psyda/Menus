# Menus — digital signage + self-serve editor

Restaurant/dispensary menu screens hosted on GitHub Pages, with a
phone-friendly editor that lets each client change items, prices and
photos themselves — published straight to their TVs in ~2 minutes.

## The pieces

| Path | What it is |
|---|---|
| `index.html` | Router for TVs — `?page=pinecone1` → `pinecone/page1.html` (reads `clients.json`) |
| `assets/display.js` + `display.css` | The display engine: 5 themes × 5 layout presets, auto-fit text, rotation/showcase/takeover, 60s version polling, live-preview mode |
| `<client>/menu.json` | All of a client's content + design choices — the single source of truth |
| `<client>/page1..3.html` | Thin shells (client id + screen number) so existing TV URLs keep working |
| `<client>/images/` | That client's photo library |
| `admin/` | The editor UI (static, served by Vercel) |
| `api/` | Vercel serverless functions: login/session, publish (commits via GitHub API), image upload |
| `tools/dev-server.mjs` | `npm run dev` — run everything locally, no GitHub needed |
| `tools/hash-password.mjs` | `npm run hash-password` — bcrypt hashes for user accounts |
| `SETUP.md` | Deploy + operations guide (start here) |

## Quick start

```bash
npm install
npm run dev          # editor at http://localhost:3000/admin (demo/demo, admin/admin)
```

Displays: `http://localhost:3000/?page=pinecone1` … `greenroom3`.

## Clients

- **The Pine Cone** — smokehouse theme; brand-panel cards, feature-list with
  rotating burger showcase, price-card grid.
- **The Green Room** — neon theme; feature-list screens with product photo
  rows, featured slider, and a full-screen promo takeover on screen 2.

Deployment, adding clients/users, and the `menu.json` schema: see
[SETUP.md](SETUP.md).
