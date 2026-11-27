import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const data = JSON.parse(read('../data/compatibility.json'));
const han = /[\u3400-\u9fff]/u;
async function localisation() {
  assert.ok(existsSync(new URL('../src/localisation.mjs', import.meta.url)), 'English audit localisation must be implemented, not only a language button');
  const module = await import('../src/localisation.mjs');
  return { ...module, ...await module.prepareTranslations(data) };
}
test('first paint and no-script fallback are English', () => {
  const html = read('../index.html');
  assert.match(html, /<html lang="en">/);
  assert.match(html, /Loading compatibility snapshot/);
  assert.match(html, /This interactive lookup requires JavaScript/);
  assert.doesNotMatch(html, han);
});
test('English is the default and only an explicit saved Chinese choice changes it', async () => {
  const { readLocale, saveLocale } = await localisation();
  assert.equal(readLocale(), 'en');
  assert.equal(readLocale({getItem: () => null}), 'en');
  assert.equal(readLocale({getItem: () => 'zh'}), 'zh');
  assert.equal(readLocale({getItem: () => 'en'}), 'en');
  assert.equal(readLocale({getItem: () => 'unexpected'}), 'en');
  assert.equal(readLocale({getItem: () => {throw new Error('blocked');}}), 'en');
  assert.doesNotThrow(() => saveLocale('zh', {setItem: () => {throw new Error('blocked');}}));
  let stored;
  saveLocale('zh', {setItem: (key, value) => {stored=[key, value];}});
  assert.deepEqual(stored, ['factspdf.locale', 'zh']);
});
test('responsive image audit preserves the full historic methodology and translates only its new appendix', async () => {
  const { translate } = await localisation();
  const raw = data.audit_method;
  const marker = '2026-11-02增量限定图像宽度';
  const at = raw.indexOf(marker);
  assert.ok(at > 10000, 'Historical English source methodology must remain complete');
  const originalEnglish = raw.slice(0, at).trimEnd();
  const displayed = translate(raw);
  assert.ok(displayed.startsWith(originalEnglish + ' '),
    'Never silently replace the historical English methodology with an audit suffix');
  assert.doesNotMatch(displayed, /undefined|[\u3400-\u9fff]/u);
  assert.match(displayed, /Incremental 2 November 2026 source audit updates five existing stable records/);
  assert.match(displayed, /other 413 stable IDs are unchanged/);
  const inventory = translate(data.css_inventory_note);
  assert.match(inventory, /80 scoped CSS property names/);
  assert.match(inventory, /bounded min.max-width/);
});

test('all 418 feature descriptions and methodology have complete English translations', async () => {
  const { auditFields, translate, validateTranslations } = await localisation();
  validateTranslations(data);
  assert.equal(data.features.length, 418);
  for (const row of data.features) for (const key of auditFields) {
    const value = row[key];
    if (typeof value === 'string') {
      assert.equal(typeof translate(value), 'string', `${row.id}.${key}`);
      assert.doesNotMatch(translate(value), han, `${row.id}.${key}`);
      if (value) assert.ok(translate(value).trim(), `${row.id}.${key} must not be hidden`);
    }
  }
  for (const key of ['audit_method','css_inventory_note']) assert.doesNotMatch(translate(data[key]), han);
  for (const drift of data.documentation_drift) assert.doesNotMatch(translate(drift.problem), han);
});
test('display translation preserves source data, support states, provenance and code examples', async () => {
  const { englishFeature, validateTranslations } = await localisation();
  const before = JSON.stringify(data);
  validateTranslations(data);
  for (const row of data.features) {
    const display = englishFeature(row);
    for (const key of ['id','status','parent_id','introduced_milestone','introduced_release','last_audited_commit','last_audited_date','sources','example','failure_code']) assert.deepEqual(display[key], row[key], `${row.id}.${key}`);
  }
  assert.equal(JSON.stringify(data), before);
});
test('missing translations fail explicitly instead of displaying stale or invented support', async () => {
  const { translate, validateTranslations } = await localisation();
  assert.throws(() => translate('未翻译的新范围'), /Missing English audit translation/);
  const changed=structuredClone(data); changed.features[0].scope='未翻译的新范围';
  assert.throws(() => validateTranslations(changed), /Missing English audit translation/);
});
test('English inherited white-space audit stays partial and documents real PDF and unsupported elements', async () => {
  const {englishFeature}=await localisation();
  const raw=data.features.find(row=>row.id==='css.white-space');
  const row=englishFeature(raw);
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'19c6e35eb2987aa84fc7b5ff16b2ccad57630c60');
  assert.match(row.scope,/html\/body/);
  assert.match(row.scope,/div\/section\/article\/blockquote/);
  assert.match(row.scope,/real PDF/);
  assert.match(row.limitations,/list\/table structural/);
  assert.match(row.limitations,/FPDF1201/);
  assert.match(row.limitations,/FPDF1305/);
  assert.doesNotMatch([row.scope,row.limitations,row.evidence_level].join(' '),han);
});

test('translated text keeps important scope restrictions and failure codes', async () => {
  const { englishFeature } = await localisation();
  const byId = id => englishFeature(data.features.find(row=>row.id===id));
  assert.match(byId('css.white-space').scope, /pre-line/);
  assert.match(byId('css.white-space').scope, /CRLF\/CR\/LF/);
  assert.match(byId('css.white-space').scope, /pre-wrap/);
  assert.match(byId('css.white-space').limitations, /tab stops/);
  assert.match(byId('css.white-space').limitations, /FPDF1305/);
  assert.match(byId('css.white-space').scope, /nowrap/);
  assert.match(byId('css.white-space').limitations, /FPDF1302/);
  assert.match(byId('css.letter-spacing').scope, /0–24pt/);
  assert.match(byId('css.letter-spacing').limitations, /FPDF1202/);
  assert.match(byId('css.word-spacing').scope, /PDF TJ/);
  assert.match(byId('css.text-transform').limitations, /FPDF1305/);
  assert.match(byId('pdf.headers').limitations, /65536/);
  assert.match(byId('html.a').limitations, /2048 characters/);
  assert.match(byId('html.a').scope, /clickable PDF \/Link URI annotations/);
});

test('source catalogue binding rejects stale text even when the string count is unchanged', async () => {
  const {prepareTranslations} = await localisation();
  const changed=structuredClone(data);
  for (const row of changed.features) if (row.scope === data.features[0].scope) row.scope='A replaced original scope';
  await assert.rejects(prepareTranslations(changed), /translations need updating/);
});

