import test from 'node:test';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validateDataset, filterFeatures, counts, safeUrl, parseRoute, routeHash, pageSlice, csv } from '../src/model.mjs';
const dataset = JSON.parse(readFileSync(new URL('../data/compatibility.json', import.meta.url), 'utf8'));
function marginEvidence(id) {
  if(['css.margin','css.margin-top','css.margin-bottom'].includes(id))
    return {sha:'f5aba206ca30c86dca8bd4b465182d5cea436dcc',path:'f5aba206ca30c86dca8bd4b465182d5cea436dcc/tests/FactsPDF.Tests/ReportPanelVerticalMarginTests.cs'};
  if(['css.margin-left','css.margin-right'].includes(id))
    return {sha:'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2',path:'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2/tests/FactsPDF.Tests/ReportPanelMarginTests.cs'};
  return {sha:'b0e6b53dcd003d74998a5a8d955c2071c0faa1c0',
    path:'76d31cc3c7694a966a68dfc3b848e7e75681fc66/tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs'};
}


test('418 stable records retain provenance with scoped preview capability updates', () => {
  assert.equal(validateDataset(dataset).features.length, 418);
  assert.equal(dataset.accepted_css_property_count,67);
  const decor=dataset.features.find(f=>f.id==='css.text-decoration');
  assert.equal(decor.status,'partial');
  assert.equal(decor.last_audited_commit,'280212ceea1eb7d0105a5bbb2ee61825364836e8');
  assert.match(decor.scope,/underline.*line-through/);
  assert.match(decor.limitations,/overline/);
  assert.equal(dataset.commit, 'bea39e6d6a55f8a9e77ef8156bf6ca6529494738');
  assert.match(dataset.commit_url, /bea39e6d6a55f8a9e77ef8156bf6ca6529494738/);
  for (const id of ['css.line-height','value.lineheight.other']) {
    const line=dataset.features.find(f=>f.id===id);
    assert.equal(line.status,'partial');
    assert.equal(line.last_audited_commit,'1aba07ce42ccd8b2ae9eade4a71204818984cb9c');
    assert.equal(line.introduced_release,null);
    assert.match(line.scope,/normal/);
    assert.match(line.limitations,/calc/);
  }
  const fontSize=dataset.features.find(f=>f.id==='css.font-size');
  const relative=dataset.features.find(f=>f.id==='value.length.relative');
  for(const entry of [fontSize,relative]){
    assert.equal(entry.status,'partial');
    assert.equal(entry.introduced_release,null);
  }
  assert.equal(fontSize.last_audited_commit,'f14550afd22ca40569998398b9fb8595d9f0e509');
  assert.equal(relative.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353');
  assert.match(fontSize.scope,/rem/);
  assert.match(fontSize.scope,/em/);
  assert.match(fontSize.limitations,/rem/);
  assert.match(relative.scope,/font-size/);
  assert.match(relative.scope,/rem/);
  assert.match(dataset.features.find(f=>f.id==='css.line-height').scope,/rem/);
  assert.match(dataset.features.find(f=>f.id==='value.lineheight.other').scope,/rem/);
  assert.match(relative.limitations,/img\/table/);
  const rgb=dataset.features.find(f=>f.id==='value.color.rgbfunc');
  assert.equal(rgb.status,'partial');
  assert.equal(rgb.last_audited_commit,'b35413c2a5a4cdc584d03374c964fc3ed6ccee43');
  assert.match(rgb.scope,/rgba/);
  for(const id of ["css.color","css.background","css.background-color","css.border","css.border-color","css.border-top-color","css.border-right-color","css.border-bottom-color","css.border-left-color","value.color.rgbfunc"]) {
    assert.equal(dataset.features.find(f=>f.id===id).last_audited_commit,'b35413c2a5a4cdc584d03374c964fc3ed6ccee43');
  }

  assert.deepEqual(counts(dataset.features), { full:44, partial:143, none:223, unknown:6, na:2 });
});
test('standalone horizontal rules are real scoped PDF separators, not full HTML/CSS', () => {
  const rule=dataset.features.find(f=>f.id==='html.hr');
  assert.equal(dataset.accepted_html_tag_count,36);
  assert.equal(dataset.accepted_css_property_count,67);
  assert.equal(rule.status,'partial');
  assert.equal(rule.last_audited_commit,'8c1d475e92902572bf38d53cd2f7d0d5c48d2eca');
  assert.equal(rule.introduced_release,null);
  assert.match(rule.scope,/1pt.*currentcolor/);
  assert.match(rule.scope,/PDF/);
  assert.match(rule.limitations,/FPDF1101\/1103\/1201\/1202\/1302/);
  assert.match(rule.failure_code,/FPDF1302/);
  assert.match(rule.limitations,/表格/);
  assert.match(rule.example,/<hr style=/);
  assert.match(rule.test_reference,/HorizontalRuleTests.cs/);
});
test('bounded report overflow-wrap audit has real scope and exact source evidence', () => {
  const f=dataset.features.find(x=>x.id==='css.overflow-wrap');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'052f7a8d3da5f87d9dfb6c7b687dd35b1eb97097');
  assert.equal(f.introduced_release,null);
  assert.match(f.scope,/break-word/);
  assert.match(f.scope,/PDF/);
  assert.match(f.limitations,/nowrap/);
  assert.match(f.limitations,/FPDF1302/);
  assert.match(f.limitations,/anywhere/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'overflow-wrap',status:'partial'}).map(x=>x.id),['css.overflow-wrap']);
  assert.equal(dataset.accepted_css_property_count,67);
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
  assert.equal(feature.last_audited_commit,'bc704aed90b1e5dbdc1deec65a7fd314b626467e');
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
  assert.match(margin.scope,/margin:0 auto/);
  assert.match(margin.limitations,/单元格p左右仍仅显式零值/);
  assert.equal(margin.example,'margin:0 auto');
  assert.equal(absolute.status,'partial');
  assert.match(absolute.scope,/mm\/cm\/in/);
  assert.match(absolute.limitations,/q\/pc/);
  assert.equal(dataset.accepted_css_property_count,67);
  assert.equal(margin.last_audited_commit,'f5aba206ca30c86dca8bd4b465182d5cea436dcc');
  for(const id of ['css.margin-left','css.margin-right']){
    const side=dataset.features.find(f=>f.id===id);
    assert.equal(side.status,'partial');
    assert.match(side.scope,/auto/);
    assert.match(side.scope,/(实际|真实)/);
    assert.match(side.limitations,/表格单元格p/);
    assert.equal(side.introduced_release,null);
    assert.equal(side.last_audited_commit,'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2');
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
  assert.equal(filterFeatures(dataset.features,{milestone:'unknown'}).length,363);
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

test('historical justification and case-transform provenance remain pinned after root-relative audit', () => {
  assert.equal(dataset.dataset_version, 'audit-2026-11-01.vector-markers');
  assert.match(dataset.audit_method, new RegExp(dataset.commit));
  assert.match(dataset.audit_method, /413 other stable-ID records remain unchanged/);
  assert.match(dataset.css_inventory_note, /66个CSS属性/);
  assert.doesNotMatch(dataset.css_inventory_note, /letter-spacing\/word-spacing仍拒绝/);
  assert.match(dataset.css_inventory_note, /text-indent.*letter-spacing\/word-spacing.*em\/rem/);
  assert.match(dataset.css_inventory_note, /24pt/);
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

test('text-indent remains a limited renderable CSS feature after relative audit', () => {
  const f=dataset.features.find(f=>f.id==='css.text-indent');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353');
  assert.equal(f.introduced_release,null);
  assert.match(f.scope,/实际内容宽度/);
  assert.match(f.scope,/PDF/);
  assert.match(f.limitations,/FPDF1302/);
  assert.match(f.limitations,/表格单元格/);
  assert.equal(f.example,'text-indent:0.25in');
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-indent',status:'partial'})
    .map(f=>f.id).sort(), ['css.text-indent','value.length.relative']);
});

test('bounded letter-spacing is a genuine partially supported PDF text capability', () => {
  const f=dataset.features.find(entry=>entry.id==='css.letter-spacing');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353');
  assert.equal(f.introduced_release,null);
  assert.equal(f.example,'letter-spacing:0.75pt');
  assert.match(f.scope,/TrueType/);
  assert.match(f.scope,/PDF Tc/);
  assert.match(f.scope,/跨页/);
  assert.match(f.limitations,/FPDF1202/);
  assert.match(f.limitations,/复杂字距/);
  assert.equal(dataset.features.find(entry=>entry.id==='css.word-spacing').status,'partial');
  assert.deepEqual(filterFeatures(dataset.features,{q:'letter-spacing',status:'partial'}).map(f=>f.id).sort(),
    ['css.letter-spacing','value.length.relative']);
});

test('word-spacing is measured and painted with explicit PDF limits', () => {
  const f=dataset.features.find(x=>x.id==='css.word-spacing');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353');
  assert.equal(f.introduced_release,null);
  assert.equal(f.example,'word-spacing:0.72pt');
  assert.match(f.scope,/TrueType CID/);
  assert.match(f.scope,/PDF TJ/);
  assert.match(f.scope,/跨页/);
  assert.match(f.limitations,/FPDF1202/);
  assert.match(f.limitations,/FPDF1302/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'word-spacing',status:'partial'}).map(f=>f.id).sort(),
    ['css.word-spacing','value.length.relative']);
});

