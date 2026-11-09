import test from 'node:test';
import { englishAudit } from '../src/audit-english-cell-text-flow.mjs';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validateDataset, filterFeatures, counts, safeUrl, parseRoute, routeHash, pageSlice, csv } from '../src/model.mjs';
const dataset = JSON.parse(readFileSync(new URL('../data/compatibility.json', import.meta.url), 'utf8'));
function marginEvidence(id) {
  if(['css.margin','css.margin-top','css.margin-bottom'].includes(id))
    return {sha:'0f6c58655d651ee934cfb2c95272dde54f4c477e',path:'0f6c58655d651ee934cfb2c95272dde54f4c477e/tests/FactsPDF.Tests/ParagraphPercentageVerticalMarginTests.cs'};
  if(['css.margin-left','css.margin-right'].includes(id))
    return {sha:'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2',path:'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2/tests/FactsPDF.Tests/ReportPanelMarginTests.cs'};
  return {sha:'b0e6b53dcd003d74998a5a8d955c2071c0faa1c0',
    path:'76d31cc3c7694a966a68dfc3b848e7e75681fc66/tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs'};
}


test('bounded clickable report links have real source provenance without claiming HTML anchor conformance', () => {
  const row=dataset.features.find(x=>x.id==='html.a');
  assert.equal(row.status,'partial');
  assert.equal(dataset.accepted_html_tag_count,38);
  assert.equal(row.last_audited_commit,'9a19096a69f323bda3c7678c590a96b19ad98864');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/PDF \/Link/);
  assert.match(row.limitations,/HTTPS/);
  assert.match(row.limitations,/2048/);
  assert.match(row.failure_code,/FPDF1103/);
  assert.match(row.test_reference,/ReportHyperlinksTests.cs/);
  assert.ok(row.sources.some(url=>url.includes('PdfLinkAnnotations.cs')));
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});

test('five scoped HTML change marks have real source provenance without claiming redaction', () => {
  const ids=['html.small','html.u','html.s','html.del','html.ins'];
  const rows=ids.map(id=>dataset.features.find(feature=>feature.id===id));
  assert.ok(rows.every(Boolean), 'All five existing HTML IDs remain present');
  for (const row of rows) {
    assert.equal(row.status, 'partial', row.id);
    assert.equal(row.last_audited_commit, 'a1300f3943efcf92426836b963e72d28b078e192');
    assert.equal(row.introduced_release, null, 'Source-only support must not imply a released version');
    assert.match(row.scope, /PDF/);
    assert.match(row.limitations, /datetime\/cite/);
    assert.match(row.limitations, /非脱敏/);
    assert.ok(row.test_reference.endsWith('/tests/FactsPDF.Tests/SemanticInlineReportTests.cs'));
  }
  assert.deepEqual(counts(dataset.features), { full:44, partial:164,none:202, unknown:6, na:2 });
});

