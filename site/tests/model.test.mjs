import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validateDataset, filterFeatures, counts, safeUrl, parseRoute, routeHash, pageSlice, csv } from '../src/model.mjs';
const dataset = JSON.parse(readFileSync(new URL('../data/compatibility.json', import.meta.url), 'utf8'));

test('all original 418 records and snapshot metadata remain intact', () => {
  assert.equal(validateDataset(dataset).features.length, 418);
  assert.equal(dataset.commit, 'e35722bf299adae3a994593f1375ee43ec53ad12');
  assert.deepEqual(counts(dataset.features), { full:44, partial:87, none:278, unknown:7, na:2 });
});
test('missing evidence, unknown status, duplicate IDs, bad parents and categories are rejected', () => {
  const mutators = [d=>d.features[0].status='planned', d=>d.features[1].id=d.features[0].id,
    d=>d.features[0].parent_id='missing', d=>d.features[0].sources=[], d=>d.categories['HTML元素']++,
    d=>d.features[0].sources=['javascript:alert(1)'], d=>d.features[0].last_audited_commit='not-sha'];
  for (const mutate of mutators) { const d=structuredClone(dataset); mutate(d); assert.throws(()=>validateDataset(d)); }
});
test('search matches literal property and Chinese limitations without regex execution', () => {
  assert(filterFeatures(dataset.features,{q:'width'}).some(f=>f.id==='css.width'));
  assert(filterFeatures(dataset.features,{q:'中文'}).length > 0);
  assert.equal(filterFeatures(dataset.features,{q:'[a+*'}).length,0);
  assert(filterFeatures(dataset.features,{q:'BORDER-STYLE'}).some(f=>f.id==='css.border-style'));
});
test('category / status / known milestone combine; plans never become supported', () => {
  const result=filterFeatures(dataset.features,{category:'CSS属性',status:'partial',milestone:'M6'});
  assert(result.length > 0);
  assert(result.every(f=>f.category==='CSS属性'&&f.status==='partial'&&f.introduced_milestone==='M6'));
  assert.equal(filterFeatures(dataset.features,{category:'HTML元素',status:'full'}).length,0);
  assert.equal(filterFeatures(dataset.features,{milestone:'unknown'}).length,377);
});
test('source links permit HTTPS only without credentials', () => {
  assert.equal(safeUrl('javascript:alert(1)'),null);
  assert.equal(safeUrl('data:text/html,<script>'),null);
  assert.equal(safeUrl('//example.com'),null);
  assert.equal(safeUrl('https://a:b@example.com'),null);
  assert.equal(safeUrl('https://github.com/KoalaFacts/FactsPDF'), 'https://github.com/KoalaFacts/FactsPDF');
});
test('shareable URL is roundtrippable; unknown options normalize safely', () => {
  const s={q:'颜色 & width',category:'CSS属性',status:'partial',milestone:'M6',feature:'css.width',page:2};
  assert.deepEqual(parseRoute(routeHash(s),dataset),s);
  const bad=parseRoute('#status=green&category=bad&milestone=v9&feature=evil&page=-2',dataset);
  assert.equal(bad.status,'all'); assert.equal(bad.category,'all'); assert.equal(bad.page,1);
  assert.equal(bad.milestone,'all'); assert.equal(bad.feature,'');
});
test('pagination clamps invalid positions and returns stable original records', () => {
  const p=pageSlice(dataset.features,999,25);
  assert.equal(p.page,17); assert.equal(p.totalPages,17); assert.equal(p.items.length,18);
  const empty=pageSlice([],5,25);assert.deepEqual(empty,{page:1,totalPages:1,items:[]});
});
test('CSV neutralizes spreadsheet formulas and quotes multi-line values', () => {
  const f={...dataset.features[0], feature:'=HYPERLINK("bad")',scope:'a,b\nc'};
  const output=csv([f]);
  assert(output.includes("'=HYPERLINK"));assert(output.includes('"a,b\nc"'));
  assert(output.includes(dataset.commit));
});