test('font-relative CSS text spacing remains a narrow audited renderer subset', () => {
  for(const id of ['css.text-indent','css.letter-spacing','css.word-spacing','value.length.relative']){
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353',id);
    assert.match(feature.scope,/em/,id);
    assert.match(feature.scope,/rem/,id);
    assert.equal(feature.introduced_release,null,id);
    assert.equal(feature.test_reference,
      'https://github.com/KoalaFacts/FactsPDF/blob/702b907c382b101bdfdc35c8383185ad9d30283f/tests/FactsPDF.Tests/FontRelativeTextSpacingTests.cs');
  }
  assert.match(dataset.features.find(f=>f.id==='css.letter-spacing').limitations,/FPDF1202/);
  assert.match(dataset.features.find(f=>f.id==='css.word-spacing').limitations,/FPDF1302/);
  assert.equal(dataset.features.length,418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
});

test('ASCII case transform is a scoped PDF glyph capability, not full Unicode casing', () => {
  const feature=dataset.features.find(x=>x.id==='css.text-transform');
  assert(feature);
  assert.equal(feature.status, 'partial');
  assert.equal(feature.last_audited_commit, '06d0be5aab6fcdf15e20f8f1e6b7432e27a4ed35');
  assert.equal(feature.introduced_pr, 88);
  assert.equal(feature.introduced_release, null);
  assert.equal(feature.example, 'text-transform:capitalize');
  assert.match(feature.scope, /uppercase\/lowercase/);
  assert.match(feature.scope, /PDF/);
  assert.match(feature.scope, /span/);
  assert.match(feature.scope, /capitalize/);
  assert.match(feature.limitations, /Unicode/);
  assert.match(feature.limitations, /FPDF1305/);
  assert.match(feature.limitations, /FPDF1504/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-transform',status:'partial'}).map(x=>x.id), ['css.text-transform']);
  assert.equal(dataset.features.length,418);
});

test('bounded report min/max widths are rendered, sourced and discoverable without blanket CSS Sizing claims', () => {
  const min=dataset.features.find(f=>f.id==='css.min-width');
  const max=dataset.features.find(f=>f.id==='css.max-width');
  for (const f of [min,max]) {
    assert.equal(f.status,'partial');
    assert.equal(f.last_audited_commit,'b0e6b53dcd003d74998a5a8d955c2071c0faa1c0');
    assert.equal(f.introduced_release,null);
    assert.match(f.scope,/width/);
    assert.match(f.limitations,/calc/);
    assert.match(f.limitations,/FPDF1302/);
  }
  assert.match(min.scope,/min超过max/);
  assert.match(max.scope,/margin:0 auto/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'min-width',status:'partial'}).filter(f=>f.id==='css.min-width').map(f=>f.id),['css.min-width']);
  assert.deepEqual(filterFeatures(dataset.features,{q:'max-width',status:'partial'}).filter(f=>f.id==='css.max-width').map(f=>f.id),['css.max-width']);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,67);
});

