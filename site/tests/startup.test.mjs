import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { build } from 'vite';
import { JSDOM } from 'jsdom';
import { filterFeatures } from '../src/model.mjs';

let output, bundle, dataset;
const settled = () => new Promise(resolve => setImmediate(resolve));
before(async () => {
  output = await mkdtemp(join(tmpdir(), 'factspdf-site-test-'));
  await build({ build: { outDir: output, emptyOutDir: true }, logLevel: 'error' });
  dataset = JSON.parse(await readFile(new URL('../data/compatibility.json', import.meta.url)));
  const filename = (await readdir(join(output, 'assets'))).find(name => name.endsWith('.js'));
  bundle = await readFile(join(output, 'assets', filename), 'utf8');
});
after(async () => { if (output) await rm(output, { recursive: true, force: true }); });

// Execute the real production AOT bundle without browser navigation. Layout,
// native dialogs, downloads and Back/Forward remain covered by Playwright in CI.
async function loadSite(t) {
  const dom = new JSDOM('<div id="app"></div>', {
    url: 'https://example.test/FactsPDF/', runScripts: 'outside-only'
  });
  t.after(() => dom.window.close());
  const { window } = dom;
  const errors=[];
  window.console.error=(...args)=>errors.push(args.map(String).join(' '));
  t.after(()=>assert.deepEqual(errors, [], 'no reactive rendering errors'));
  window.fetch = async () => ({ ok: true, json: async () => dataset });
  window.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
  window.HTMLDialogElement.prototype.close = function () { this.open = false; };
  window.eval(bundle);
  await settled();
  return window;
}

test('production template mounts the snapshot and reactive table without a load error', async t => {
  const window = await loadSite(t);
  const document = window.document;
  assert.equal(document.querySelector('[role="alert"]')?.textContent, undefined);
  assert.equal(document.querySelector('[data-total]')?.textContent, '418');
  assert.equal(document.querySelectorAll('tbody tr').length, 25);
  assert.equal(document.querySelector('.table-wrap').firstChild.tagName, 'TABLE');
  const query = document.querySelector('#query');
  query.value = 'FPDF1102';
  query.dispatchEvent(new window.Event('input', { bubbles: true }));
  await settled();
  assert.equal(document.querySelector('.results-heading h2 span').textContent, '98');
  assert.deepEqual([...document.querySelectorAll('tbody .feature-button')].map(node => node.textContent),
    filterFeatures(dataset.features, { q: 'FPDF1102' }).slice(0, 25).map(row => row.feature));
  assert.equal(document.querySelectorAll('tbody tr').length, 25);
});

test('opening details preserves the filtered route as a browser history entry', async t => {
  const window = await loadSite(t);
  const query = window.document.querySelector('#query');
  query.value = 'width';
  query.dispatchEvent(new window.Event('input', { bubbles: true }));
  await settled();
  const length = window.history.length;
  window.document.querySelector('.feature-button').click();
  await settled();
  assert.equal(window.history.length, length + 1);
  assert.match(window.location.hash, /feature=/);
  assert.equal(window.document.querySelector('dialog').open, true);
});

test('source JSON download is reachable outside the mobile-hidden sidebar', async t => {
  const window = await loadSite(t);
  const link = [...window.document.querySelectorAll('a[download="FactsPDF-compatibility.json"]')]
    .find(node => !node.closest('.sidebar'));
  assert.ok(link, 'source JSON download must remain in the main controls');
  assert.equal(link.getAttribute('href'), '/FactsPDF/data/compatibility.json');
});

test('closing a clicked detail returns to its list history entry', async t => {
  const window = await loadSite(t);
  window.document.querySelector('.feature-button').click();
  await settled();
  const featureHash = window.location.hash;
  const changed = new Promise(resolve => window.addEventListener('hashchange', resolve, { once: true }));
  window.document.querySelector('dialog .close').click();
  // replaceState does not navigate, so bound the wait to expose the old bug.
  await Promise.race([changed, new Promise(resolve => setTimeout(resolve, 100))]);
  assert.equal(window.location.hash.includes('feature='), false);
  const forward = new Promise(resolve => window.addEventListener('hashchange', resolve, { once: true }));
  window.history.forward();
  await Promise.race([forward, new Promise(resolve => setTimeout(resolve, 100))]);
  assert.equal(window.location.hash, featureHash, 'Forward must reopen the detail that close navigated back from');
});
