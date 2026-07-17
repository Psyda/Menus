'use strict';

var util = require('./_lib/util');
var auth = require('./_lib/auth');
var store = require('./_lib/store');

var EXT = { png: 1, jpg: 1, jpeg: 1, webp: 1, gif: 1, avif: 1 };
var MAX_BYTES = 4 * 1024 * 1024;

module.exports = async function handler(req, res) {
  try {
    if (req.method === 'GET') return await list(req, res);
    if (req.method === 'POST') return await upload(req, res);
    if (req.method === 'DELETE') return await remove(req, res);
    return util.send(res, 405, { error: 'Method not allowed' });
  } catch (e) {
    return util.send(res, 500, { error: 'Server error: ' + e.message });
  }
};

async function list(req, res) {
  var q = util.getQuery(req);
  var sess = auth.requireClientAccess(req, res, q.client);
  if (!sess) return;
  var files = await store.listFiles(q.client + '/images/');
  var images = files
    .filter(function (f) {
      var ext = f.path.split('.').pop().toLowerCase();
      return EXT[ext];
    })
    .map(function (f) {
      return { path: f.path.slice(q.client.length + 1), size: f.size }; // client-relative: images/...
    });
  return util.send(res, 200, { ok: true, images: images });
}

function cleanName(name) {
  var base = String(name || '').split(/[\\/]/).pop() || '';
  var m = /^(.*)\.([a-zA-Z0-9]+)$/.exec(base);
  if (!m) return null;
  var stem = m[1].toLowerCase().replace(/[^a-z0-9-_]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 60) || 'image';
  var ext = m[2].toLowerCase();
  if (!EXT[ext]) return null;
  return stem + '.' + ext;
}

async function upload(req, res) {
  if (!util.sameOrigin(req)) return util.send(res, 403, { error: 'Cross-origin request blocked' });
  var body = await util.readBody(req, 7 * 1024 * 1024); // base64 overhead
  var clientId = String(body.client || '');
  var sess = auth.requireClientAccess(req, res, clientId);
  if (!sess) return;

  var name = cleanName(body.name);
  if (!name) return util.send(res, 400, { error: 'Image must be a png, jpg, webp, gif or avif file' });

  var b64 = String(body.data || '').replace(/^data:[^;]+;base64,/, '');
  var buf;
  try { buf = Buffer.from(b64, 'base64'); } catch (e) { buf = null; }
  if (!buf || !buf.length) return util.send(res, 400, { error: 'No image data received' });
  if (buf.length > MAX_BYTES) return util.send(res, 400, { error: 'Image too large (max 4 MB — the editor usually resizes automatically)' });

  var rel = 'images/uploads/' + name;
  // avoid silently replacing a different file: add a numeric suffix if taken
  var existing = await store.readFile(clientId + '/' + rel);
  if (existing && !body.overwrite) {
    var i = 2, stem = name.replace(/\.[^.]+$/, ''), ext = name.split('.').pop();
    while (existing && i < 50) {
      rel = 'images/uploads/' + stem + '-' + i + '.' + ext;
      existing = await store.readFile(clientId + '/' + rel);
      i++;
    }
  }

  var sha = existing ? (existing.sha === 'local' ? undefined : existing.sha) : undefined;
  await store.writeFile(clientId + '/' + rel, buf, clientId + ': upload ' + rel + ' by ' + sess.sub + ' (menu editor)', sha);
  return util.send(res, 200, { ok: true, path: rel });
}

async function remove(req, res) {
  if (!util.sameOrigin(req)) return util.send(res, 403, { error: 'Cross-origin request blocked' });
  var body = await util.readBody(req, 64 * 1024);
  var clientId = String(body.client || '');
  var sess = auth.requireClientAccess(req, res, clientId);
  if (!sess) return;

  var p = String(body.path || '').replace(/^\/+/, '');
  if (p.indexOf('..') >= 0 || !/^images\/[a-zA-Z0-9 _\-./()+]+$/.test(p)) {
    return util.send(res, 400, { error: 'Bad image path' });
  }
  var ext = p.split('.').pop().toLowerCase();
  if (!EXT[ext]) return util.send(res, 400, { error: 'Not an image' });

  await store.deleteFile(clientId + '/' + p, clientId + ': delete ' + p + ' by ' + sess.sub + ' (menu editor)');
  return util.send(res, 200, { ok: true });
}