test('root-rem and own-font em box lengths are bounded, evidence-backed partial CSS subsets', () => {
  for(const id of ['css.width','css.min-width','css.max-width','css.padding',
    'css.padding-top','css.padding-right','css.padding-bottom','css.padding-left']){
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit,'b0e6b53dcd003d74998a5a8d955c2071c0faa1c0',id);
    assert.equal(feature.introduced_release,null,id);
    assert.match(feature.scope,/rem/,id);
    assert.match(feature.limitations,/table|表格/,id);
    assert.equal(feature.test_reference,'https://github.com/KoalaFacts/FactsPDF/blob/76d31cc3c7694a966a68dfc3b848e7e75681fc66/tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs');
  }
  assert.match(dataset.features.find(f=>f.id==='css.width').limitations,/col/);
  assert.match(dataset.features.find(f=>f.id==='css.padding').limitations,/FPDF1202/);
  assert.match(dataset.features.find(f=>f.id==='css.font-size').limitations,/margin/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,67);
  assert.deepEqual(filterFeatures(dataset.features,{q:'width rem',status:'partial'}).filter(f=>f.id==='css.width').map(f=>f.id),['css.width']);
});

test('bounded rem/em report margins are actual source-bound partial CSS abilities', () => {
  const revised=['css.margin','css.margin-top','css.margin-bottom','css.margin-left',
    'css.margin-right'];
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,67);
  assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
  for(const id of revised) {
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit, marginEvidence(id).sha, id);
    assert.equal(feature.introduced_release,null,id);
    assert.match(feature.scope+' '+feature.limitations,/rem/,id);
    assert.equal(feature.test_reference,
      'https://github.com/KoalaFacts/FactsPDF/blob/' + marginEvidence(id).path);
  }
  assert.match(dataset.features.find(f=>f.id==='css.margin').scope,/margin:0 auto/);
  assert.match(dataset.features.find(f=>f.id==='css.margin').scope,/margin:12pt auto 7pt/);
  for(const id of ['css.margin-top','css.margin-bottom']) {
    const row=dataset.features.find(f=>f.id===id);
    assert.match(row.scope,/div\/section\/article/);
    assert.match(row.limitations,/margin collapse/);
    assert.match(row.limitations,/FPDF1302/);
  }
  assert.match(dataset.features.find(f=>f.id==='css.margin').limitations,/单元格p左右仍仅显式零值/);
  assert.match(dataset.features.find(f=>f.id==='value.length.relative').limitations,/img\/table/);
  for(const id of ['css.margin-top','css.margin-bottom'])
    assert.match(dataset.features.find(f=>f.id===id).limitations,/auto/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'margin rem',status:'partial'})
    .filter(f=>f.id==='css.margin').map(f=>f.id),['css.margin']);
});