test('418 stable records retain provenance with scoped preview capability updates', () => {
  assert.equal(validateDataset(dataset).features.length, 418);
  assert.equal(dataset.accepted_css_property_count,116);
  const decor=dataset.features.find(f=>f.id==='css.text-decoration');
  assert.equal(decor.status,'partial');
  assert.equal(decor.last_audited_commit,'2d18399e25c65267431f0decc7fe716dde82688a');
  assert.match(decor.scope,/underline.*line-through/);
  assert.match(decor.scope,/overline.*text-decoration-color/);
  assert.match(decor.limitations,/double\/wavy/);
  assert.equal(dataset.commit, '5c45a68546d0d903eccd2579782e55f2fedff5e4');
  assert.match(dataset.commit_url, /5c45a68546d0d903eccd2579782e55f2fedff5e4/);
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
  assert.equal(fontSize.last_audited_commit,'b95f1bd3c5d8275bff21f399c9cf59a2d413c024');
  assert.match(fontSize.scope,/smaller\/larger/);
  assert.match(fontSize.test_reference,/FontSizeKeywordTests.cs/);
  assert.equal(relative.last_audited_commit,'92e2b02c12740cd85f244bf04e57c70e89e5c353');
  assert.match(fontSize.scope,/rem/);
  assert.match(fontSize.scope,/em/);
  assert.match(fontSize.limitations,/rem/);
  assert.match(relative.scope,/font-size/);
  assert.match(relative.scope,/rem/);
  assert.match(dataset.features.find(f=>f.id==='css.line-height').scope,/rem/);
  assert.match(dataset.features.find(f=>f.id==='value.lineheight.other').scope,/rem/);
  assert.match(relative.limitations,/table\/col/);
  assert.match(relative.limitations,/img现有受限em\/rem/);
  const rgb=dataset.features.find(f=>f.id==='value.color.rgbfunc');
  assert.equal(rgb.status,'partial');
  assert.equal(rgb.last_audited_commit,'6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d');
  assert.match(rgb.scope,/rgba/);
  for(const id of ["css.color","css.background","css.background-color","css.border","css.border-color","css.border-top-color","css.border-right-color","css.border-bottom-color","css.border-left-color"]) {
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.last_audited_commit, id==='css.border' ? 'd4584c1f32d6aaa35939ff22f14a11093e92945b' : id==='css.text-decoration' ? 'b8f9b938ec42b108fb6f325715a4dd9b692e027d' : ['css.color','css.background','css.background-color'].includes(id) ? '3dbcded4f5d39b16b79bbff7aac4132305659d4a' : 'c9138808d785986712f17e977fa2371854cb6aed');
    assert.match(feature.scope,/hsl\(/);
    assert.match(feature.limitations,/alpha/);
    assert.match(feature.test_reference, id==='css.border' ? /LogicalBorderComponentTests/ : ['css.color','css.background','css.background-color'].includes(id) ? /CurrentColorReportTests/ : /CssNamedColorTests/);
    assert.equal(feature.introduced_release,null);
  }
  assert.equal(rgb.last_audited_commit,'6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d');
  for (const id of ['css.color','css.background-color','css.background']) {
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.last_audited_commit,'3dbcded4f5d39b16b79bbff7aac4132305659d4a');
    assert.match(feature.scope,/currentColor/);
    assert.equal(feature.introduced_release,null);
    assert.match(feature.test_reference,/CurrentColorReportTests.cs/);
  }
  assert.match(dataset.features.find(f=>f.id==='css.background-color').limitations,/currentColor/);
  assert.match(dataset.audit_method,/All other 415 stable records/);

  assert.deepEqual(counts(dataset.features), { full:44, partial:164,none:202, unknown:6, na:2 });
});
test('standalone horizontal rules are real scoped PDF separators, not full HTML/CSS', () => {
  const rule=dataset.features.find(f=>f.id==='html.hr');
  assert.equal(dataset.accepted_html_tag_count,38);
  assert.equal(dataset.accepted_css_property_count,116);
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
  assert.equal(f.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60');
  assert.equal(f.introduced_release,null);
  assert.match(f.scope,/break-word/);
  assert.match(f.scope,/PDF/);
  assert.match(f.limitations,/nowrap/);
  assert.match(f.limitations,/FPDF1302/);
  assert.match(f.limitations,/anywhere/);
  assert.match(f.scope,/html\/body.*固定列.*td\/th/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'overflow-wrap',status:'partial'}).map(x=>x.id),['css.overflow-wrap']);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('whole-paragraph nowrap is a limited PDF capability, not CSS Text conformance', () => {
  const feature=dataset.features.find(f=>f.id==='css.white-space');
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/nowrap/);
  assert.match(feature.sources.join(' '),/488691c604e43c5bd1cf8c2b444e0d7cfb388699/);
  assert.match(feature.limitations,/span/);
  assert.match(feature.limitations,/FPDF1302/);
  assert.equal(feature.example,'white-space:pre-wrap');
  assert.match(feature.scope,/normal.*nowrap.*pre-line.*pre-wrap/);
  assert.match(feature.scope,/html\/body.*固定列.*td\/th/);
  assert.match(feature.limitations,/制表符/);
  assert.match(feature.limitations,/FPDF1305/);
  assert.match(feature.limitations,/FPDF1201/);
  assert.match(feature.limitations,/FPDF1202/);
  assert.match(feature.test_reference,/TableCellTextFlowTests\.cs/);
  assert.match(feature.evidence_level,/继承白空格模式.*独立 p PDF 等价对照/,
    'The inherited report white-space audit must retain explicit positive actual PDF equivalence evidence.');
  assert.deepEqual(filterFeatures(dataset.features,{q:'white-space',status:'partial'}).map(f=>f.id).sort(),
    ['css.white-space','css.word-spacing','html.pre']);
});
test('scoped border-box CSS affects report geometry without misreporting tables', () => {
  const feature=dataset.features.find(f=>f.id==='css.box-sizing');
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/border-box/);
  assert.match(feature.scope,/padding/);
  assert.match(feature.scope,/img/);
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
  assert.match(margin.limitations,/table-cell p左右仍仅显式零值/);
  assert.equal(margin.example,'margin:0 auto');
  assert.equal(absolute.status,'partial');
  assert.match(absolute.scope,/mm\/cm\/in/);
  assert.match(absolute.limitations,/pc\/q 已在上述受限CSS物理长度中支持，但CLI页边距等其他入口仍不扩展/);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.equal(margin.last_audited_commit,'0f6c58655d651ee934cfb2c95272dde54f4c477e');
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
  assert.equal(filterFeatures(dataset.features,{milestone:'unknown'}).length,361);
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

test('bounded LTR alignment provenance and historical justification remain precise', () => {
  assert.equal(dataset.dataset_version, 'audit-2026-11-09.fixed-track-relative-widths-candidate');
  assert.equal(dataset.commit_url, `https://github.com/KoalaFacts/FactsPDF/commit/${dataset.commit}`);
  assert.match(dataset.audit_method, /413 other stable-ID records remain unchanged/);
  assert.match(dataset.css_inventory_note, /现累计81个受限CSS属性名/);
  assert.doesNotMatch(dataset.css_inventory_note, /letter-spacing\/word-spacing仍拒绝/);
  assert.match(dataset.css_inventory_note, /text-indent.*letter-spacing\/word-spacing.*em\/rem/);
  assert.match(dataset.css_inventory_note, /24pt/);
  assert.match(dataset.css_inventory_note, /text-align:justify/);
  const property=dataset.features.find(x=>x.id==='css.text-align');
  const values=dataset.features.find(x=>x.id==='value.align.justify');
  assert.equal(property.status, 'partial');
  assert.equal(values.status, 'partial');
  assert.equal(property.last_audited_commit,'09a186b4e6f33355db3014ef0b0fcd61df224bc3');
  assert.equal(values.last_audited_commit,'748867bb7ece222cc552a4d315143572fb1bd642');
  assert.equal(values.introduced_release,null);
  assert.equal(property.example,'text-align:justify');
  assert.equal(values.example,'text-align:justify');
  assert.match(property.scope,/PDF TJ/);
  assert.match(values.scope,/自动折行/);
  assert.match(values.scope,/start\/end/);
  assert.match(values.limitations,/start\/end/);
  assert.match(values.limitations,/RTL/);
  assert.match(property.limitations,/FPDF1201/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-align:justify',status:'partial'})
    .map(x=>x.id).sort(), ['css.text-align','value.align.justify']);
});

test('text-indent remains a limited renderable CSS feature after relative audit', () => {
  const f=dataset.features.find(f=>f.id==='css.text-indent');
  assert.equal(f.status,'partial');
  assert.equal(f.last_audited_commit,'7282c0839235443ab49935cb0f2e8092117786a1');
  assert.equal(f.introduced_release,null);
  assert.match(f.scope,/实际单元格或段落内容宽度/);
  assert.match(f.scope,/PDF/);
  assert.match(f.limitations,/FPDF1302/);
  assert.match(f.limitations,/表格单元格/);
  assert.match(f.scope,/html\/body.*div\/section\/article\/blockquote/);
  assert.match(f.scope,/固定列 td\/th/);
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
    assert.equal(feature.last_audited_commit,id==='css.text-indent' ? '7282c0839235443ab49935cb0f2e8092117786a1' : '92e2b02c12740cd85f244bf04e57c70e89e5c353',id);
    assert.match(feature.scope,/em/,id);
    assert.match(feature.scope,/rem/,id);
    assert.equal(feature.introduced_release,null,id);
    assert.equal(feature.test_reference,id==='css.text-indent'
      ? 'https://github.com/KoalaFacts/FactsPDF/blob/7282c0839235443ab49935cb0f2e8092117786a1/tests/FactsPDF.Tests/TableCellTextIndentTests.cs'
      : 'https://github.com/KoalaFacts/FactsPDF/blob/702b907c382b101bdfdc35c8383185ad9d30283f/tests/FactsPDF.Tests/FontRelativeTextSpacingTests.cs');
  }
  assert.match(dataset.features.find(f=>f.id==='css.letter-spacing').limitations,/FPDF1202/);
  assert.match(dataset.features.find(f=>f.id==='css.word-spacing').limitations,/FPDF1302/);
  assert.equal(dataset.features.length,418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
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
    assert.equal(f.last_audited_commit,'b6bebe8e3b97f13190148c6f53bed49ec2678ede');
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
  assert.equal(dataset.accepted_css_property_count,116);
});

test('root-rem and own-font em box lengths are bounded, evidence-backed partial CSS subsets', () => {
  for(const id of ['css.width','css.min-width','css.max-width','css.padding',
    'css.padding-top','css.padding-right','css.padding-bottom','css.padding-left']){
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit, ['css.min-width','css.max-width'].includes(id)
      ? 'b6bebe8e3b97f13190148c6f53bed49ec2678ede' : 'b0e6b53dcd003d74998a5a8d955c2071c0faa1c0', id);
    assert.equal(feature.introduced_release,null,id);
    assert.match(feature.scope,/rem/,id);
    assert.match(feature.limitations,/table|表格/,id);
    assert.equal(feature.test_reference,'https://github.com/KoalaFacts/FactsPDF/blob/76d31cc3c7694a966a68dfc3b848e7e75681fc66/tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs');
  }
  assert.match(dataset.features.find(f=>f.id==='css.width').limitations,/col/);
  const padding=dataset.features.find(f=>f.id==='css.padding');
  assert.match(padding.failure_code,/FPDF1202/);
  assert.match(padding.limitations,/FPDF1201\/1202\/1302/);
  assert.match(dataset.features.find(f=>f.id==='css.font-size').limitations,/margin/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(filterFeatures(dataset.features,{q:'width rem',status:'partial'}).filter(f=>f.id==='css.width').map(f=>f.id),['css.width']);
});

test('bounded rem/em report margins are actual source-bound partial CSS abilities', () => {
  const revised=['css.margin','css.margin-top','css.margin-bottom','css.margin-left',
    'css.margin-right'];
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  for(const id of revised) {
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit, ['css.min-width','css.max-width'].includes(id)
      ? 'b6bebe8e3b97f13190148c6f53bed49ec2678ede' : marginEvidence(id).sha, id);
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
  assert.match(dataset.features.find(f=>f.id==='css.margin').limitations,/table-cell p左右仍仅显式零值/);
  assert.match(dataset.features.find(f=>f.id==='value.length.relative').limitations,/table\/col/);
  assert.match(dataset.features.find(f=>f.id==='value.length.relative').limitations,/img现有受限em\/rem/);
  for(const id of ['css.margin-top','css.margin-bottom'])
    assert.match(dataset.features.find(f=>f.id===id).limitations,/auto/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'margin rem',status:'partial'})
    .filter(f=>f.id==='css.margin').map(f=>f.id),['css.margin']);
});

