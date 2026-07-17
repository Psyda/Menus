'use strict';

/* Storage adapter. Production: GitHub Contents API (the repo IS the
   database — every publish is a commit, GitHub Pages redeploys, TVs
   pick up the new version). Dev: set LOCAL_REPO_PATH to read/write a
   local checkout instead. */

var fs = require('fs');
var path = require('path');

var LOCAL = process.env.LOCAL_REPO_PATH || '';

function ghConfig() {
  var repo = process.env.GITHUB_REPO;
  var token = process.env.GITHUB_TOKEN;
  var branch = process.env.GITHUB_BRANCH || 'main';
  if (!repo || !token) throw new Error('GITHUB_REPO and GITHUB_TOKEN env vars must be set');
  return { repo: repo, token: token, branch: branch };
}

async function gh(method, url, body) {
  var cfg = ghConfig();
  var res = await fetch('https://api.github.com' + url, {
    method: method,
    headers: {
      'Authorization': 'Bearer ' + cfg.token,
      'Accept': 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'menus-admin'
    },
    body: body ? JSON.stringify(body) : undefined
  });
  if (res.status === 404) return { notFound: true };
  if (!res.ok) {
    var text = await res.text();
    throw new Error('GitHub API ' + res.status + ': ' + text.slice(0, 300));
  }
  return res.json();
}

function safeJoin(base, rel) {
  var p = path.normalize(path.join(base, rel));
  if (!p.startsWith(path.normalize(base + path.sep))) throw new Error('bad path');
  return p;
}

/* → { content: Buffer, sha } or null */
async function readFile(relPath) {
  if (LOCAL) {
    var p = safeJoin(LOCAL, relPath);
    if (!fs.existsSync(p)) return null;
    return { content: fs.readFileSync(p), sha: 'local' };
  }
  var cfg = ghConfig();
  var out = await gh('GET', '/repos/' + cfg.repo + '/contents/' + encodePath(relPath) + '?ref=' + encodeURIComponent(cfg.branch));
  if (out.notFound) return null;
  return { content: Buffer.from(out.content || '', 'base64'), sha: out.sha };
}

/* commit create/update */
async function writeFile(relPath, buffer, message, sha) {
  if (LOCAL) {
    var p = safeJoin(LOCAL, relPath);
    fs.mkdirSync(path.dirname(p), { recursive: true });
    fs.writeFileSync(p, buffer);
    return { sha: 'local' };
  }
  var cfg = ghConfig();
  var body = {
    message: message,
    content: buffer.toString('base64'),
    branch: cfg.branch
  };
  if (sha) body.sha = sha;
  var out = await gh('PUT', '/repos/' + cfg.repo + '/contents/' + encodePath(relPath), body);
  return { sha: out.content && out.content.sha };
}

async function deleteFile(relPath, message) {
  if (LOCAL) {
    var p = safeJoin(LOCAL, relPath);
    if (fs.existsSync(p)) fs.unlinkSync(p);
    return { ok: true };
  }
  var cfg = ghConfig();
  var cur = await gh('GET', '/repos/' + cfg.repo + '/contents/' + encodePath(relPath) + '?ref=' + encodeURIComponent(cfg.branch));
  if (cur.notFound) return { ok: true };
  await gh('DELETE', '/repos/' + cfg.repo + '/contents/' + encodePath(relPath), {
    message: message, sha: cur.sha, branch: cfg.branch
  });
  return { ok: true };
}

/* list all file paths under a prefix → [{path, size}] */
async function listFiles(prefix) {
  if (LOCAL) {
    var base = safeJoin(LOCAL, prefix);
    var out = [];
    var walk = function (dir) {
      if (!fs.existsSync(dir)) return;
      fs.readdirSync(dir, { withFileTypes: true }).forEach(function (e) {
        var full = path.join(dir, e.name);
        if (e.isDirectory()) walk(full);
        else out.push({ path: path.relative(LOCAL, full).split(path.sep).join('/'), size: fs.statSync(full).size });
      });
    };
    walk(base);
    return out;
  }
  var cfg = ghConfig();
  var tree = await gh('GET', '/repos/' + cfg.repo + '/git/trees/' + encodeURIComponent(cfg.branch) + '?recursive=1');
  if (tree.notFound || !tree.tree) return [];
  return tree.tree
    .filter(function (n) { return n.type === 'blob' && n.path.indexOf(prefix) === 0; })
    .map(function (n) { return { path: n.path, size: n.size || 0 }; });
}

function encodePath(p) {
  return p.split('/').map(encodeURIComponent).join('/');
}

module.exports = { readFile: readFile, writeFile: writeFile, deleteFile: deleteFile, listFiles: listFiles };
