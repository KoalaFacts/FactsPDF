import test from 'node:test';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validateDataset, filterFeatures, counts, safeUrl, parseRoute, routeHash, pageSlice, csv } from '../src/model.mjs';
const dataset = JSON.parse(readFileSync(new URL('../data/compatibility.json', import.meta.url), 'utf8'));

test('418 stable records retain provenance with scoped preview capability updates', () => {
  assert.equal(validateDataset(dataset).features.length, 418);
  assert.equal(dataset.accepted_css_property_count,49);
  const decor=dataset.features.find(f=>f.id==='css.text-decoration');
  assert.equal(decor.status,'partial');
  assert.equal(decor.last_audited_commit,'280212ceea1eb7d0105a5bbb2ee61825364836e8');
  assert.match(decor.scope,/underline.*line-through/);
  assert.match(decor.limitations,/overline/);
  assert.equal(dataset.commit, '69816f7de9112400db4f7ba4902073e5464ac6b1');
  for (const id of ['css.line-height','value.lineheight.other']) {
    const line=dataset.features.find(f=>f.id===id);
    assert.equal(line.status,'partial');
    assert.equal(line.last_audited_commit,'20566ebb8fc2cfea07e61c65df8e01a95b755140');
    assert.equal(line.introduced_release,null);
    assert.match(line.scope,/normal/);
    assert.match(line.limitations,/calc/);
  }
  const fontSize=dataset.features.find(f=>f.id==='css.font-size');
  const relative=dataset.features.find(f=>f.id==='value.length.relative');
  for(const entry of [fontSize,relative]){
    assert.equal(entry.status,'partial');
    assert.equal(entry.last_audited_commit,'cd9355bbf8c85d31a8f5c08ae8e02d89a3ebe345');
    assert.equal(entry.introduced_release,null);
  }
  assert.match(fontSize.scope,/125%/);
  assert.match(fontSize.scope,/em/);
  assert.match(fontSize.limitations,/rem/);
  assert.match(relative.scope,/font-size/);
  assert.match(relative.limitations,/width/);
  const rgb=dataset.features.find(f=>f.id==='value.color.rgbfunc');
  assert.equal(rgb.status,'partial');
  assert.equal(rgb.last_audited_commit,'b35413c2a5a4cdc584d03374c964fc3ed6ccee43');
  assert.match(rgb.scope,/rgba/);
  for(const id of ["css.color","css.background","css.background-color","css.border","css.border-color","css.border-top-color","css.border-right-color","css.border-bottom-color","css.border-left-color","value.color.rgbfunc"]) {
    assert.equal(dataset.features.find(f=>f.id===id).last_audited_commit,'b35413c2a5a4cdc584d03374c964fc3ed6ccee43');
  }

  assert.deepEqual(counts(dataset.features), { full:44, partial:131, none:235, unknown:6, na:2 });
});
test('whole-paragraph nowrap is a limited PDF capability, not CSS Text conformance', () => {
  const feature=dataset.features.find(f=>f.id==='css.white-space');
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'8082791cf059bc10e0c8453156c865170bbed495');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/nowrap/);
  assert.match(feature.scope,/br/);
  assert.match(feature.limitations,/span/);
  assert.match(feature.limitations,/FPDF1302/);
  assert.equal(feature.example,'white-space:nowrap');
  assert.deepEqual(filterFeatures(dataset.features,{q:'white-space',status:'partial'}).map(f=>f.id).sort(),
    ['css.white-space','css.word-spacing']);
});
test('scoped border-box CSS affects report geometry without misreporting tables', () => {
  const feature=dataset.features.find(f=>f.id==='css.box-sizing');
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'28d150e570797e1247fe8fff9a9b030d1724f047');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/border-box/);
  assert.match(feature.scope,/padding/);
  assert.match(feature.limitations,/img/);
  assert.match(feature.limitations,/table/);
  assert.match(feature.limitations,/FPDF1302/);
  assert.equal(feature.example,'box-sizing:border-box;width:140mm;padding:5mm;border:0.25mm solid #23446b');
});
test('print units and bounded four-sided report margins remain scoped', () => {
  const margin=dataset.features.find(f=>f.id==='css.margin');
  const absolute=dataset.features.find(f=>f.id==='value.length.absolute');
  assert.equal(margin.status,'partial');
  assert.match(margin.scope,/margin:0 auto按实际外框居中/);
  assert.match(margin.limitations,/单元格p左右仅显式零值/);
  assert.equal(margin.example,'margin:0 auto');
  assert.equal(absolute.status,'partial');
  assert.match(absolute.scope,/mm\/cm\/in/);
  assert.match(absolute.limitations,/q\/pc/);
  assert.equal(dataset.accepted_css_property_count,49);
  assert.equal(margin.last_audited_commit,'125e7788ac804d9d6960b29717efaf0c151077c0');
  for(const id of ['css.margin-left','css.margin-right']){
    const side=dataset.features.find(f=>f.id===id);
    assert.equal(side.status,'partial');
    assert.match(side.scope,/auto/);
    assert.match(side.scope,/(实际|真实)/);
    assert.match(side.limitations,/表格单元格p/);
    assert.equal(side.introduced_release,null);
  }
  assert.equal(dataset.features.find(f=>f.id==='value.length.relative').status,'partial');
});
test('colour-only background shorthand is discoverable without claiming CSS backgrounds', () => {
  const match=filterFeatures(dataset.features,{q:'background',status:'partial'}).find(f=>f.id==='css.background');
  assert(match);
  assert.match(match.scope,/颜色/);
  assert.match(match.limitations,/url/);
  assert.match(match.limitations,/渐变/);
  assert.equal(match.example,'background:rgb(240 246 250)');
  assert.equal(match.introduced_release,null);
  assert.equal(dataset.features.find(f=>f.id==='css.background-image').status,'none');
});
test('colspan lookup includes the implemented subset without promoting rowspan', () => {
  const span=filterFeatures(dataset.features,{q:'colspan',status:'partial'}).find(f=>f.id==='attr.15');
  assert(span, 'supported-subset search must find the colspan / rowspan record');
  assert.match(span.scope,/colspan/);
  assert.match(span.limitations,/rowspan[^；。]*不支持/);
  assert.equal(filterFeatures(dataset.features,{q:'colspan',status:'full'}).length,0);
});
test('PNG lookup exposes bounded transparency support and unsupported encodings', () => {
  const matches=filterFeatures(dataset.features,{q:'PNG',status:'partial'});
  for(const id of ['html.img','pdf.images']){
    const feature=matches.find(f=>f.id===id);
    assert(feature, `${id} must be discoverable as partial PNG support`);
    assert.match(feature.scope,/PNG/);
  }
  const images=matches.find(f=>f.id==='pdf.images');
  assert.match(images.scope,/tRNS/);
  assert.match(images.scope,/alpha/);
  for(const unsupported of [/16.?bit/,/Adam7/,/APNG/,/SVG/]){
    assert.match(images.limitations,unsupported);
  }
});
test('structured page furniture is discoverable without enabling HTML header/footer tags', () => {
  const margins=filterFeatures(dataset.features,{q:'PageFurniture',status:'partial'}).find(f=>f.id==='pdf.headers');
  assert(margins);
  assert(filterFeatures(dataset.features,{q:'--page-furniture',status:'partial'}).some(f=>f.id==='pdf.headers'));
  for(const supported of [/\{page\}/,/\{pages\}/,/六区/,/FirstPage/]){
    assert.match(margins.scope,supported);
  }
  assert.match(margins.scope,/HeaderText/);
  assert.match(margins.scope,/FooterText/);
  assert.match(margins.scope,/PageNumbers/);
  assert.match(margins.scope,/literal/);
  assert.match(margins.limitations,/互斥/);
  assert.match(margins.limitations,/单行/);
  assert.match(margins.limitations,/@page/);
  assert.match(margins.example,/--page-furniture/);
  for(const id of ['html.header','html.footer']){
    const tag=dataset.features.find(f=>f.id===id);
    assert.equal(tag.status,'none');
    assert.match(tag.limitations,/PageFurniture/);
    assert.match(tag.limitations,/HeaderText\/FooterText/);
  }
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
  assert.equal(filterFeatures(dataset.features,{milestone:'unknown'}).length,364);
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
  assert(output.includes(f.last_audited_commit));
});

