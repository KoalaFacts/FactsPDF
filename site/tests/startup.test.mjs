import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { webcrypto } from 'node:crypto';
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
async function loadSite(t, { data=dataset, url='https://example.test/FactsPDF/', language='zh' } = {}) {
  const dom = new JSDOM('<div id="app"></div>', {
    url, runScripts: 'outside-only'
  });
  t.after(() => dom.window.close());
  const { window } = dom;
  // Existing interaction tests explicitly exercise the saved Chinese locale.
  // The no-preference English production path is tested separately below.
  if(language) window.localStorage.setItem('factspdf.locale', language);
  Object.defineProperty(window.crypto, 'subtle', {value:webcrypto.subtle});
  window.TextEncoder=TextEncoder;
  const errors=[];
  window.console.error=(...args)=>{errors.push(args.map(String).join(' '));console.error(...args.map(String));};
  t.after(()=>assert.deepEqual(errors, [], 'no reactive rendering errors'));
  window.fetch = async () => ({ ok: true, json: async () => data });
  window.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
  window.HTMLDialogElement.prototype.close = function () { this.open = false; };
  window.eval(bundle);
  for(let attempt=0;attempt<100&&!window.document.querySelector('[data-total],[role=alert]');attempt++)
    await new Promise(resolve=>setTimeout(resolve,10));
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
  assert.equal(document.querySelector('.results-heading h2 span').textContent, '79');
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

test('inventory note follows loaded record count in both interface languages', async t => {
  const data=structuredClone(dataset);
  const extra={...data.features[0],id:'test.extra',parent_id:null};
  data.features.push(extra);data.categories[extra.category]++;
  const window=await loadSite(t,{data});
  assert.equal(window.document.querySelector('[data-total]').textContent,'419');
  assert.match(window.document.querySelector('.inventory-note').textContent,/419/);
  window.document.querySelector('.language').click();await settled();
  assert.match(window.document.querySelector('.inventory-note').textContent,/419 is an inventory count/);
});

test('copied feature link matches the displayed canonical URL without borrowed query values', async t => {
  const window=await loadSite(t,{url:'https://example.test/FactsPDF/?tracking=borrowed'});
  let copied;
  Object.defineProperty(window.navigator,'clipboard',{value:{writeText:async value=>{copied=value;}}});
  window.document.querySelector('.feature-button').click();await settled();
  const shown=window.document.querySelector('input[aria-label="Feature link"]').value;
  window.document.querySelector('.share button').click();await settled();
  assert.equal(copied,shown);
  assert.equal(new URL(copied).search,'');
  assert.match(new URL(copied).hash,/feature=/);
});

test('English filter accessible names follow the displayed locale', async t => {
  const window=await loadSite(t);
  window.document.querySelector('.language').click();await settled();
  assert.deepEqual([...window.document.querySelectorAll('.toolbar select')].map(node=>node.getAttribute('aria-label')),
    ['Category filter','Support status filter','Introduction milestone filter']);
});


test('production bundle defaults to English content without a saved preference', async t => {
  const window=await loadSite(t,{language:null});
  const document=window.document;
  assert.equal(document.documentElement.lang,'en');
  assert.match(document.querySelector('#query').placeholder,/Search elements/);
  assert.doesNotMatch(document.querySelector('tbody').textContent,/[\u3400-\u9fff]/u);
  document.querySelector('.feature-button').click();await settled();
  assert.doesNotMatch(document.querySelector('.detail-scope').textContent,/[\u3400-\u9fff]/u);
  assert.doesNotMatch(document.querySelector('.limitation').textContent,/[\u3400-\u9fff]/u);
  assert.match(document.querySelector('.limitation').textContent,/rejection does not count as support/);
});

test('clearing empty results restores table, empty state and pager together', async t => {
  const window=await loadSite(t,{language:null});
  const document=window.document,query=document.querySelector('#query');
  for(let cycle=0;cycle<2;cycle++){
    query.value='zz-no-such-feature-qa';
    query.dispatchEvent(new window.Event('input',{bubbles:true}));await settled();
    assert.equal(document.querySelectorAll('tbody tr').length,0);
    assert.ok(document.querySelector('[data-testid="empty"]'));
    assert.match(document.querySelector('.pagination').textContent,/0 records/);
    document.querySelector('[data-testid="empty"] button').click();await settled();
    assert.equal(query.value,'');assert.equal(window.location.hash,'');
    assert.equal(document.querySelector('.results-heading h2 span').textContent,'418');
    assert.equal(document.querySelectorAll('tbody tr').length,25);
    assert.equal(document.querySelector('[data-testid="empty"]'),null);
    assert.match(document.querySelector('.pagination').textContent,/Page 1 \/ 17 · 418 records/);
    assert.equal(document.querySelector('[aria-label="Next page"]').disabled,false);
    document.querySelector('.reset').click();await settled();
    assert.equal(document.querySelectorAll('tbody tr').length,25);
  }
});

test('cached empty state follows locale and route navigation after recovery', async t => {
  const window=await loadSite(t,{language:null}),document=window.document;
  async function navigate(hash){
    const changed=new Promise(resolve=>window.addEventListener('hashchange',resolve,{once:true}));
    window.location.hash=hash;await changed;await settled();
  }
  await navigate('#q=zz-no-such-feature-qa');
  document.querySelector('.language').click();await settled();
  assert.equal(document.querySelector('.empty h3').textContent,'没有匹配记录');
  document.querySelector('.reset').click();await settled();
  assert.equal(document.querySelector('.empty'),null);
  await navigate('#q=zz-no-such-feature-qa');
  document.querySelector('.language').click();await settled();
  assert.equal(document.querySelector('.empty h3').textContent,'No matching records');
  await navigate('#q=FPDF1102');
  assert.equal(document.querySelector('.empty'),null);
  assert.equal(document.querySelector('.results-heading h2 span').textContent,'79');
  assert.match(document.querySelector('.pagination').textContent,/Page 1 \/ 4 · 79 records/);
  assert.equal(document.querySelectorAll('tbody tr').length,25);
  document.querySelector('[aria-label="Next page"]').click();await settled();
  assert.match(document.querySelector('.pagination').textContent,/Page 2 \/ 4 · 79 records/);
  document.querySelector('.reset').click();await settled();
  assert.match(document.querySelector('.pagination').textContent,/Page 1 \/ 17 · 418 records/);
});