test('all diagnostic annotations display in English without changing error codes or raw data', async () => {
  const {translate} = await localisation();
  const before = JSON.stringify(data);
  for (const row of data.features) {
    const original=row.failure_code;
    if (typeof original !== 'string') continue;
    const display=translate(original);
    assert.doesNotMatch(display, han, `${row.id} diagnostic is fully translated`);
    assert.deepEqual(display.match(/FPDF\d{4}/g), original.match(/FPDF\d{4}/g), `${row.id} error code identity/order`);
  }
  assert.match(translate(data.features.find(row=>row.id==='css.letter-spacing').failure_code), /FPDF1202 \(unsupported value\)/);
  assert.match(translate(data.features.find(row=>row.id==='pdf.headers').failure_code), /1304–1306/);
  assert.match(translate(data.features.find(row=>row.id==='pdf.headers').failure_code), /return 2/);
  assert.equal(JSON.stringify(data), before);
});


test('merged overflow-wrap audit keeps its exact new support scope in English', async () => {
  const {englishFeature, translate} = await localisation();
  const row=data.features.find(feature=>feature.id==='css.overflow-wrap');
  const display=englishFeature(row);
  assert.equal(display.status,'partial');
  assert.equal(display.last_audited_commit,'fa7531e4e3c7dd0f1199916537d885dc164bf2ab');
  assert.match(display.scope,/flat cell paragraphs/);
  assert.match(display.scope,/legacy word-wrap alias/);
  assert.match(display.scope,/outside lists\/tables/);
  assert.match(display.limitations,/nowrap\/pre forbids soft wraps/);
  assert.match(display.limitations,/FPDF1302/);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/overflow-wrap:break-word/);
});

test('centred report-panel translations retain the bounded PDF margin contract',async()=>{
  const {englishFeature,translate}=await localisation();
  const ids=['css.margin','css.margin-left','css.margin-right'];
  for(const id of ids){
    const source=data.features.find(row=>row.id===id);
    const row=englishFeature(source);
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit,
      id==='css.margin' ? '0f6c58655d651ee934cfb2c95272dde54f4c477e' : 'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope,/div\/section\/article/);
    assert.match(row.scope,/auto/);
    assert.match(row.limitations,/table-cell p|table-cell/);
    assert.match(row.limitations,/calc|negative/);
    assert.doesNotMatch([row.scope,row.limitations,row.applicability].join(' '),han);
  }
  const margin=englishFeature(data.features.find(row=>row.id==='css.margin'));
  assert.match(margin.limitations,/vertical auto/);
  assert.match(margin.scope,/percentage/);
  assert.match(margin.limitations,/FPDF1302/);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/margin:0 auto/);
  assert.match(data.audit_method,/Five-record source-bound report panel/);
  assert.match(data.audit_method,/413 other stable-ID records remain unchanged/);
  assert.equal(data.features.length,418);
});


test('real vertical panel margins retain exact English scope and source identity', async()=>{
  const {englishFeature,translate}=await localisation();
  for(const id of ['css.margin-top','css.margin-bottom']){
    const row=englishFeature(data.features.find(f=>f.id===id));
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit,'0f6c58655d651ee934cfb2c95272dde54f4c477e');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope,/div\/section\/article/);
    assert.match(row.scope,/outside the box/);
    assert.match(row.limitations,/FPDF1201/);
    assert.match(row.limitations,/FPDF1302/);
    assert.match(row.limitations,/collaps/);
    assert.doesNotMatch(row.scope+' '+row.limitations,han);
  }
  const margin=englishFeature(data.features.find(f=>f.id==='css.margin'));
  assert.equal(margin.last_audited_commit,'0f6c58655d651ee934cfb2c95272dde54f4c477e');
  assert.match(margin.scope,/margin:0 auto/);
  assert.match(margin.scope,/margin:12pt auto 7pt/);
  assert.match(margin.limitations,/table-cell p/);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/without repeating margins/);
  assert.equal(data.features.length,418);
  assert.match(data.audit_method,/413 other stable-ID records remain unchanged/);
});

test('numeric font weights are described in English without claiming variable font support',async()=>{
  const {englishFeature,translate}=await localisation();
  const feature=englishFeature(data.features.find(f=>f.id==='css.font-weight'));
  assert.equal(feature.last_audited_commit,'e94987af9b07c2d7502c25b41625b85aa393f63f');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/100–500/);
  assert.match(feature.scope,/600–900/);
  assert.match(feature.limitations,/FPDF1507/);
  assert.match(feature.limitations,/variable font/);
  assert.match(feature.scope,/bolder\/lighter/);
  assert.match(feature.limitations,/nine-weight/);
  assert.match(translate(data.css_inventory_note),/hundred-step values 100–900/);
  assert.doesNotMatch(feature.scope+' '+feature.limitations,han);
});

test('English display describes a truly painted scoped horizontal rule', async () => {
  const { englishFeature } = await localisation();
  const raw=data.features.find(row => row.id==='html.hr');
  const display=englishFeature(raw);
  assert.equal(raw.status,'partial');
  assert.equal(display.status,'partial');
  assert.match(display.scope,/1pt solid currentcolor/);
  assert.match(display.scope,/PDF/);
  assert.match(display.limitations,/FPDF1101\/1103\/1201\/1202\/1302/);
  assert.match(display.failure_code,/FPDF1302/);
  assert.match(display.limitations,/not inside paragraphs/);
  assert.equal(display.last_audited_commit,'8c1d475e92902572bf38d53cd2f7d0d5c48d2eca');
});

test('LTR logical spacing has complete English audit text', async () => {
  const {englishFeature,translate}=await localisation();
  for(const name of ['margin-inline','margin-block','padding-inline','padding-block']) {
    const row=englishFeature(data.features.find(f=>f.id==='css.'+name));
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit, name==='margin-block'
      ? '0f6c58655d651ee934cfb2c95272dde54f4c477e' : '03c17c1f4f4b3eff556cb7353f356ca70439f59a');
    assert.match(row.scope+' '+row.limitations,/LTR|RTL/);
    assert.match(row.scope,/PDF/);
    assert.match(row.scope,new RegExp(name+'-start'));
    assert.match(row.scope,new RegExp(name+'-end'));
    assert.match(row.limitations, name==='margin-block'
      ? /FPDF1201\/1202\/1302/ : /FPDF1302/);
    assert.doesNotMatch(row.scope+' '+row.limitations,han);
  }
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/margin-inline-start/);
  assert.match(translate(data.css_inventory_note),/padding-block-end/);
  assert.match(translate(data.css_inventory_note),/without repeating margins/);
});


