import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { createServer } from 'node:http';
import { loadResources } from '../../probes/browser-wasm/wwwroot/resources.mjs';
import * as resources from '../../probes/browser-wasm/wwwroot/resources.mjs';

let server, origin;
const requests = [];
before(async () => {
  server = createServer((req, res) => {
    requests.push(req.url);
    const files = new Map([
      ['/input', Buffer.from('<p>报告 café</p>')], ['/latin', Buffer.from([1, 2])],
      ['/cjk', Buffer.from([3, 4])], ['/empty', Buffer.alloc(0)],
      ['/bad-utf8', Buffer.from([0xc3, 0x28])],
    ]);
    const bytes = files.get(req.url);
    res.writeHead(bytes === undefined ? 404 : 200).end(bytes);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
});
after(async () => { await new Promise(resolve => server.close(resolve)); });

test('fetches exact UTF-8 and ordered explicit font bytes', async () => {
  const loaded = await loadResources(`${origin}/input`, [`${origin}/latin`, `${origin}/cjk`]);
  assert.equal(loaded.html, '<p>报告 café</p>');
  assert.deepEqual(loaded.fonts.map(b => [...b]), [[1, 2], [3, 4]]);
});
test('absent font fails explicitly with no discovery requests', async () => {
  requests.length = 0;
  await assert.rejects(loadResources(`${origin}/input`, [`${origin}/absent`]), { code: 'B2_RESOURCE_HTTP' });
  assert.deepEqual(requests, ['/input', '/absent']);
});
test('empty explicitly selected font cannot become the no-font mode', async () => {
  await assert.rejects(loadResources(`${origin}/input`, [`${origin}/empty`]), { code: 'B2_RESOURCE_EMPTY' });
});
test('invalid UTF-8 input is rejected rather than replaced', async () => {
  await assert.rejects(loadResources(`${origin}/bad-utf8`, []), { code: 'B2_INPUT_UTF8' });
});
test('zero fonts preserves the explicit no-font mode', async () => {
  const loaded = await loadResources(`${origin}/input`, []);
  assert.deepEqual(loaded.fonts, []);
});
test('probe rejects more than two font URLs before any fetch', async () => {
  requests.length = 0;
  await assert.rejects(loadResources(`${origin}/input`, ['a', 'b', 'c']), { code: 'B2_FONT_COUNT' });
  assert.deepEqual(requests, []);
});

test('style resources retain explicit role and chain ordering without pairing', async () => {
  assert.equal(typeof resources.loadStyleResources, 'function');
  const loaded = await resources.loadStyleResources(`${origin}/input`, {
    italic: [`${origin}/cjk`, `${origin}/latin`], bold: [`${origin}/latin`],
  });
  assert.equal(loaded.html, '<p>报告 café</p>');
  assert.deepEqual([...loaded.fontBytes], [1, 2, 3, 4, 1, 2]);
  assert.deepEqual([...loaded.fontLengths], [2, 2, 2]);
  assert.deepEqual([...loaded.fontRoles], [1, 2, 2]);
});

test('invalid style configuration fails before any resource is fetched', async () => {
  assert.equal(typeof resources.loadStyleResources, 'function');
  for (const config of [null, [], { unknown: [] }, { normal: null }, { bold: [''] },
    { normal: ['x', 'x', 'x'], bold: ['x', 'x', 'x'], italic: ['x', 'x', 'x'] }]) {
    requests.length = 0;
    await assert.rejects(resources.loadStyleResources(`${origin}/input`, config), { code: 'B2_INPUT_FONTS' });
    assert.deepEqual(requests, []);
  }
});

test('style resource HTTP and empty errors stay distinct from missing core roles', async () => {
  assert.equal(typeof resources.loadStyleResources, 'function');
  await assert.rejects(resources.loadStyleResources(`${origin}/input`, { bold: [`${origin}/missing`] }), { code: 'B2_RESOURCE_HTTP' });
  await assert.rejects(resources.loadStyleResources(`${origin}/input`, { italic: [`${origin}/empty`] }), { code: 'B2_RESOURCE_EMPTY' });
  const noFonts = await resources.loadStyleResources(`${origin}/input`, {});
  assert.equal(noFonts.fontBytes.length, 0);
});
