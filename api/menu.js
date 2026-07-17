'use strict';

var util = require('./_lib/util');
var auth = require('./_lib/auth');
var store = require('./_lib/store');
var validate = require('./_lib/validate');

module.exports = async function handler(req, res) {
  try {
    if (req.method === 'GET') return await getMenu(req, res);
    if (req.method === 'PUT' || req.method === 'POST') return await putMenu(req, res);
    return util.send(res, 405, { error: 'Method not allowed' });
  } catch (e) {
    return util.send(res, 500, { error: 'Server error: ' + e.message });
  }
};

async function getMenu(req, res) {
  var q = util.getQuery(req);
  var sess = auth.requireClientAccess(req, res, q.client);
  if (!sess) return;
  var file = await store.readFile(q.client + '/menu.json');
  if (!file) return util.send(res, 404, { error: 'No menu.json for this client yet' });
  var menu;
  try { menu = JSON.parse(file.content.toString('utf8')); }
  catch (e) { return util.send(res, 500, { error: 'menu.json in repo is not valid JSON' }); }
  return util.send(res, 200, { ok: true, menu: menu });
}

async function putMenu(req, res) {
  if (!util.sameOrigin(req)) return util.send(res, 403, { error: 'Cross-origin request blocked' });
  var body = await util.readBody(req, 1024 * 1024);
  var clientId = String(body.client || '');
  var sess = auth.requireClientAccess(req, res, clientId);
  if (!sess) return;

  var result = validate.validateMenu(body.menu, clientId);
  if (result.error) return util.send(res, 400, { error: result.error });

  // optimistic concurrency: publish only on top of the version you loaded
  var current = await store.readFile(clientId + '/menu.json');
  var currentVersion = 0, sha = undefined;
  if (current) {
    sha = current.sha === 'local' ? undefined : current.sha;
    try { currentVersion = JSON.parse(current.content.toString('utf8')).version || 0; } catch (e) { currentVersion = 0; }
  }
  var baseVersion = parseInt(body.baseVersion, 10);
  if (!isNaN(baseVersion) && current && baseVersion !== currentVersion) {
    return util.send(res, 409, {
      error: 'The menu changed since you loaded it (someone else published). Reload and re-apply your edits.',
      currentVersion: currentVersion
    });
  }

  var menu = result.menu;
  menu.version = currentVersion + 1;

  var pretty = Buffer.from(JSON.stringify(menu, null, 2) + '\n', 'utf8');
  var message = clientId + ': menu update v' + menu.version + ' by ' + sess.sub + ' (menu editor)';
  await store.writeFile(clientId + '/menu.json', pretty, message, sha);

  return util.send(res, 200, { ok: true, version: menu.version, menu: menu });
}