test('element em geometric scope is audited without weakening rejection',()=>{
  for(const id of ["css.width","css.min-width","css.max-width","css.padding","css.padding-top","css.padding-right","css.padding-bottom","css.padding-left","css.margin","css.margin-top","css.margin-bottom","css.margin-left","css.margin-right"]){
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.status,'partial',id);
    assert.equal(feature.last_audited_commit,
      ['css.min-width','css.max-width'].includes(id)
        ? 'b6bebe8e3b97f13190148c6f53bed49ec2678ede' : marginEvidence(id).sha, id);
    assert.equal(feature.test_reference,'https://github.com/KoalaFacts/FactsPDF/blob/' +
      marginEvidence(id).path,id);
    assert.match(feature.scope,/em/,id);
    assert.equal(feature.introduced_release,null,id);
  }
  assert.equal(dataset.features.find(f=>f.id==='css.font-size').last_audited_commit,
    'b95f1bd3c5d8275bff21f399c9cf59a2d413c024');
  assert.match(dataset.features.find(f=>f.id==='css.padding').limitations,/td\/th包括colspan.*table自身/);
  assert.match(dataset.features.find(f=>f.id==='css.margin').limitations,/table-cell p左右仍仅显式零值/);
  assert.match(dataset.features.find(f=>f.id==='value.length.relative').limitations,/letter-spacing\/word-spacing/);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  assert.equal(dataset.features.length,418);
});

