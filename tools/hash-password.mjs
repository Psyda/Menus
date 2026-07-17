#!/usr/bin/env node
/* Generate a bcrypt hash for the MENUS_USERS env var.

   Usage:  npm run hash-password            (prompts, input hidden)
           node tools/hash-password.mjs 'the-password'   (quick, but lands in shell history — avoid for real passwords)
*/
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const bcrypt = require('bcryptjs');

function output(pw) {
  if (!pw || pw.length < 8) {
    console.error('Use at least 8 characters (12+ random recommended — these accounts can edit live menus).');
    process.exit(1);
  }
  const hash = bcrypt.hashSync(pw, 12);
  console.log('\nbcrypt hash:\n' + hash);
  console.log('\nUser entry for MENUS_USERS (fill in username/client/name):');
  console.log(JSON.stringify({ username: 'USERNAME', hash: hash, client: 'CLIENT_ID', name: 'Display Name', role: 'client' }));
}

const arg = process.argv[2];
if (arg) {
  output(arg);
} else {
  const stdin = process.stdin;
  process.stdout.write('Password (hidden): ');
  if (stdin.setRawMode) stdin.setRawMode(true);
  let pw = '';
  stdin.on('data', (ch) => {
    const c = ch.toString('utf8');
    if (c === '\r' || c === '\n' || c === '\u0004') {        // enter / EOF
      if (stdin.setRawMode) stdin.setRawMode(false);
      process.stdout.write('\n');
      output(pw);
      process.exit(0);
    } else if (c === '\u0003') {                              // ctrl-c
      process.exit(1);
    } else if (c === '\u007f' || c === '\b') {                // backspace
      pw = pw.slice(0, -1);
    } else {
      pw += c;
    }
  });
}
