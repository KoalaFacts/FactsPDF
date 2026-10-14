import assert from 'node:assert/strict';
import { test } from 'node:test';
import { serveStatic } from './static_server.mjs';
import { createConnection } from 'node:net';
import { withDeadline } from './deadline.mjs';

test('only explicit static bytes are served; POST and unknown paths fail', async () => {
  const server = await serveStatic(new Map([['/asset', { bytes: Buffer.from('static'), type: 'text/plain' }]]));
  try {
    assert.equal(await (await fetch(`${server.origin}/asset`)).text(), 'static');
    assert.equal((await fetch(`${server.origin}/asset`, { method: 'POST' })).status, 405);
    assert.equal((await fetch(`${server.origin}/convert`)).status, 404);
    assert.equal((await fetch(`${server.origin}/../../etc/passwd`)).status, 404);
  } finally { await server.close(); }
});

test('cleanup closes a client stalled before completing HTTP headers', async () => {
  const server = await serveStatic(new Map());
  const address = new URL(server.origin);
  const socket = createConnection({ host: address.hostname, port: Number(address.port) });
  await new Promise(resolve => socket.once('connect', resolve));
  socket.write('GET / HTTP/1.1\r\nHost: localhost\r\n');
  try { await withDeadline(server.close(), 250, 'Server cleanup'); }
  finally { socket.destroy(); }
});