test('new report blockquotes have English default text with bounded limits', async () => {
  const {englishFeature}=await localisation();
  const row=englishFeature(data.features.find(f=>f.id==='html.blockquote'));
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'16b235a85b57159b3b7ac2182b38487b6a22f6eb');
  assert.match(row.scope,/8pt/);
  assert.match(row.scope,/24pt/);
  assert.match(row.scope,/PDF/);
  assert.match(row.scope,/border-radius/);
  assert.match(row.limitations,/elliptical/);
  assert.match(row.limitations,/cite/);
  assert.match(row.limitations,/FPDF1302/);
  assert.doesNotMatch([row.scope,row.limitations,row.stage,row.evidence_level].join(' '),han);
});


test('real display:none source limitations are completely translated into English', async () => {
  const { englishFeature, translate } = await localisation();
  const feature = englishFeature(data.features.find(row => row.id === 'css.display'));
  assert.equal(feature.status,'partial');
  assert.equal(feature.last_audited_commit,'9762e45dac1907eb77234132c2e14c348aa55264');
  assert.match(feature.scope,/display:none/);
  assert.match(feature.scope,/pagination space/);
  assert.match(feature.limitations,/hidden attribute/);
  assert.match(feature.limitations,/strict HTML\/CSS/);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/Scoped display:block\/none/);
});

test('bounded CSS list markers have complete English source-bound translations',async()=>{
  const {englishFeature,translate}=await localisation();
  const entry=englishFeature(data.features.find(f=>f.id==='css.list-style-type'));
  assert.equal(entry.status,'partial');
  assert.equal(entry.last_audited_commit,'22e73cd894b8bdc750a610c1a91e11d3b36978ca');
  assert.match(entry.scope,/hollow-circle/);
  assert.match(entry.scope,/solid-square/);
  assert.doesNotMatch(entry.limitations,/circle\/square/);
  assert.match(entry.scope,/lower-roman/);
  assert.match(entry.scope,/Actual PDF/);
  assert.match(entry.limitations,/3999/);
  const position=englishFeature(data.features.find(f=>f.id==='css.list-style-position'));
  assert.equal(position.status,'partial');
  assert.match(position.scope,/inside or outside/);
  assert.match(position.limitations,/FPDF1201/);
  assert.doesNotMatch(position.scope+position.limitations,han);
  assert.match(translate(data.css_inventory_note),/from 109 to 110/);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
  assert.doesNotMatch(entry.scope+' '+entry.limitations,han);
  for(const id of ['html.ol','html.ul','html.li'])
    assert.doesNotMatch(englishFeature(data.features.find(f=>f.id===id)).scope,han);
});

test('patterned border scope and limitations remain precise in English', async () => {
  const { englishFeature } = await localisation();
  for(const id of ["css.border","css.border-top","css.border-right","css.border-bottom","css.border-left","css.border-style","css.border-top-style","css.border-right-style","css.border-bottom-style","css.border-left-style"]){
    const feature=englishFeature(data.features.find(f=>f.id===id));
    assert.match(feature.scope,/dashed/);
    assert.match(feature.scope,/dotted/);
    if (["css.border-top","css.border-right","css.border-bottom","css.border-left"].includes(id))
      assert.match(feature.limitations,/rounded.*image borders/, id);
    else
      assert.match(feature.limitations,/double/, id);
    assert.doesNotMatch(feature.evidence_level,han);
  }
});

test('scoped whole-row vertical alignment has audited English rendering',async()=>{
 const {englishFeature,translate}=await localisation();
 const row=englishFeature(data.features.find(f=>f.id==='css.vertical-align'));
 assert.equal(row.status,'partial');
 assert.equal(row.last_audited_commit,'f4eb1345e8ef587b8ac5f5ca31b7e799d203601b');
 assert.match(row.scope,/Fixed-column table td\/th/);
 assert.match(row.limitations,/FPDF1302/);
 assert.match(row.limitations,/No rowspan/);
 assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
 assert.doesNotMatch(row.scope+' '+row.limitations,han);
});

test('rounded report panel audit translates vector paint and explicit limits',async()=>{
 const {englishFeature,translate}=await localisation();
 const f=englishFeature(data.features.find(x=>x.id==='css.border-radius'));
 assert.equal(f.status,'partial');assert.match(f.scope,/Bezier/);
 assert.match(f.scope,/clockwise/);assert.match(f.scope,/page/);assert.match(f.limitations,/elliptical/);assert.match(f.limitations,/longhands/);
 assert.match(f.limitations,/FPDF1201\/1202\/1302\/1401/);
 assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
});

test('new source-bound panel breaks have fully translated English limits',async()=>{
  const {englishFeature,translate}=await localisation();
  for(const id of ['css.break-before','css.break-after','css.page-break-before','css.page-break-after','value.break.page']) {
    const original=data.features.find(f=>f.id===id);
    const translated=englishFeature(original);
    assert.equal(translated.last_audited_commit,'67566764419232585632d8ea6f2106aeeeff2f70',id);
    assert.match(translated.scope,/PDF/);
    if(id==='value.break.page') assert.match(translated.limitations,/CSS Fragmentation/);
    else assert.match(translated.limitations,/FPDF1201\/1202/);
    assert.doesNotMatch(translated.scope+' '+translated.limitations,/[\u3400-\u9fff]/u);
  }
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
});

test('fixed-height card translation does not misrepresent general CSS Sizing', async () => {
  const {englishFeature} = await localisation();
  const height = englishFeature(data.features.find(row => row.id === 'css.height'));
  assert.equal(height.status, 'partial');
  assert.match(height.scope, /actual|really change/);
  assert.match(height.scope, /content-box\/border-box/);
  assert.match(height.limitations, /FPDF1302/);
  assert.match(height.limitations, /Not generic CSS Sizing/);
  assert.doesNotMatch(height.scope + height.limitations, han);
});

test('scoped word-break English audit distinguishes normal emergency wrap from break-all',async()=>{
  const {englishFeature,translate}=await localisation();
  const row=englishFeature(data.features.find(item=>item.id==='css.word-break'));
  assert.equal(row.status,'partial');
  assert.equal(row.last_audited_commit,'2732d4e49cba7bcbb82d30023291d0301dfc4802');
  assert.match(row.scope,/current PDF line/);
  assert.match(row.scope,/glyph/);
  assert.match(row.limitations,/keep-all/);
  assert.match(row.scope,/break-word[^.]*byte-identical to overflow-wrap:break-word/);
  assert.doesNotMatch(row.limitations,/break-word/);
  assert.match(row.limitations,/FPDF1301\/1302/);
  assert.doesNotMatch(row.scope+' '+row.limitations,han);
  assert.match(translate(data.css_inventory_note),/80 scoped CSS property names/);
});


