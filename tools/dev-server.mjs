#!/usr/bin/env node
/* Local dev server: serves the repo statically and mounts the api/
   functions the same way Vercel does, with LOCAL_REPO_PATH storage so
   publishes write straight to your working copy (no GitHub needed).

   Usage:
     SESSION_SECRET=dev-secret-dev-secret-dev-secret-123 \
     MENUS_USERS='[{"username":"demo","hash":"<bcrypt>","client":"pinecone","name":"Demo","role":"client"}]' \
     node tools/dev-server.mjs [port]
*/
import http from 'http';
import fs from 'fs';
import path from 'path';
import url from 'url';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const ROOT = path.resolve(path.dirname(url.fileURLToPath(import.meta.url)), '..');
const PORT = parseInt(process.argv[2] || process.env.PORT || '3000', 10);

process.env.LOCAL_REPO_PATH = process.env.LOCAL_REPO_PATH || ROOT;
if (!process.env.SESSION_SECRET) {
  process.env.SESSION_SECRET = 'dev-only-secret-do-not-use-in-production-1234';
  console.log('! Using a built-in dev SESSION_SECRET (fine locally, never in production)');
}
if (!process.env.MENUS_USERS) {
  // dev fallback: demo/demo (client on pinecone) and admin/admin (all clients)
  const bcrypt = require('bcryptjs');
  process.env.MENUS_USERS = JSON.stringify([
    { username: 'demo', hash: bcrypt.hashSync('demo', 10), client: 'pinecone', name: 'Pine Cone Demo', role: 'client' },
    { username: 'admin', hash: bcrypt.hashSync('admin', 10), client: null, name: 'Admin', role: 'admin' }
  ]);
  console.log('! MENUS_USERS not set — dev logins: demo/demo (pinecone), admin/admin (all clients)');
}

const MIME = {
  html: 'text/html; charset=utf-8', js: 'text/javascript; charset=utf-8', mjs: 'text/javascript; charset=utf-8',
  css: 'text/css; charset=utf-8', json: 'application/json; charset=utf-8', png: 'image/png', jpg: 'image/jpeg',
  jpeg: 'image/jpeg', webp: 'image/webp', gif: 'image/gif', avif: 'image/avif', svg: 'image/svg+xml', ico: 'image/x-icon'
};

const server = http.createServer(async (req, res) => {
  const u = new URL(req.url, 'http://' + (req.headers.host || 'localhost'));
  const pathname = decodeURIComponent(u.pathname);

  if (pathname.startsWith('/api/')) {
    const name = pathname.slice(5).replace(/\/$/, '');
    const file = path.join(ROOT, 'api', name + '.js');
    if (!/^[a-z0-9-]+$/.test(name) || !fs.existsSync(file)) {
      res.writeHead(404, { 'Content-Type': 'application/json' });
      return res.end('{"error":"No such API route"}');
    }
    try {
      delete require.cache[require.resolve(file)];
      const handler = require(file);
      await handler(req, res);
    } catch (e) {
      console.error(e);
      if (!res.headersSent) res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: 'Dev server error: ' + e.message }));
    }
    return;
  }

  // static files
  let rel = pathname === '/' ? '/index.html' : pathname;
  if (rel.endsWith('/')) rel += 'index.html';
  let file = path.normalize(path.join(ROOT, rel));
  if (!file.startsWith(ROOT)) { res.writeHead(403); return res.end(); }
  if (!fs.existsSync(file) && fs.existsSync(file + '.html')) file += '.html';
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!fs.existsSync(file)) { res.writeHead(404, { 'Content-Type': 'text/plain' }); return res.end('Not found: ' + rel); }
  const ext = file.split('.').pop().toLowerCase();
  res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(file).pipe(res);
});

server.listen(PORT, () => {
  console.log('Menus dev server → http://localhost:' + PORT);
  console.log('  displays: http://localhost:' + PORT + '/?page=pinecone1');
  console.log('  editor:   http://localhost:' + PORT + '/admin/');
});
