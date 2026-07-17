'use strict';

/* Shared helpers for API functions. Written to work both on Vercel's
   Node runtime and under tools/dev-server.mjs. */

function send(res, status, obj) {
  res.statusCode = status;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.setHeader('Cache-Control', 'no-store');
  res.end(JSON.stringify(obj));
}

function readBody(req, limitBytes) {
  return new Promise(function (resolve, reject) {
    if (req.body !== undefined) {
      // Vercel already parsed it
      if (typeof req.body === 'string') {
        try { return resolve(JSON.parse(req.body)); } catch (e) { return resolve({}); }
      }
      return resolve(req.body || {});
    }
    var chunks = [], size = 0, limit = limitBytes || 8 * 1024 * 1024;
    req.on('data', function (c) {
      size += c.length;
      if (size > limit) { reject(new Error('body too large')); req.destroy(); return; }
      chunks.push(c);
    });
    req.on('end', function () {
      if (!chunks.length) return resolve({});
      try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); }
      catch (e) { resolve({}); }
    });
    req.on('error', reject);
  });
}

function getQuery(req) {
  if (req.query) return req.query;
  var u = new URL(req.url, 'http://x');
  var q = {};
  u.searchParams.forEach(function (v, k) { q[k] = v; });
  return q;
}

function parseCookies(req) {
  var out = {};
  var raw = req.headers.cookie || '';
  raw.split(';').forEach(function (part) {
    var i = part.indexOf('=');
    if (i > 0) out[part.slice(0, i).trim()] = decodeURIComponent(part.slice(i + 1).trim());
  });
  return out;
}

/* CSRF guard for state-changing requests: browsers send Origin (or at
   least Sec-Fetch-Site) on cross-site requests; require same-origin. */
function sameOrigin(req) {
  var origin = req.headers.origin;
  var host = req.headers['x-forwarded-host'] || req.headers.host || '';
  if (origin) {
    try { return new URL(origin).host === host; } catch (e) { return false; }
  }
  var sfs = req.headers['sec-fetch-site'];
  if (sfs) return sfs === 'same-origin' || sfs === 'none';
  return true; // non-browser client; cookies won't be attached cross-site anyway
}

module.exports = { send: send, readBody: readBody, getQuery: getQuery, parseCookies: parseCookies, sameOrigin: sameOrigin };
