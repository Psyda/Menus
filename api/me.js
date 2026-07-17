'use strict';

var util = require('./_lib/util');
var auth = require('./_lib/auth');
var store = require('./_lib/store');

module.exports = async function handler(req, res) {
  if (req.method !== 'GET') return util.send(res, 405, { error: 'Method not allowed' });
  var sess = auth.getSession(req);
  if (!sess) return util.send(res, 401, { error: 'Not signed in' });

  var clients = [];
  try {
    var file = await store.readFile('clients.json');
    if (file) {
      var cfg = JSON.parse(file.content.toString('utf8'));
      clients = (cfg.clients || []).map(function (c) {
        return { id: c.id, name: c.name, screens: c.screens || 3, accent: c.accent || '' };
      });
    }
  } catch (e) { /* clients list is optional */ }

  if (sess.role !== 'admin') {
    clients = clients.filter(function (c) { return c.id === sess.client; });
    if (!clients.length && sess.client) clients = [{ id: sess.client, name: sess.name, screens: 3, accent: '' }];
  }

  return util.send(res, 200, {
    ok: true,
    user: { username: sess.sub, name: sess.name, client: sess.client, role: sess.role },
    clients: clients
  });
};