// Use a real ICU locale in a child process, not a patched String method.
test('ASCII identifier searches are case-insensitive with Turkish defaults', () => {
  const moduleUrl=new URL('../src/model.mjs',import.meta.url).href;
  const script=`import {filterFeatures} from ${JSON.stringify(moduleUrl)}; console.log(JSON.stringify({locale:Intl.DateTimeFormat().resolvedOptions().locale,hits:filterFeatures([{id:'css.width',feature:'width'}],{q:'WIDTH'}).length}));`;
  const result=JSON.parse(execFileSync(process.execPath,['--input-type=module','-e',script],{encoding:'utf8',env:{...process.env,LANG:'tr_TR.UTF-8',LC_ALL:'tr_TR.UTF-8'}}));
  assert.match(result.locale,/^tr/);
  assert.equal(result.hits,1);
});
test('unknown milestone matches null, omitted and empty values consistently', () => {
  const base={...dataset.features[0]};delete base.introduced_milestone;
  const rows=[base,{...base,introduced_milestone:null},{...base,introduced_milestone:''},{...base,introduced_milestone:'M6'}];
  assert.deepEqual(filterFeatures(rows,{milestone:'unknown'}),rows.slice(0,3));
});
test('literal all query survives route serialization while enum sentinels remain omitted', () => {
  const route={q:'all',category:'all',status:'all',milestone:'all',feature:'',page:1};
  assert.equal(routeHash(route),'#q=all');
  assert.deepEqual(parseRoute(routeHash(route),dataset),route);
});