test('element em geometric scope is audited without weakening rejection',()=>{
  for(const id of ["css.width","css.min-width","css.max-width","css.padding","css.padding-top","css.padding-right","css.padding-bottom","css.padding-left","css.margin","css.margin-top","css.margin-bottom","css.margin-left","css.margin-right"]){
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit, marginEvidence(id).sha, id);
    assert.equal(feature.test_reference,'https://github.com/KoalaFacts/FactsPDF/blob/' +
      marginEvidence(id).path,id);
    assert.match(feature.scope,/em/,id);
    assert.equal(feature.introduced_release,null,id);
  }
  assert.equal(dataset.features.find(f=>f.id==='css.font-size').last_audited_commit,
    'f14550afd22ca40569998398b9fb8595d9f0e509');
  assert.match(dataset.features.find(f=>f.id==='css.padding').limitations,/表格/);
  assert.match(dataset.features.find(f=>f.id==='css.margin').limitations,/单元格p左右仍仅显式零值/);
  assert.match(dataset.features.find(f=>f.id==='value.length.relative').limitations,/letter-spacing\/word-spacing/);
  assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
  assert.equal(dataset.features.length,418);
});

test('numeric hundred-step report font weights use the explicit source-audited role scope', () => {
  const row=dataset.features.find(x=>x.id==='css.font-weight');
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/100–900/);
  assert.match(row.scope,/100–500/);
  assert.match(row.scope,/600–900/);
  assert.match(row.limitations,/FPDF1507/);
  assert.match(row.limitations,/bolder\/lighter/);
  assert.match(dataset.audit_method,/417 full feature records preserve/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,67);
});

