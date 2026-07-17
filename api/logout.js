'use strict';

var util = require('./_lib/util');
var auth = require('./_lib/auth');

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return util.send(res, 405, { error: 'Method not allowed' });
  res.setHeader('Set-Cookie', auth.clearCookie(req));
  return util.send(res, 200, { ok: true });
};