test('previous justification provenance remains pinned after the one-row case-transform update', () => {
  assert.equal(dataset.dataset_version, 'audit-2026-10-28.text-transform-ascii');
  assert.match(dataset.audit_method, new RegExp(dataset.commit));
  assert.match(dataset.audit_method, /417 other records unchanged/);
  assert.match(dataset.css_inventory_note, /49个CSS属性/);
  assert.match(dataset.css_inventory_note, /text-align:justify/);
  const property=dataset.features.find(x=>x.id==='css.text-align');
  const values=dataset.features.find(x=>x.id==='value.align.justify');
  assert.equal(property.status, 'partial');
  assert.equal(values.status, 'partial');
  assert.equal(property.last_audited_commit,'744366cf5ab206f901bf4ee3b9d014b7bf022e29');
  assert.equal(values.last_audited_commit,'744366cf5ab206f901bf4ee3b9d014b7bf022e29');
  assert.equal(values.introduced_release,null);
  assert.equal(property.example,'text-align:justify');
  assert.equal(values.example,'text-align:justify');
  assert.match(property.scope,/PDF TJ/);
  assert.match(values.scope,/自动换行/);
  assert.match(values.limitations,/start\/end/);
  assert.match(values.limitations,/RTL/);
  assert.match(property.limitations,/FPDF1201/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-align:justify',status:'partial'})
    .map(x=>x.id).sort(), ['css.text-align','value.align.justify']);
});

test('text-indent is a real limited renderable CSS feature with unchanged 417 other records', () => {
  const f=dataset.features.find(f=>f.id==='css.text-indent');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'ef77adbc52eb498d3f53ffac8db7cb3476cdb930');
  assert.equal(f.introduced_release,null);
  assert.match(f.scope,/实际内容宽度/);
  assert.match(f.scope,/PDF/);
  assert.match(f.limitations,/FPDF1302/);
  assert.match(f.limitations,/表格单元格/);
  assert.equal(f.example,'text-indent:0.25in');
  assert.equal(filterFeatures(dataset.features,{q:'text-indent',status:'partial'}).length,1);
});

test('bounded letter-spacing is a genuine partially supported PDF text capability', () => {
  const f=dataset.features.find(entry=>entry.id==='css.letter-spacing');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'10776e9829ed01380562979af311747d17a761fc');
  assert.equal(f.introduced_release,null);
  assert.equal(f.example,'letter-spacing:0.75pt');
  assert.match(f.scope,/TrueType/);
  assert.match(f.scope,/PDF Tc/);
  assert.match(f.scope,/跨页/);
  assert.match(f.limitations,/FPDF1202/);
  assert.match(f.limitations,/复杂字距/);
  assert.equal(dataset.features.find(entry=>entry.id==='css.word-spacing').status,'partial');
  assert.equal(filterFeatures(dataset.features,{q:'letter-spacing',status:'partial'}).length,1);
});

test('word-spacing is measured and painted with explicit PDF limits', () => {
  const f=dataset.features.find(x=>x.id==='css.word-spacing');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'10776e9829ed01380562979af311747d17a761fc');
  assert.equal(f.introduced_release,null);
  assert.equal(f.example,'word-spacing:0.72pt');
  assert.match(f.scope,/TrueType CID/);
  assert.match(f.scope,/PDF TJ/);
  assert.match(f.scope,/跨页/);
  assert.match(f.limitations,/FPDF1202/);
  assert.match(f.limitations,/FPDF1302/);
  assert.equal(filterFeatures(dataset.features,{q:'word-spacing',status:'partial'}).length,1);
});

test('ASCII case transform is a scoped PDF glyph capability, not full Unicode casing', () => {
  const feature=dataset.features.find(x=>x.id==='css.text-transform');
  assert(feature);
  assert.equal(feature.status, 'partial');
  assert.equal(feature.last_audited_commit, dataset.commit);
  assert.equal(feature.introduced_pr, 88);
  assert.equal(feature.introduced_release, null);
  assert.equal(feature.example, 'text-transform:uppercase');
  assert.match(feature.scope, /uppercase\/lowercase/);
  assert.match(feature.scope, /PDF/);
  assert.match(feature.scope, /span/);
  assert.match(feature.limitations, /capitalize/);
  assert.match(feature.limitations, /FPDF1305/);
  assert.match(feature.limitations, /FPDF1504/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-transform',status:'partial'}).map(x=>x.id), ['css.text-transform']);
  assert.equal(dataset.features.length,418);
});