test('four grouped LTR logical spacing records include eight side longhands', () => {
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,67);
  for (const name of ['margin-inline','margin-block','padding-inline','padding-block']) {
    const row=dataset.features.find(f=>f.id==='css.'+name);
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit,'03c17c1f4f4b3eff556cb7353f356ca70439f59a');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope+' '+row.limitations,/LTR|RTL/);
    assert.match(row.scope,/PDF/);
    assert.match(row.limitations,/FPDF1302/);
    assert.match(row.test_reference,/LogicalReportLonghandTests.cs/);
    for (const side of [name+'-start', name+'-end']) assert.match(row.scope, new RegExp(side));
    assert.ok(row.sources.some(source => source.includes('/src/FactsPDF/Css/CssSyntaxAdapter.cs')));
  }
});


test('bounded quoted report panels have one source-bound HTML feature and real PDF evidence', () => {
  const row=dataset.features.find(f=>f.id==='html.blockquote');
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_html_tag_count,36);
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'c180ec615123717e0e665e63b84ba8ae8acd48d7');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/PDF/);
  assert.match(row.scope,/8pt/);
  assert.match(row.scope,/24pt/);
  assert.match(row.limitations,/FPDF1101/);
  assert.match(row.limitations,/cite/);
  assert.match(row.test_reference,/BlockquoteReportTests.cs/);
});


test('scoped display suppression is backed by actual source and retains the 418-row audit', () => {
  const f = dataset.features.find(row => row.id === 'css.display');
  assert.equal(f.status, 'partial');
  assert.equal(f.last_audited_commit, '9762e45dac1907eb77234132c2e14c348aa55264');
  assert.match(f.scope, /display:none/);
  assert.match(f.scope, /分页/);
  assert.match(f.limitations, /flex\/grid/);
  assert.match(f.limitations, /严格HTML\/CSS/);
  assert.match(f.test_reference, /ReportDisplayTests\.cs/);
  assert.equal(dataset.accepted_css_property_count, 66);
  assert.equal(dataset.features.length, 418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
});

test('bounded list marker audit retains actual PDF scope and 418 provenance rows',()=>{
  const line=dataset.features.find(f=>f.id==='css.list-style-type');
  assert.equal(line.status,'partial');
  assert.equal(line.last_audited_commit,'7665f2fbafa34e14f9d792e7e7728d67f268e0e3');
  assert.match(line.scope,/circle/);
  assert.match(line.scope,/square/);
  assert.doesNotMatch(line.limitations,/circle\/square/);
  assert.equal(line.introduced_release,null);
  assert.equal(line.introduced_pr,110);
  assert.match(line.scope,/lower-roman/);
  assert.match(line.limitations,/3999/);
  assert.match(line.test_reference,/ListMarkerStyleTests/);
  for(const id of ['html.ol','html.ul','html.li']){
    const html=dataset.features.find(f=>f.id===id);
    assert.equal(html.last_audited_commit,'7665f2fbafa34e14f9d792e7e7728d67f268e0e3');
    assert.match(html.scope,/list-style-type/);
  }
  assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
  assert.equal(dataset.accepted_css_property_count,67);
});

test('source-bounded ordered-list numbering and one-token shorthand evidence',()=>{
 const style=dataset.features.find(x=>x.id==='css.list-style');
 assert.equal(style.status,'partial');
 assert.equal(style.last_audited_commit,'bea39e6d6a55f8a9e77ef8156bf6ca6529494738');
 assert.match(style.limitations,/inside/);
 for (const id of ['html.ol','html.ul','html.li']){
  const entry=dataset.features.find(x=>x.id===id);
  assert.equal(entry.last_audited_commit,'bea39e6d6a55f8a9e77ef8156bf6ca6529494738');
  assert.match(entry.scope,/start\/reversed/);
  assert.equal(entry.introduced_release,null);
 }
 assert.deepEqual(counts(dataset.features),{full:44,partial:143,none:223,unknown:6,na:2});
});
