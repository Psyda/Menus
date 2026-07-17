'use strict';

var crypto = require('crypto');
var bcrypt = require('bcryptjs');
var jwt = require('jsonwebtoken');
var util = require('./util');

var COOKIE = 'menus_session';
var SESSION_DAYS = 30;

// dummy hash so unknown usernames take the same time as wrong passwords
var DUMMY_HASH = '$2a$12$C6UzMDM.H6dfI/f/IKcEeO7ZUr5oWsUYzQY1QNq1qNq1qNq1qNq1q';

function secret() {
  var s = process.env.SESSION_SECRET;
  if (!s || s.length < 32) throw new Error('SESSION_SECRET env var must be set (32+ random characters)');
  return s;
}

function loadUsers() {
  var raw = process.env.MENUS_USERS;
  if (!raw) throw new Error('MENUS_USERS env var is not set');
  var users;
  try { users = JSON.parse(raw); } catch (e) { throw new Error('MENUS_USERS is not valid JSON'); }
  if (!Array.isArray(users)) throw new Error('MENUS_USERS must be a JSON array');
  return users.filter(function (u) { return u && u.username && u.hash; });
}

/* best-effort in-memory rate limit (per serverless instance) */
var attempts = {}; // key -> { n, resetAt }
var MAX_ATTEMPTS = 8;
var WINDOW_MS = 10 * 60 * 1000;

function rateLimited(key) {
  var now = Date.now();
  var a = attempts[key];
  if (!a || a.resetAt < now) { attempts[key] = { n: 0, resetAt: now + WINDOW_MS }; a = attempts[key]; }
  return a.n >= MAX_ATTEMPTS;
}
function recordFailure(key) {
  var a = attempts[key];
  if (a) a.n++;
}
function clientIP(req) {
  var xf = req.headers['x-forwarded-for'];
  return (xf ? String(xf).split(',')[0].trim() : req.socket && req.socket.remoteAddress) || 'unknown';
}

function verifyLogin(req, username, password) {
  var key = clientIP(req) + '|' + String(username).toLowerCase();
  if (rateLimited(key)) return { error: 'Too many attempts. Try again in a few minutes.', status: 429 };
  var users = loadUsers();
  var user = users.find(function (u) { return u.username.toLowerCase() === String(username).toLowerCase(); });
  var ok = bcrypt.compareSync(String(password), user ? user.hash : DUMMY_HASH);
  if (!user || !ok) {
    recordFailure(key);
    return { error: 'Wrong username or password.', status: 401 };
  }
  return { user: user };
}

function signSession(user) {
  return jwt.sign(
    { sub: user.username, client: user.client || null, role: user.role === 'admin' ? 'admin' : 'client', name: user.name || user.username },
    secret(),
    { expiresIn: SESSION_DAYS + 'd' }
  );
}

function sessionCookie(token, req) {
  var host = req.headers['x-forwarded-host'] || req.headers.host || '';
  var secure = !/^localhost(:|$)|^127\.0\.0\.1(:|$)/.test(host);
  var parts = [
    COOKIE + '=' + token,
    'Path=/',
    'HttpOnly',
    'SameSite=Lax',
    'Max-Age=' + (SESSION_DAYS * 86400)
  ];
  if (secure) parts.push('Secure');
  return parts.join('; ');
}

function clearCookie(req) {
  return COOKIE + '=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0';
}

/* Returns the session payload or null. */
function getSession(req) {
  var cookies = util.parseCookies(req);
  var token = cookies[COOKIE];
  if (!token) return null;
  try { return jwt.verify(token, secret()); } catch (e) { return null; }
}

/* Auth + client-scope check. Sends the error response itself and
   returns null when unauthorized. */
function requireClientAccess(req, res, clientId) {
  var sess = getSession(req);
  if (!sess) { util.send(res, 401, { error: 'Not signed in' }); return null; }
  if (!clientId) { util.send(res, 400, { error: 'Missing client' }); return null; }
  if (!/^[a-z0-9-]{1,40}$/.test(clientId)) { util.send(res, 400, { error: 'Bad client id' }); return null; }
  if (sess.role !== 'admin' && sess.client !== clientId) {
    util.send(res, 403, { error: 'No access to this client' });
    return null;
  }
  return sess;
}

module.exports = {
  verifyLogin: verifyLogin,
  signSession: signSession,
  sessionCookie: sessionCookie,
  clearCookie: clearCookie,
  getSession: getSession,
  requireClientAccess: requireClientAccess,
  hashPassword: function (pw) { return bcrypt.hashSync(pw, 12); }
};