test('numeric hundred-step report font weights use the explicit source-audited role scope', () => {
  const row=dataset.features.find(x=>x.id==='css.font-weight');
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'e94987af9b07c2d7502c25b41625b85aa393f63f');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/100–900/);
  assert.match(row.scope,/100–500/);
  assert.match(row.scope,/600–900/);
  assert.match(row.limitations,/FPDF1507/);
  assert.match(row.scope,/bolder\/lighter/);
  assert.match(row.limitations,/两角色|九档/);
  assert.match(row.evidence_level,/48条/);
  assert.match(dataset.audit_method,/417 full feature records preserve/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('logical padding catalogue distinguishes merged cells from candidate table in both locales', () => {
  assert.equal(dataset.features.length, 418);
  assert.equal(new Set(dataset.features.map(row => row.id)).size, 418);
  assert.deepEqual(counts(dataset.features), {full:44,partial:164,none:202,unknown:6,na:2});
  for (const [id, name, scopeIndex, limitsIndex] of [
    ['css.padding-inline','padding-inline',257,258],
    ['css.padding-block','padding-block',259,260]
  ]) {
    const row=dataset.features.find(entry=>entry.id===id);
    assert.equal(row.status,'partial',id);
    assert.match(row.scope,/PR #215合并并完成主线验证/,id);
    assert.match(row.scope,new RegExp('table自身的'+name),id);
    assert.match(row.scope,/PR #216候选/,id);
    assert.match(row.scope,/尚未合并/,id);
    assert.match(row.limitations,/col\/colgroup\/tr\/thead\/tbody\/tfoot/,id);
    assert.match(row.limitations,/单元格内p的独立padding/,id);
    assert.match(row.limitations,/FPDF1302/,id);
    assert.match(englishAudit[scopeIndex],/main-verified \(#215\)/,id);
    assert.match(englishAudit[scopeIndex],new RegExp('table.{0,20}'+name),id);
    assert.match(englishAudit[scopeIndex],/not merged/,id);
    assert.match(englishAudit[limitsIndex],/col, colgroup, tr, thead, tbody, tfoot/,id);
    assert.match(englishAudit[limitsIndex],/cell-paragraph|paragraphs inside table cells/,id);
    assert.match(englishAudit[limitsIndex],/FPDF1302/,id);
  }
});

test('four grouped LTR logical spacing records include eight side longhands', () => {
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  for (const name of ['margin-inline','margin-block','padding-inline','padding-block']) {
    const row=dataset.features.find(f=>f.id==='css.'+name);
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit, name==='margin-block'
      ? '0f6c58655d651ee934cfb2c95272dde54f4c477e' : '03c17c1f4f4b3eff556cb7353f356ca70439f59a');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope+' '+row.limitations,/LTR|RTL/);
    assert.match(row.scope,/PDF/);
    // The refreshed margin-block audit lists all three codes in compact
    // FPDF1201/1202/1302 notation; other rows spell out FPDF1302.
    assert.match(row.limitations, name==='margin-block'
      ? /FPDF1201\/1202\/1302/ : /FPDF1302/);
    assert.match(row.test_reference, name==='margin-block'
      ? /ParagraphPercentageVerticalMarginTests.cs/ : /LogicalReportLonghandTests.cs/);
    for (const side of [name+'-start', name+'-end']) assert.match(row.scope, new RegExp(side));
    assert.ok(row.sources.some(source => source.includes('/src/FactsPDF/Css/CssSyntaxAdapter.cs')));
  }
});


test('bounded quoted report panels have one source-bound HTML feature and real PDF evidence', () => {
  const row=dataset.features.find(f=>f.id==='html.blockquote');
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_html_tag_count,38);
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'16b235a85b57159b3b7ac2182b38487b6a22f6eb');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/PDF/);
  assert.match(row.scope,/8pt/);
  assert.match(row.scope,/24pt/);
  assert.match(row.scope,/border-radius/);
  assert.match(row.limitations,/椭圆/);
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
  assert.equal(dataset.accepted_css_property_count,116);
  assert.equal(dataset.features.length, 418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});

test('bounded list marker audit retains actual PDF scope and 418 provenance rows',()=>{
  const line=dataset.features.find(f=>f.id==='css.list-style-type');
  assert.equal(line.status,'partial');
  assert.equal(line.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
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
    assert.equal(html.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
    assert.match(html.scope,/list-style-type/);
  }
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  const inside=dataset.features.find(f=>f.id==='css.list-style-position');
  assert.equal(inside.status,'partial');
  assert.equal(inside.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
  assert.match(inside.scope,/同基线/);
  assert.match(inside.limitations,/FPDF1201/);
  assert.equal(inside.introduced_release,null);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('source-bounded ordered-list numbering and one-token shorthand evidence',()=>{
 const style=dataset.features.find(x=>x.id==='css.list-style');
 assert.equal(style.status,'partial');
 assert.equal(style.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
 assert.match(style.scope,/inside/);
 assert.match(style.limitations,/三词/);
 for (const id of ['html.ol','html.ul','html.li']){
  const entry=dataset.features.find(x=>x.id===id);
  assert.equal(entry.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
  assert.match(entry.scope,/start\/reversed/);
  assert.equal(entry.introduced_release,null);
 }
 assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});

test('bounded patterned borders draw PDF strokes without changing 418 audit IDs', () => {
  for(const id of ["css.border","css.border-top","css.border-right","css.border-bottom","css.border-left","css.border-style","css.border-top-style","css.border-right-style","css.border-bottom-style","css.border-left-style"]){
    const row=dataset.features.find(f=>f.id===id);
    assert.equal(row.status,'partial');
    // Real named-colour paint updates the border shorthand and four
    // physical border-side shorthands. Style-only rows keep prior provenance.
    const inkSides=['css.border','css.border-top','css.border-right','css.border-bottom','css.border-left'];
    assert.equal(row.last_audited_commit,inkSides.includes(id)
      ? 'd4584c1f32d6aaa35939ff22f14a11093e92945b' : '4b5d6da3553579dad75dd00bfb041e1f3c200d90');
    assert.match(row.scope,/dashed/);
    assert.match(row.scope,/dotted/);
    if (["css.border-top","css.border-right","css.border-bottom","css.border-left"].includes(id))
      assert.match(row.limitations,/圆角或图片边框/, id);
    else
      assert.match(row.limitations,/double/, id);
    assert.match(row.test_reference,id==='css.border' ? /LogicalBorderComponentTests/ : inkSides.includes(id)
      ? /CssNamedColorTests/ : /ReportPatternedBorderTests/);
    assert.equal(row.introduced_release,null);
  }
  assert.equal(dataset.accepted_css_property_count,116);
  assert.equal(dataset.features.length,418);
  assert.deepEqual(counts(dataset.features), {full:44,partial:164,none:202,unknown:6,na:2});
});


test('source-only HTML pre and CSS pre produce real bounded report text, without broad HTML/CSS promises', () => {
  const pre=dataset.features.find(feature => feature.id==='html.pre');
  const css=dataset.features.find(feature => feature.id==='css.white-space');
  assert.equal(pre.status,'partial');
  assert.equal(pre.last_audited_commit,'d8d1a9e083bfc83829275ec08f6038261b4e1663');
  assert.equal(css.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60');
  assert.match(pre.scope,/U\+0020/);
  assert.match(pre.limitations,/FPDF1305/);
  assert.match(css.scope,/pre-wrap.*pre/);
  assert.equal(dataset.accepted_html_tag_count,38);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});

test('whole-row table-cell vertical alignment stays a scoped, source-proven CSS capability',()=>{
  const row=dataset.features.find(x=>x.id==='css.vertical-align');
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'f4eb1345e8ef587b8ac5f5ca31b7e799d203601b');
  assert.match(row.scope,/td\/th.*top\/middle\/bottom/);
  assert.match(row.limitations,/FPDF1201/);assert.match(row.limitations,/FPDF1202/);assert.match(row.limitations,/FPDF1302/);
  assert.match(row.test_reference,/TableVerticalAlignmentTests\.cs/);
  assert.equal(row.introduced_release,null);
  assert.equal(dataset.features.length,418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  assert.equal(dataset.accepted_css_property_count,116);
});

test('rounded report card radius is bounded real PDF paint, not full CSS Borders',()=>{
 const f=dataset.features.find(x=>x.id==='css.border-radius');
 assert.equal(f.status,'partial');assert.equal(f.last_audited_commit,'16b235a85b57159b3b7ac2182b38487b6a22f6eb');
 assert.equal(f.introduced_release,null);assert.match(f.scope,/贝塞尔.*真实绘制/);
 assert.match(f.scope,/跨页/);assert.match(f.scope,/一至四/);assert.match(f.scope,/左上.*右上.*右下.*左下/);
 assert.match(f.limitations,/百分比.*椭圆/);assert.match(f.scope,/border-top-left-radius.*border-bottom-left-radius/);
 assert.match(f.limitations,/FPDF1201\/1202\/1302\/1401/);
 assert.match(f.test_reference,/RoundedCornerLonghandTests.cs/);
 assert.equal(dataset.features.length,418);assert.equal(dataset.accepted_css_property_count,116);
 assert.deepEqual(filterFeatures(dataset.features,{q:'border-radius',status:'partial'}).map(x=>x.id),['html.blockquote','css.border-radius']);
 assert.deepEqual(filterFeatures(dataset.features,{q:'border-radius',status:'partial',category:'CSS属性'}).map(x=>x.id),['css.border-radius']);
});

test('min-height is a real bounded PDF panel feature, not broad CSS sizing', () => {
  const feature=dataset.features.find(f=>f.id==='css.min-height');
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/div\/section\/article\/blockquote/);
  assert.match(feature.scope,/PDF/);
  assert.match(feature.limitations,/FPDF1302/);
  assert.match(feature.limitations,/max-height/);
  assert.match(feature.test_reference,/ImageHeightConstraintsTests.cs/);
  assert.equal(dataset.features.find(f=>f.id==='css.height').status,'partial');
  assert.equal(dataset.features.find(f=>f.id==='css.max-height').status,'partial');
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('print section page boundaries and legacy aliases retain scoped source evidence', () => {
  const five=['css.break-before','css.break-after','css.page-break-before','css.page-break-after','value.break.page'];
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  for(const id of five) {
    const feature=dataset.features.find(f=>f.id===id);
    assert.equal(feature.last_audited_commit,'67566764419232585632d8ea6f2106aeeeff2f70',id);
    assert.equal(feature.introduced_release,null,id);
    assert.match(feature.test_reference,/SectionPageBreakTests\.cs/);
    assert.match(feature.scope,/PDF|分页/);
    if(id==='value.break.page') assert.match(feature.limitations,/单元格/);
    else assert.match(feature.limitations,/FPDF1201\/1202/);
  }
  assert.match(dataset.features.find(f=>f.id==='css.page-break-before').scope,/always/);
  assert.match(dataset.features.find(f=>f.id==='css.page-break-after').scope,/avoid/);
  assert.equal(dataset.features.find(f=>f.id==='css.page-break-inside').status,'partial');
  assert.equal(dataset.features.find(f=>f.id==='css.break-inside').status,'partial');
  assert.deepEqual(filterFeatures(dataset.features,{q:'page-break-before',status:'partial'}).map(f=>f.id),['css.page-break-before']);
});

test('bounded report height is a real PDF layout feature with a precise source identity', () => {
  const height=dataset.features.find(f=>f.id==='css.height');
  assert.equal(height.status,'partial');
  assert.equal(height.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae');
  assert.equal(height.introduced_release,null);
  assert.match(height.scope,/div\/section\/article\/blockquote/);
  assert.match(height.scope,/content-box\/border-box/);
  assert.match(height.limitations,/FPDF1302/);
  assert.match(height.test_reference,/ImageHeightSizingTests.cs/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('scoped word-break actually places glyphs and preserves all 418 feature identities', () => {
  const row=dataset.features.find(item=>item.id==='css.word-break');
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/word-break:normal\/break-all/);
  assert.match(row.scope,/PDF/);
  assert.match(row.limitations,/FPDF1301/); assert.match(row.limitations,/FPDF1302/);
  assert.match(row.scope,/html\/body.*固定列.*td\/th/);
  assert.match(row.test_reference,/TableCellTextFlowTests.cs/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('bound image sizing appears without losing all 418 records or image transparency limits', () => {
  const ids=['html.img','pdf.images','css.box-sizing','css.min-width','css.max-width'];
  for(const id of ids){
    const f=dataset.features.find(entry=>entry.id===id);
    assert.equal(f.status,'partial',id);
    assert.equal(f.last_audited_commit,
      ['html.img','pdf.images','css.box-sizing'].includes(id)
        ? '6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae' : 'b6bebe8e3b97f13190148c6f53bed49ec2678ede',id);
    assert.match(f.scope,/img|图片|图像/,id);
    assert.match(f.limitations,/FPDF|height|自动缩小|自动收缩|无独立/,id);
    assert.match(f.sources.join(' '),/ImageLayout\.cs/,id);
  }
  assert.match(dataset.features.find(f=>f.id==='html.img').scope,/border-box/);
  assert.match(dataset.features.find(f=>f.id==='pdf.images').scope,/tRNS\/alpha/);
  assert.match(dataset.features.find(f=>f.id==='css.max-width').scope,/百分比/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});


test('bound image height changes actual PDF geometry without changing stable audit scope', () => {
  assert.equal(dataset.dataset_version, 'audit-2026-11-09.fixed-track-relative-widths-candidate');
  assert.equal(dataset.commit, '5c45a68546d0d903eccd2579782e55f2fedff5e4');
  for (const id of ['html.img','pdf.images','css.height','css.box-sizing']) {
    const f=dataset.features.find(x=>x.id===id);
    assert.equal(f.status,'partial',id);
    assert.equal(f.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae',id);
    assert.match(f.scope,/height/,id);
    assert.match(f.sources.join(' '),/ImageHeightSizingTests\.cs|ImageHeightConstraintsTests\.cs/,id);
    assert.equal(f.introduced_release,null,id);
  }
  assert.match(dataset.features.find(x=>x.id==='css.height').scope,/border-box/);
  assert.match(dataset.features.find(x=>x.id==='css.height').limitations,/FPDF1302/);
  assert.match(dataset.features.find(x=>x.id==='pdf.images').scope,/tRNS/);
  for (const id of ['css.min-width','css.max-width'])
    assert.equal(dataset.features.find(x=>x.id===id).last_audited_commit,
      'b6bebe8e3b97f13190148c6f53bed49ec2678ede',id);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});


test('physical image height limits are source-audited and actually scoped to bound images', () => {
  const max=dataset.features.find(f=>f.id==='css.max-height');
  const min=dataset.features.find(f=>f.id==='css.min-height');
  assert.equal(max.status,'partial');
  assert.equal(max.introduced_release,null);
  assert.equal(max.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae');
  assert.match(max.scope,/img|JPEG/);
  assert.match(max.limitations,/FPDF1302/);
  assert.match(max.sources.join(' '),/ImageHeightConstraintsTests\.cs/);
  assert.match(min.scope,/img/);
  assert.match(min.limitations,/百分比/);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('bounded keep-together card is a real, narrowly tracked PDF capability', () => {
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  for(const [id,name] of [['css.break-inside','break-inside:avoid'],['css.page-break-inside','page-break-inside:avoid']]){
    const record=dataset.features.find(f=>f.id===id);
    assert.equal(record.status,'partial');
    assert.equal(record.last_audited_commit,'ecdef6d8745caa3d2b527197c5767e25f89ba6b0');
    assert.equal(record.introduced_release,null);
    assert.equal(record.example,name);
    assert.match(record.scope,/PDF/);
    assert.match(record.limitations,/FPDF1201/);
    assert.match(record.limitations,/FPDF1302/);
    assert.match(record.test_reference,/ReportKeepTogetherTests\.cs/);
  }
  assert.equal(dataset.features.find(f=>f.id==='css.orphans').status,'none');
  assert.equal(dataset.features.find(f=>f.id==='css.widows').status,'none');
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  assert.match(dataset.audit_method,/416条维持历史来源/);
  assert.match(dataset.css_inventory_note,/83个受限CSS属性名/);
});

test('pc and q physical lengths retain 418 audit rows and 107 current CSS property names', () => {
  const extension=dataset.features.filter(f=>f.physical_unit_extension_commit==='ca3aef91e20792782edb49004369dfbf1e9712b3');
  assert.equal(extension.length,37);
  assert.equal(extension.filter(f=>f.id.startsWith('css.')).length,36);
  assert(extension.every(f=>f.scope.includes('pc') && f.scope.includes('q')));
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  assert.equal(dataset.features.find(f=>f.id==='css.margin').last_audited_commit,
    '0f6c58655d651ee934cfb2c95272dde54f4c477e');
});


test('six LTR logical border shorthands are source-bound to existing physical PDF paint', () => {
  assert.equal(dataset.features.length, 418);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.equal(dataset.dataset_version, 'audit-2026-11-09.fixed-track-relative-widths-candidate');
  const row = dataset.features.find(f => f.id === 'css.border');
  assert.equal(row.status, 'partial');
  assert.equal(row.last_audited_commit, 'd4584c1f32d6aaa35939ff22f14a11093e92945b');
  assert.match(row.test_reference, /LogicalBorderComponentTests\.cs$/);
  for (const shorthand of ['border-inline', 'border-block',
      'border-inline-start/end', 'border-block-start/end'])
    assert.ok(row.scope.includes(shorthand), shorthand);
  assert.match(row.scope, /跨页PDF/);
  assert.match(row.limitations, /RTL.*竖排/);
  assert.match(row.limitations, /border-width\/style\/color/);
  assert.match(row.sources.join(' '), /e237b2737572489d7b69227a7bc3c00c2591b3be\/tests\/FactsPDF.Tests\/LogicalBorderReportTests.cs/);
  assert.match(dataset.audit_method, /44-row real paginated report and independent physical-edge PDF oracle/);
  assert.match(dataset.css_inventory_note, /从83增加至89/);
  assert.deepEqual(counts(dataset.features), {full:44, partial:164,none:202, unknown:6, na:2});
});

test('eighteen bounded LTR logical border component names share real per-side PDF geometry',()=>{const r=dataset.features.find(x=>x.id==='css.border');assert.equal(dataset.accepted_css_property_count,116);assert.equal(dataset.features.length,418);assert.equal(r.last_audited_commit,'d4584c1f32d6aaa35939ff22f14a11093e92945b');for(let p of ['border-inline-width/style/color','border-block-width/style/color','border-inline-start/end-{width,style,color}','border-block-start/end-{width,style,color}'])assert.ok(r.scope.includes(p));assert.match(r.test_reference,/LogicalBorderComponentTests\.cs$/);assert.match(r.limitations,/RTL/);assert.ok(r.sources.some(s=>s.includes('99328f071395d5d7cc1eedad9138f05ee118fb0f/tests/FactsPDF.Tests/LogicalBorderComponentTests.cs')));});

test('bounded print/all media is an independently sourced partial at-rule without changing stable property names',()=>{
  const row=dataset.features.find(x=>x.id==='rule.media');
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'70f37e7e881bade30851ecaa06528a27813f0403');
  assert.equal(row.introduced_release,null);
  assert.match(row.scope,/@media print/);
  assert.match(row.scope,/@media all/);
  assert.match(row.scope,/PDF/);
  assert.match(row.limitations,/FPDF1204\/1202/);
  assert.match(row.test_reference,/PrintMediaReportTests\.cs$/);
  assert.ok(row.sources.some(u=>u.includes('70f37e7e881bade30851ecaa06528a27813f0403/src/FactsPDF/Css/CssStylesheets.cs')));
  assert.match(dataset.audit_method,/418 IDs and 107 accepted CSS property names/);
  assert.equal(dataset.features.find(x=>x.id==='css.border').last_audited_commit,'d4584c1f32d6aaa35939ff22f14a11093e92945b');
});

test('bounded CSS decoration ink thickness is provenance-linked without new audit IDs',()=>{
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  const x=dataset.features.find(f=>f.id==='css.text-decoration');
  assert.equal(x.last_audited_commit,'2d18399e25c65267431f0decc7fe716dde82688a');
  assert.match(x.scope,/text-decoration-thickness/);
  assert.match(x.scope,/0.2–12pt/);
  assert.match(x.limitations,/from-font/);
  assert.match(x.test_reference,/CombinedTextDecorationReportTests\.cs$/);
  assert.ok(x.sources.some(url=>url.includes('b8f9b938ec42b108fb6f325715a4dd9b692e027d/src/FactsPDF/Css/CssSyntaxAdapter.cs')));
  assert.match(dataset.css_inventory_note,/由107增至108/);
});

test('bounded text underline offset changes real geometry without new IDs or release claims', () => {
  const row = dataset.features.find(f => f.id === 'css.text-decoration');
  assert.equal(row.last_audited_commit,'2d18399e25c65267431f0decc7fe716dde82688a');
  assert.match(row.scope, /text-underline-offset:auto/);
  assert.match(row.scope, /0–24pt/);
  assert.match(row.limitations, /负偏移/);
  assert.match(row.test_reference, /CombinedTextDecorationReportTests.cs/);
  assert.match(row.scope, /1–3 个互不重复线型/);
  assert.ok(row.sources.some(url=>url.includes('2d18399e25c65267431f0decc7fe716dde82688a/src/FactsPDF/PdfPaintSerializer.cs')));
  assert.equal(row.introduced_release,null);
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
});

test('bounded text-align-last keeps existing 418 feature IDs and precisely audited PDF scope', () => {
  const feature=dataset.features.find(x=>x.id==='css.text-align');
  assert.equal(dataset.accepted_css_property_count,116);
  assert.equal(dataset.features.length,418);
  assert.match(feature.feature,/text-align-last/);
  assert.match(feature.scope,/text-align-last:auto\/left\/center\/right\/start\/end\/justify/);
  assert.match(feature.scope,/真实 PDF 字形X坐标/);
  assert.match(feature.limitations,/inline span/);
  assert.equal(feature.last_audited_commit,'09a186b4e6f33355db3014ef0b0fcd61df224bc3');
  assert.match(feature.test_reference,/LastLineAlignmentReportTests.cs$/);
  assert.deepEqual(filterFeatures(dataset.features,{q:'text-align-last',status:'partial',category:'CSS属性'})
    .map(row=>row.id),['css.text-align']);
  assert.equal(feature.introduced_release,null);
});

test('bounded attribute selectors move a real syntax feature to partial without inflating CSS property names', () => {
  const row = dataset.features.find(f => f.id === 'selector.attribute');
  assert.equal(row.status, 'partial');
  assert.equal(row.last_audited_commit, '845ab1799149e8b833306f2e49b3f8b240d699c8');
  assert.equal(dataset.accepted_css_property_count, 116);
  assert.match(row.scope, /\[attr\]/);
  assert.match(row.scope, /\[attr=value\]/);
  assert.match(row.scope, /\[attr\^=value\]/);
  assert.match(row.scope, /\[attr\*=value\]/);
  assert.match(row.limitations, /CSS Selectors/);
  assert.match(row.test_reference, /AttributeOperatorReportTests\.cs$/);
  assert.equal(row.introduced_release, null);
  assert.deepEqual(counts(dataset.features), {full:44,partial:164,none:202,unknown:6,na:2});
});

// The four bounded-margin scope descriptions must never regress to the old nonpercentage contract.
test('CSS margin catalogue retains bounded vertical percentage support and stable IDs', () => {
  assert.equal(dataset.features.length,418);
  assert.equal(new Set(dataset.features.map(x=>x.id)).size,418);
  assert.equal(dataset.accepted_css_property_count,116);
  for(const id of ['css.margin-top','css.margin-bottom','css.margin','css.margin-block']) {
    const row=dataset.features.find(x=>x.id===id);
    assert.equal(row.status,'partial',id);
    assert.match(row.scope,/p\/pre\/h1–h6/,id);
    assert.match(row.scope,/百分比/,id);
    assert.match(row.scope,/包含块内容宽度/,id);
    assert.doesNotMatch(row.scope,/仍限非负pt|仍限上下pt|段落\/标题只允许非百分比长度/,id);
  }
});

test('bound image history survives the table-track candidate without inflating CSS statuses',()=>{
  const ids=["html.img","pdf.images","css.width","css.min-width","css.max-width","css.height","css.min-height","css.max-height","css.padding","css.padding-top","css.padding-right","css.padding-bottom","css.padding-left","value.length.relative","css.box-sizing"];
  const trackIds=new Set(["css.width","css.padding","css.padding-top","css.padding-right","css.padding-bottom","css.padding-left"]);
  const imageEvidence='https://github.com/KoalaFacts/FactsPDF/blob/145b1bd5dc7ceb4d6b90fbe6c6c32259b0c2f430/tests/FactsPDF.Tests/FontRelativeImageSizingTests.cs';
  assert.equal(dataset.features.length,418);
  assert.equal(dataset.accepted_css_property_count,116);
  for(const id of ids){
    const f=dataset.features.find(entry=>entry.id===id);
    const hasCurrentTrackCandidate=trackIds.has(id);
    assert.equal(f.status,'partial',id);
    assert.equal(f.candidate_source_commit,hasCurrentTrackCandidate?'5c45a68546d0d903eccd2579782e55f2fedff5e4':'145b1bd5dc7ceb4d6b90fbe6c6c32259b0c2f430',id);
    assert.match(f.candidate_test_reference,hasCurrentTrackCandidate
      ? /TableTrackRelativeWidthTests\.cs$/ : /FontRelativeImageSizingTests\.cs$/,id);
    assert.ok(f.sources.includes(imageEvidence),id);
    assert.match(f.scope,/em\/rem/,id);
    assert.match(f.scope,/PR #196/,id);
    assert.match(f.limitations,/百分比/,id);
  }
  for(const id of ['html.table','html.col']){
    const f=dataset.features.find(entry=>entry.id===id);
    assert.equal(f.status,'partial',id);
    assert.equal(f.candidate_source_commit,'5c45a68546d0d903eccd2579782e55f2fedff5e4',id);
    assert.match(f.candidate_test_reference,/TableTrackRelativeWidthTests\.cs$/,id);
    assert.match(f.scope,/未合并CSS候选/,id);
  }
  for(const id of ['css.min-width','css.max-width']){
    const f=dataset.features.find(entry=>entry.id===id);
    assert.doesNotMatch(f.scope,/图片仍拒绝em\/rem/,id);
    assert.match(f.scope,/图片新增有界em\/rem支持/,id);
  }
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});

test('fixed-track td/th text flow preserves partial evidence and IDs', () => {
  for (const id of ['css.white-space','css.word-break','css.overflow-wrap']) {
    const f=dataset.features.find(feature=>feature.id===id);
    assert.ok(f,id);
    assert.equal(f.status,'partial',id);
    assert.equal(f.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60',id);
    assert.equal(f.introduced_release,null,id);
    assert.match(f.scope,/固定列.*td\/th/,id);
    assert.match(f.limitations,/FPDF1201/,id);
    assert.match(f.test_reference,/TableCellTextFlowTests.cs/,id);
  }
  assert.equal(validateDataset(dataset).features.length,418);
  assert.deepEqual(counts(dataset.features),{full:44,partial:164,none:202,unknown:6,na:2});
});
