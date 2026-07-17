'use strict';

var util = require('./_lib/util');
var auth = require('./_lib/auth');

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return util.send(res, 405, { error: 'Method not allowed' });
  if (!util.sameOrigin(req)) return util.send(res, 403, { error: 'Cross-origin request blocked' });
  try {
    var body = await util.readBody(req, 64 * 1024);
    var username = String(body.username || '').trim();
    var password = String(body.password || '');
    if (!username || !password) return util.send(res, 400, { error: 'Enter your username and password.' });

    var result = auth.verifyLogin(req, username, password);
    if (result.error) return util.send(res, result.status, { error: result.error });

    var token = auth.signSession(result.user);
    res.setHeader('Set-Cookie', auth.sessionCookie(token, req));
    return util.send(res, 200, {
      ok: true,
      user: {
        username: result.user.username,
        name: result.user.name || result.user.username,
        client: result.user.client || null,
        role: result.user.role === 'admin' ? 'admin' : 'client'
      }
    });
  } catch (e) {
    return util.send(res, 500, { error: 'Server error: ' + e.message });
  }
};