test('physical image height has complete English audit, with original methodology retained', async () => {
  const {englishFeature,translate}=await localisation();
  for(const id of ['html.img','pdf.images','css.box-sizing','css.height']){
    const f=englishFeature(data.features.find(x=>x.id===id));
    assert.equal(f.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae',id);
    assert.match(f.scope,/height/i,id);
    assert.match(f.limitations,/percent|em\/rem/i,id);
    assert.doesNotMatch(f.scope+' '+f.limitations,han,id);
  }
  const method=translate(data.audit_method);
  assert.match(method,/Additive 2 November 2026 physical CSS image-height/);
  assert.match(method,/414 other records retain historic source\/release identities/);
  assert.match(method,/Incremental 2 November 2026 source audit updates five existing stable records/);
  assert.doesNotMatch(method,/undefined|[\u3400-\u9fff]/u);
  assert.match(translate(data.css_inventory_note),/418 audit records and 81 scoped CSS property names/);
});


test('new English image height limit scope accurately separates bound img from panels', async () => {
  const { englishFeature, translate } = await localisation();
  const max = englishFeature(data.features.find(row => row.id === 'css.max-height'));
  assert.equal(max.status,'partial');
  assert.equal(max.last_audited_commit,'6ff2f6fd1cc21b2a0657f333d3bb9e6a73fed1ae');
  assert.match(max.scope,/real PDF image matrix/);
  assert.match(max.limitations,/object-fit/);
  assert.match(max.limitations,/FPDF1302/);
  assert.doesNotMatch(max.scope+max.limitations,han);
  assert.match(translate(data.css_inventory_note),/81 scoped CSS property names/);
});

test('kept-card source scope has complete English while preserving all old methodology', async () => {
  const {englishFeature,translate}=await localisation();
  for(const id of ['css.break-inside','css.page-break-inside']){
    const row=englishFeature(data.features.find(f=>f.id===id));
    assert.equal(row.status,'partial');
    assert.equal(row.last_audited_commit,'ecdef6d8745caa3d2b527197c5767e25f89ba6b0');
    assert.match(row.scope,/PDF/);
    assert.match(row.limitations,/FPDF1302/);
    assert.match(row.limitations,/top-level|nested/);
    assert.doesNotMatch(row.scope+' '+row.limitations,han);
  }
  assert.match(translate(data.audit_method),/historical source\/release identities are preserved/);
  assert.match(translate(data.audit_method),/Incremental 2 November 2026 source audit updates five/);
  assert.match(translate(data.css_inventory_note),/83 scoped CSS property names/);
  assert.doesNotMatch(translate(data.css_inventory_note),han);
});

test('HSL colour support has exact English scope and leaves RGB history intact',async()=>{
  const {englishFeature,translate}=await localisation();
  for(const id of ['css.color','css.background','css.background-color','css.border','css.border-color',
    'css.border-top-color','css.border-right-color','css.border-bottom-color','css.border-left-color']) {
    const f=englishFeature(data.features.find(f=>f.id===id));
    assert.equal(f.last_audited_commit,['css.border','css.border-top','css.border-right','css.border-bottom','css.border-left'].includes(id) ? 'd4584c1f32d6aaa35939ff22f14a11093e92945b' : id==='css.text-decoration' ? '2d18399e25c65267431f0decc7fe716dde82688a' : id==='css.background' ? '0482c90ffeab1df474d7e2b38a7aa449de18082e' : ['css.color','css.background-color'].includes(id) ? '3dbcded4f5d39b16b79bbff7aac4132305659d4a' : 'c9138808d785986712f17e977fa2371854cb6aed');
    assert.match(f.scope,/Opaque HSL/);assert.match(f.limitations,/alpha/);
    assert.doesNotMatch(f.scope+' '+f.limitations,han);
  }
  assert.match(englishFeature(data.features.find(f=>f.id==='value.color.rgbfunc')).scope,/percentage/);
  assert.match(translate(data.audit_method),/percent-RGB audit/);
});


test('named CSS colours retain the independent source and English limitations', async () => {
  const { englishFeature } = await localisation();
  for (const id of ['css.color','css.background','css.background-color','css.border','css.border-color','css.text-decoration']) {
    const row = data.features.find(f => f.id === id);
    const english = englishFeature(row);
    assert.equal(row.last_audited_commit, id==='css.border' ? 'd4584c1f32d6aaa35939ff22f14a11093e92945b' : id==='css.text-decoration' ? '2d18399e25c65267431f0decc7fe716dde82688a' : id==='css.background' ? '0482c90ffeab1df474d7e2b38a7aa449de18082e' : ['css.color','css.background-color'].includes(id) ? '3dbcded4f5d39b16b79bbff7aac4132305659d4a' : 'c9138808d785986712f17e977fa2371854cb6aed');
    assert.match(row.scope, /148 种标准不透明 CSS 具名颜色/);
    assert.match(english.scope, /148 standard opaque (CSS )?named colours/);
    assert.match(english.scope, /PDF/);
    assert.doesNotMatch(english.scope, han);
    assert.equal(row.introduced_release, null);
  }
  assert.equal(data.features.length,418);
  assert.equal(data.accepted_css_property_count, 384);
});


test('English LTR logical border audit is complete and does not claim RTL support', async () => {
  const { englishFeature, translate } = await localisation();
  const feature=englishFeature(data.features.find(x=>x.id==='css.border'));
  assert.equal(data.features.length,418);
  assert.equal(data.accepted_css_property_count, 384);
  assert.match(feature.scope,/border-inline/);
  assert.match(feature.scope,/border-block/);
  assert.match(feature.scope,/actual left\/right\/top\/bottom PDF strokes/);
  assert.match(feature.limitations,/RTL, vertical writing/);
  assert.doesNotMatch(feature.scope+' '+feature.limitations,han);
  assert.match(translate(data.audit_method),/44-entry multi-page report/);
  assert.match(translate(data.css_inventory_note),/accepted CSS name count is now 107/);
  assert.doesNotMatch(translate(data.css_inventory_note),han);
});

test('English LTR component audit retains precise 18-name limits',async()=>{const {englishFeature,translate}=await localisation();const f=englishFeature(data.features.find(f=>f.id==='css.border'));assert.equal(data.accepted_css_property_count, 384);assert.match(f.scope,/18 additional scoped LTR logical border component names/);assert.match(f.limitations,/RTL, vertical writing/);assert.match(translate(data.css_inventory_note),/now 107/);assert.doesNotMatch(f.scope+f.limitations,han);});

test('bounded printable text decoration width is translated with exact limits',async()=>{
  const {englishFeature,translate}=await localisation();
  const row=englishFeature(data.features.find(f=>f.id==='css.text-decoration'));
  assert.equal(data.accepted_css_property_count, 384);
  assert.match(row.scope,/three distinct/);
  assert.match(row.scope,/0.2–12pt/);
  assert.match(row.scope,/resets omitted thickness to auto/);
  assert.match(row.limitations,/percentages, from-font, calc/);
  assert.match(translate(data.css_inventory_note),/from 107 to 108/);
  assert.doesNotMatch(row.scope+row.limitations,/每|不支持|受限/u);
});

test('bounded underline offset remains exact in verified English and Chinese', async () => {
  const { englishFeature, translate } = await localisation();
  const row = englishFeature(data.features.find(f=>f.id==='css.text-decoration'));
  assert.match(row.scope,/text-underline-offset:auto/);
  assert.match(row.scope,/0–24pt/);
  assert.match(row.limitations,/negative offset/);
  assert.match(translate(data.css_inventory_note),/from 108 to 109/);
  assert.doesNotMatch(row.scope+row.limitations,/[\u3400-\u9fff]/u);
  assert.equal(data.features.length,418);
  assert.equal(data.accepted_css_property_count, 384);
});

test('English CLI diagnostic location record is source-bound and fully translated',async()=>{
  const {englishFeature}=await localisation();
  const cli=data.features.find(f=>f.id==='runtime.cli');
  assert.equal(cli.last_audited_commit,'306012538553815967d0e364ef95842648c446bb');
  assert.match(cli.test_reference,/CliErrorLocationTests\.cs$/);
  const english=englishFeature(cli);
  assert.match(english.limitations,/line and column/);
  assert.match(english.limitations,/LF, CRLF and CR/);
  assert.doesNotMatch(english.limitations+english.evidence_level,han);
});

test('English .NET SDK records name the fluent Pdf.Write() writer, not the retired API',async()=>{
  const {englishFeature}=await localisation();
  for(const id of ['runtime.dotnet','pdf.headers','pdf.page-options','rule.page']){
    const english=englishFeature(data.features.find(f=>f.id===id));
    const text=Object.values(english).filter(v=>typeof v==='string').join(' ');
    assert.doesNotMatch(text,/PdfOptions|PdfConverter|LoadTrueType|LoadImage/);
    assert.doesNotMatch(english.scope+english.limitations+english.feature,han);
  }
  assert.match(englishFeature(data.features.find(f=>f.id==='runtime.dotnet')).scope,/Pdf\.Write\(\)/);
});

test('bounded English last-line alignment audit retains source and error boundary',async()=>{
  const {englishFeature,translate}=await localisation();
  const row=englishFeature(data.features.find(x=>x.id==='css.text-align'));
  assert.equal(data.features.length,418);
  assert.equal(data.accepted_css_property_count, 384);
  assert.match(row.scope,/text-align-last:auto\/left\/center\/right\/start\/end\/justify/);
  assert.match(row.limitations,/inline declarations/);
  assert.match(translate(data.css_inventory_note),/115 to 116/);
  assert.doesNotMatch(row.scope+row.limitations,/\p{Script=Han}/u);
});

for(const id of ['css.margin-top','css.margin-bottom','css.margin','css.margin-block']) {
  test('English margin scope stays source-bound to corrected Chinese text: '+id, async()=>{
    const {englishFeature}=await localisation();
    const row=data.features.find(x=>x.id===id),english=englishFeature(row);
    assert.equal(english.status,'partial');
    assert.match(row.scope,/百分比/);
    assert.match(english.scope,/percentag/i);
    assert.doesNotMatch(english.scope,han);
    assert.equal(english.last_audited_commit,row.last_audited_commit);
    assert.equal(english.introduced_release,row.introduced_release);
  });
}

test('initial-value-only CSS rows are fully translated without claiming layout support',async()=>{
  const {englishFeature,translate}=await localisation();
  const row=englishFeature(data.features.find(f=>f.id==='css.visibility'));
  assert.match(row.scope,/visibility:visible/);
  assert.match(row.limitations,/No positioning, floats/);
  assert.match(row.limitations,/FPDF1202/);
  assert.doesNotMatch(row.scope+row.limitations+row.stage+row.evidence_level,/[㐀-鿿]/u);
  assert.match(translate(data.css_inventory_note),/from 117 to 126/);
  assert.match(translate(data.css_inventory_note),/from 126 to 131/);
  assert.match(englishFeature(data.features.find(f=>f.id==='css.box-shadow')).limitations,/shadows, transforms/);
  assert.match(translate(data.css_inventory_note),/from 131 to 135/);
  assert.match(translate(data.css_inventory_note),/cursor, pointer-events, user-select, resize/);
  assert.match(translate(data.css_inventory_note),/from 135 to 137/);
  assert.match(translate(data.css_inventory_note),/-webkit-print-color-adjust/);
  assert.match(translate(data.css_inventory_note),/from 137 to 140/);
  assert.match(translate(data.css_inventory_note),/-moz-osx-font-smoothing/);
  assert.match(translate(data.css_inventory_note),/from 140 to 141/);
  assert.match(translate(data.css_inventory_note),/-webkit-text-size-adjust/);
  assert.match(translate(data.css_inventory_note),/from 141 to 142/);
  assert.match(translate(data.css_inventory_note),/appearance and its -webkit-\/-moz- spellings/);
  assert.match(translate(data.css_inventory_note),/from 142 to 144/);
  assert.match(translate(data.css_inventory_note),/touch-action/);
  assert.match(translate(data.css_inventory_note),/from 144 to 145/);
  assert.match(translate(data.css_inventory_note),/will-change only/);
  assert.match(translate(data.css_inventory_note),/from 145 to 151/);
  assert.match(translate(data.css_inventory_note),/transition-property, -duration/);
  assert.match(translate(data.css_inventory_note),/from 151 to 160/);
  assert.match(translate(data.css_inventory_note),/from 160 to 169/);
  assert.match(translate(data.css_inventory_note),/-webkit-overflow-scrolling/);
  assert.match(translate(data.css_inventory_note),/from 169 to 174/);
  assert.match(translate(data.css_inventory_note),/tap highlight/);
  assert.match(translate(data.css_inventory_note),/from 174 to 176/);
  assert.match(translate(data.css_inventory_note),/-moz-box-sizing/);
  assert.match(translate(data.css_inventory_note),/from 176 to 178/);
  assert.match(translate(data.css_inventory_note),/-ms-text-size-adjust/);
  assert.match(translate(data.css_inventory_note),/from 178 to 180/);
  assert.match(translate(data.css_inventory_note),/forced-color-adjust/);
  assert.match(translate(data.css_inventory_note),/from 180 to 184/);
  assert.match(translate(data.css_inventory_note),/mix-blend-mode:normal/);
  assert.match(translate(data.css_inventory_note),/from 184 to 195/);
  assert.match(translate(data.css_inventory_note),/-webkit-backdrop-filter are the legacy engine spellings/);
  assert.match(translate(data.css_inventory_note),/from 195 to 201/);
  assert.match(translate(data.css_inventory_note),/from 201 to 210/);
  assert.match(translate(data.css_inventory_note),/from 210 to 213/);
  assert.match(translate(data.css_inventory_note),/from 213 to 219/);
  assert.match(translate(data.css_inventory_note),/from 219 to 220/);
  assert.match(translate(data.css_inventory_note),/border-collapse:separate and border-spacing at one or two zero lengths[^.]*from 370 to 372/);
  assert.match(translate(data.css_inventory_note),/empty-cells:show and caption-side:top[^.]*from 372 to 374/);
  assert.match(translate(data.css_inventory_note),/offset-path:none, offset-rotate and offset-anchor at auto, initial-letter:normal[^.]*from 374 to 379/);
  assert.match(translate(data.css_inventory_note),/quotes at auto, none, match-parent or pairs of strings[^.]*from 379 to 380/);
  assert.match(translate(data.css_inventory_note),/gap and row-gap at normal[^.]*from 380 to 382/);
  assert.match(translate(data.css_inventory_note),/font-family as a family list[^.]*from 382 to 383/);
  assert.match(translate(data.css_inventory_note),/background-color-adjust is an alias of print-color-adjust[^.]*from 383 to 384/);
  { const row=data.features.find(x=>x.id==='css.font-family'); assert.equal(row.status,'partial'); assert.equal(row.failure_code,'FPDF1202'); assert.match(englishFeature(row).limitations,/never matched by name/); assert.doesNotMatch(englishFeature(row).scope+englishFeature(row).limitations,han); }
  for (const id of ['css.gap','css.row-gap']) { const row=data.features.find(x=>x.id===id); assert.equal(row.status,'partial'); assert.equal(row.failure_code,'FPDF1202'); assert.doesNotMatch(englishFeature(row).scope+englishFeature(row).limitations,han); }
  { const row=data.features.find(x=>x.id==='css.quotes'); assert.equal(row.status,'partial'); assert.equal(row.failure_code,'FPDF1202'); assert.doesNotMatch(englishFeature(row).scope+englishFeature(row).limitations,han); }
  for (const id of ['css.border-collapse','css.border-spacing','css.empty-cells','css.caption-side']) { const row=data.features.find(x=>x.id===id); assert.equal(row.status,'partial'); assert.equal(row.failure_code,'FPDF1202'); assert.doesNotMatch(englishFeature(row).scope+englishFeature(row).limitations,han); }
  assert.match(translate(data.css_inventory_note),/font-synthesis-position at none are validated and dropped like the shorthand[^.]*from 366 to 370/);
  assert.match(translate(data.css_inventory_note),/from 220 to 222/);
  assert.match(translate(data.css_inventory_note),/from 222 to 224/);
  assert.match(translate(data.css_inventory_note),/from 224 to 228/);assert.match(translate(data.css_inventory_note),/from 228 to 233/);assert.match(translate(data.css_inventory_note),/from 233 to 238/);assert.match(translate(data.css_inventory_note),/from 238 to 243/);assert.match(translate(data.css_inventory_note),/from 243 to 251/);assert.match(translate(data.css_inventory_note),/from 251 to 256/);assert.match(translate(data.css_inventory_note),/from 256 to 266/);assert.match(translate(data.css_inventory_note),/from 266 to 270/);assert.match(translate(data.css_inventory_note),/from 270 to 275/);assert.match(translate(data.css_inventory_note),/from 275 to 283/);assert.match(translate(data.css_inventory_note),/from 283 to 305/);assert.match(translate(data.css_inventory_note),/from 305 to 308/);
  assert.match(translate(data.css_inventory_note),/content-visibility:visible/);
  assert.match(translate(data.css_inventory_note),/-moz-tab-size/);
  assert.match(translate(data.css_inventory_note),/font-variation-settings/);
  assert.match(translate(data.css_inventory_note),/-webkit-touch-callout/);
  assert.match(translate(data.css_inventory_note),/@keyframes rule/);
});

test('English outline audit keeps panel-only paint and dropped resets elsewhere', async () => {
  const { englishFeature } = await localisation();
  const row = data.features.find(f => f.id === 'css.outline');
  assert.equal(row.status, 'partial');
  const f = englishFeature(row);
  assert.match(f.limitations, /outline:none\/0/);
  assert.match(f.limitations, /still fails FPDF1201/);
  assert.match(row.limitations, /outline:none\/0/);
  assert.doesNotMatch(f.scope + f.limitations, han);
});

test('box-sizing on the root, inline text and tables is translated with its table-box geometry', async () => {
  const { englishFeature } = await localisation();
  const row = data.features.find(x => x.id === 'css.box-sizing');
  assert.equal(row.status, 'partial');
  const english = englishFeature(row);
  assert.match(english.limitations, /<html> and inline text \(span\/b\/strong\/i\/em\/small\/u\/s\/del\/ins\/a\) accept it without changing output/);
  assert.match(english.limitations, /On a table, border-box makes an explicit width include its padding and borders/);
  assert.match(english.limitations, /universal \*\{box-sizing:border-box\} reset also passes on pages with tables/);
  assert.match(english.applicability, /table structure \(col\/row\/cell boxes and cell paragraphs\) accept it with no effect; the table box width supports border-box\./);
  assert.doesNotMatch(english.scope + english.limitations + english.applicability, han);
});

test('zero root margins and html padding are translated without claiming body margin support', async () => {
  const { englishFeature } = await localisation();
  for (const id of ['css.margin', 'css.margin-top', 'css.margin-left', 'css.margin-inline', 'css.margin-block']) {
    const english = englishFeature(data.features.find(x => x.id === id));
    assert.match(english.limitations, /body.*accept only (a )?zero|zero margin on <html> or <body>/, id);
    assert.doesNotMatch(english.limitations, han, id);
  }
  const padding = englishFeature(data.features.find(x => x.id === 'css.padding'));
  assert.match(padding.limitations, /<html> accepts only a zero padding, which changes no output; a nonzero value still fails FPDF1201\./);
});

test('zero inline margins, padding and transparent inline backgrounds are translated as exceptions only', async () => {
  const { englishFeature } = await localisation();
  for (const [id, kind] of [['css.margin', 'a zero margin'], ['css.margin-left', 'a zero margin'], ['css.padding', 'a zero padding'],
    ['css.padding-inline', 'a zero padding'], ['css.background-color', 'a transparent background'], ['css.background', 'a transparent background']]) {
    const english = englishFeature(data.features.find(x => x.id === id));
    assert.match(english.limitations, new RegExp(`Exception: ${kind}.* on inline text \\(span/b/strong/i/em/small/u/s/del/ins/a\\) is accepted`), id);
    assert.match(english.limitations, /is accepted and (changes no output|paints nothing); other values still fail FPDF1201\./, id);
    assert.doesNotMatch(english.limitations, han, id);
  }
});

test('zero table-part and list-item margins are translated for every margin row', async () => {
  const { englishFeature } = await localisation();
  for (const id of ['css.margin', 'css.margin-top', 'css.margin-bottom', 'css.margin-left', 'css.margin-right', 'css.margin-inline', 'css.margin-block']) {
    const english = englishFeature(data.features.find(x => x.id === id));
    assert.match(english.limitations, /A zero margin on li and on table rows, row groups, columns, column groups and cells \(tr\/thead\/tbody\/col\/colgroup\/td\/th\) is the initial value/, id);
    assert.match(english.limitations, /cells \(tr\/thead\/tbody\/col\/colgroup\/td\/th\)[^.]*; nonzero values still fail FPDF1201\./, id);
    assert.doesNotMatch(english.limitations, han, id);
  }
});

test('zero ul, ol, table and br margins and zero table-part padding are translated', async () => {
  const { englishFeature } = await localisation();
  for (const id of ['css.margin', 'css.margin-top', 'css.margin-bottom', 'css.margin-left', 'css.margin-right', 'css.margin-inline', 'css.margin-block']) {
    const english = englishFeature(data.features.find(x => x.id === id));
    assert.match(english.limitations, /A zero margin on ul, ol, table and br is the initial value and is accepted with no change to output; nonzero values and inherit still fail FPDF1201\.$/, id);
    assert.doesNotMatch(english.limitations, han, id);
  }
  for (const id of ['css.padding', 'css.padding-top', 'css.padding-right', 'css.padding-bottom', 'css.padding-left', 'css.padding-inline', 'css.padding-block']) {
    const english = englishFeature(data.features.find(x => x.id === id));
    assert.match(english.limitations, /A zero padding on table rows, row groups, columns and column groups \(tr\/thead\/tbody\/tfoot\/col\/colgroup\) and on br is accepted with no change to output; nonzero values and inherit still fail FPDF1201\.$/, id);
    assert.doesNotMatch(english.limitations, han, id);
  }
});

test('code, kbd, samp, var, cite and dfn are partial inline text with English audit text', async () => {
  const { englishFeature } = await localisation();
  for (const tag of ['code', 'kbd', 'samp', 'var', 'cite', 'dfn']) {
    const feature = data.features.find(x => x.id === `html.${tag}`);
    assert.equal(feature.status, 'partial', tag);
    const english = englishFeature(feature);
    assert.match(english.scope, new RegExp(`^<${tag}> renders as inline `), tag);
    assert.match(english.limitations, /There is no monospace fallback/, tag);
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(english[field], han, `${tag} ${field}`);
  }
});

test('time and data are partial inline text with English audit text', async () => {
  const { englishFeature } = await localisation();
  for (const [tag, attribute] of [['time', 'datetime'], ['data', 'value']]) {
    const feature = data.features.find(x => x.id === `html.${tag}`);
    assert.equal(feature.status, 'partial', tag);
    const english = englishFeature(feature);
    assert.match(english.scope, new RegExp(`^<${tag}> renders as inline text with no default style; its ${attribute} attribute`), tag);
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(english[field], han, `${tag} ${field}`);
  }
});

test('abbr is partial inline text with a dotted underline when titled, in English', async () => {
  const { englishFeature } = await localisation();
  const feature = data.features.find(x => x.id === 'html.abbr');
  assert.equal(feature.status, 'partial');
  const english = englishFeature(feature);
  assert.match(english.scope, /^<abbr> renders as inline text; with a title attribute it draws the HTML default dotted underline/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(english[field], han, field);
});

test('header, footer, main, nav and aside are partial block containers like div, in English', async () => {
  const { englishFeature } = await localisation();
  for (const tag of ['header', 'footer', 'main', 'nav', 'aside']) {
    const feature = data.features.find(x => x.id === `html.${tag}`);
    assert.equal(feature.status, 'partial', tag);
    const english = englishFeature(feature);
    assert.match(english.scope, /^A nested block container like div/, tag);
    if (tag === 'header' || tag === 'footer') assert.match(english.limitations, /PageFurniture/, tag);
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(english[field], han, `${tag} ${field}`);
  }
});

test('figure is a partial block like blockquote and figcaption a block like div, in English', async () => {
  const { englishFeature } = await localisation();
  const figure = englishFeature(data.features.find(x => x.id === 'html.figure'));
  const caption = englishFeature(data.features.find(x => x.id === 'html.figcaption'));
  assert.equal(figure.status, 'partial');
  assert.equal(caption.status, 'partial');
  assert.match(figure.scope, /^A block container like blockquote/);
  assert.match(figure.limitations, /list or a table/);
  assert.match(caption.scope, /^A nested block container like div/);
  for (const f of [figure, caption])
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(f[field], han, field);
});

test('address is a partial italic block like div, in English', async () => {
  const { englishFeature } = await localisation();
  const feature = data.features.find(x => x.id === 'html.address');
  assert.equal(feature.status, 'partial');
  const english = englishFeature(feature);
  assert.match(english.scope, /^A block container like div whose text is italic by default/);
  assert.match(english.limitations, /fails like em/);
  assert.equal(english.stage, 'Semantics / rendering');
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(english[field], han, field);
});

test('dl, dt and dd are partial block containers like div, in English', async () => {
  const { englishFeature } = await localisation();
  const [dl, dt, dd] = ['html.dl', 'html.dt', 'html.dd'].map(id => englishFeature(data.features.find(x => x.id === id)));
  for (const f of [dl, dt, dd]) {
    assert.equal(f.status, 'partial');
    assert.equal(f.stage, 'Semantics / rendering');
    assert.match(f.limitations, /end tags cannot be omitted/);
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(f[field], han, field);
  }
  assert.match(dl.scope, /8pt top and bottom margins/);
  assert.match(dd.scope, /24pt start indent/);
});

test('tfoot is a partial table footer of ordinary body rows, in English', async () => {
  const { englishFeature } = await localisation();
  const [table, thead, tfoot] = ['html.table', 'html.thead', 'html.tfoot'].map(id => englishFeature(data.features.find(x => x.id === id)));
  assert.equal(tfoot.status, 'partial');
  assert.match(tfoot.scope, /directly after the nonempty tbody/);
  assert.match(tfoot.limitations, /not repeated on each page fragment/);
  assert.doesNotMatch(table.limitations, /tfoot/);
  assert.doesNotMatch(thead.limitations, /tfoot/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(tfoot[field], han, field);
});

test('menu is a partial list exactly like ul, in English', async () => {
  const { englishFeature } = await localisation();
  const menu = englishFeature(data.features.find(x => x.id === 'html.menu'));
  assert.equal(menu.status, 'partial');
  assert.match(menu.scope, /exactly like ul/);
  assert.match(menu.limitations, /No toolbar-menu semantics/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(menu[field], han, field);
});

test('q is partial inline text in quotation marks, in English', async () => {
  const { englishFeature } = await localisation();
  const q = englishFeature(data.features.find(x => x.id === 'html.q'));
  assert.equal(q.status, 'partial');
  assert.match(q.scope, /curly double quotes/);
  assert.match(q.limitations, /ASCII straight quotes/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(q[field], han, field);
});

test('bdi is partial inline text like span without bidi isolation, in English', async () => {
  const { englishFeature } = await localisation();
  const bdi = englishFeature(data.features.find(x => x.id === 'html.bdi'));
  assert.equal(bdi.status, 'partial');
  assert.match(bdi.scope, /Inline text like span/);
  assert.match(bdi.limitations, /No bidirectional isolation/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(bdi[field], han, field);
});

test('template is partial: dropped with its inert contents, in English', async () => {
  const { englishFeature } = await localisation();
  const template = englishFeature(data.features.find(x => x.id === 'html.template'));
  assert.equal(template.status, 'partial');
  assert.match(template.scope, /inert HTML template fragment/);
  assert.match(template.limitations, /shadowrootmode/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(template[field], han, field);
});

test('dir is partial: ltr accepted on any element, in English', async () => {
  const { englishFeature } = await localisation();
  const dir = englishFeature(data.features.find(x => x.id === 'attr.10'));
  assert.equal(dir.status, 'partial');
  assert.match(dir.scope, /dir="ltr"/);
  assert.match(dir.limitations, /FPDF1103/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(dir[field], han, field);
});

test('script is partial: dropped with its data and never run, in English', async () => {
  const { englishFeature } = await localisation();
  const script = englishFeature(data.features.find(x => x.id === 'html.script'));
  assert.equal(script.status, 'partial');
  assert.match(script.scope, /No script runs/);
  assert.match(script.limitations, /double-escaped/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(script[field], han, field);
});

test('meta is partial: named and Open Graph metadata accepted, in English', async () => {
  const { englishFeature } = await localisation();
  const meta = englishFeature(data.features.find(x => x.id === 'html.meta'));
  assert.equal(meta.status, 'partial');
  assert.match(meta.scope, /viewport/);
  assert.match(meta.limitations, /color-scheme/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(meta[field], han, field);
});

test('link is partial: metadata links accepted, stylesheets still fail, in English', async () => {
  const { englishFeature } = await localisation();
  const link = englishFeature(data.features.find(x => x.id === 'html.link'));
  assert.equal(link.status, 'partial');
  assert.match(link.scope, /canonical/);
  assert.match(link.limitations, /rel=stylesheet/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(link[field], han, field);
});

test('hidden attribute is partial: display:none on report blocks, in English', async () => {
  const { englishFeature } = await localisation();
  const hidden = englishFeature(data.features.find(x => x.id === 'attr.11'));
  assert.equal(hidden.status, 'partial');
  assert.match(hidden.scope, /\[hidden\]\{display:none\}/);
  assert.match(hidden.limitations, /until-found/);
  const display = englishFeature(data.features.find(x => x.id === 'css.display'));
  assert.match(display.limitations, /attr\.11/);
  assert.match(englishFeature(data.features.find(x => x.id === 'selector.attribute')).limitations, /alt\/hidden and/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(hidden[field], han, field);
});

test('href / target / rel is partial: target and hyperlink rel on a ignored, in English', async () => {
  const { englishFeature } = await localisation();
  const row = englishFeature(data.features.find(x => x.id === 'attr.12'));
  assert.equal(row.status, 'partial');
  assert.match(row.scope, /noopener/);
  assert.match(row.limitations, /_new/);
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(row[field], han, field);
});

test('table parts accept the display value they already render, in English', async () => {
  const { englishFeature } = await localisation();
  const display = englishFeature(data.features.find(x => x.id === 'css.display'));
  assert.equal(display.status, 'partial');
  assert.match(display.limitations, /table-header-group on thead/);
  assert.match(display.limitations, /table-footer-group on tfoot/);
  const tfoot = englishFeature(data.features.find(x => x.id === 'html.tfoot'));
  assert.match(tfoot.limitations, /table-row-group is accepted/);
  assert.match(tfoot.limitations, /table-footer-group .* fails FPDF1202/);
  for (const row of [display, tfoot])
    for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(row[field], han, field);
});

test(':root pseudo-class is partial and other pseudo-classes still fail, in English', async () => {
  const { englishFeature } = await localisation();
  const row = englishFeature(data.features.find(x => x.id === 'selector.pseudo'));
  assert.equal(row.status, 'partial');
  assert.equal(row.failure_code, 'FPDF1203');
  assert.match(row.scope, /\(0,1,0\)/);
  assert.match(row.limitations, /:first-child, :nth-child, :lang, :hover/);
  assert.match(englishFeature(data.features.find(x => x.id === 'selector.attribute')).limitations, /pseudo-classes other than :root/);
  for (const field of ['feature', 'scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(row[field], han, field);
});

test('custom properties and var() are partial, failing instead of unsetting, in English', async () => {
  const { englishFeature } = await localisation();
  const row = englishFeature(data.features.find(x => x.id === 'functions.var'));
  assert.equal(row.status, 'partial');
  assert.equal(row.failure_code, 'FPDF1202');
  assert.match(row.scope, /var\(--name, fallback\)/);
  assert.match(row.limitations, /would unset the declaration at computed-value time/);
  assert.match(row.limitations, /MaxCssSyntaxNodes/);
  assert.equal(englishFeature(data.features.find(x => x.id === 'functions.math')).stage, 'Syntax representation only');
  for (const field of ['scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(row[field], han, field);
});

test('non-strict CSS recovery is partial and valid unsupported CSS still fails, in English', async () => {
  const { englishFeature } = await localisation();
  const row = englishFeature(data.features.find(x => x.id === 'syntax.recovery'));
  assert.equal(row.status, 'partial');
  assert.equal(row.failure_code, 'FPDF1201–1204');
  assert.equal(row.stage, 'Semantics / rendering');
  assert.match(row.scope, /WithStrict\(false\)/);
  assert.match(row.scope, /--strict false/);
  assert.match(row.limitations, /position:absolute, :hover, @page, display:flex/);
  assert.match(row.limitations, /MaxCssWarnings/);
  for (const field of ['feature', 'scope', 'limitations', 'stage', 'evidence_level']) assert.doesNotMatch(row[field], han, field);
});
