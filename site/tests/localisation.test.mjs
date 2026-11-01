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
  assert.match(byId('html.a').limitations, /rejection does not count as support/);
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
  assert.equal(display.last_audited_commit,'052f7a8d3da5f87d9dfb6c7b687dd35b1eb97097');
  assert.match(display.scope,/fixed-column table cells/);
  assert.match(display.limitations,/nowrap prohibits automatic breaks/);
  assert.match(display.limitations,/FPDF1302/);
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
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
      id==='css.margin' ? 'f5aba206ca30c86dca8bd4b465182d5cea436dcc' : 'a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope,/div\/section\/article/);
    assert.match(row.scope,/auto/);
    assert.match(row.limitations,/table-cell p|table-cell/);
    assert.match(row.limitations,/calc|negative/);
    assert.doesNotMatch([row.scope,row.limitations,row.applicability].join(' '),han);
  }
  const margin=englishFeature(data.features.find(row=>row.id==='css.margin'));
  assert.match(margin.limitations,/vertical percentage\/auto/);
  assert.match(margin.limitations,/FPDF1302/);
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
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
    assert.equal(row.last_audited_commit,'f5aba206ca30c86dca8bd4b465182d5cea436dcc');
    assert.equal(row.introduced_release,null);
    assert.match(row.scope,/div\/section\/article/);
    assert.match(row.scope,/outside the box/);
    assert.match(row.limitations,/FPDF1201/);
    assert.match(row.limitations,/FPDF1302/);
    assert.match(row.limitations,/collaps/);
    assert.doesNotMatch(row.scope+' '+row.limitations,han);
  }
  const margin=englishFeature(data.features.find(f=>f.id==='css.margin'));
  assert.equal(margin.last_audited_commit,'f5aba206ca30c86dca8bd4b465182d5cea436dcc');
  assert.match(margin.scope,/margin:0 auto/);
  assert.match(margin.scope,/margin:12pt auto 7pt/);
  assert.match(margin.limitations,/table-cell p/);
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/without repeating margins/);
  assert.equal(data.features.length,418);
  assert.match(data.audit_method,/413 other stable-ID records remain unchanged/);
});

test('numeric font weights are described in English without claiming variable font support',async()=>{
  const {englishFeature,translate}=await localisation();
  const feature=englishFeature(data.features.find(f=>f.id==='css.font-weight'));
  assert.equal(feature.last_audited_commit,'7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750');
  assert.equal(feature.introduced_release,null);
  assert.match(feature.scope,/100–500/);
  assert.match(feature.scope,/600–900/);
  assert.match(feature.limitations,/FPDF1507/);
  assert.match(feature.limitations,/variable font/);
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
    assert.equal(row.last_audited_commit,'03c17c1f4f4b3eff556cb7353f356ca70439f59a');
    assert.match(row.scope+' '+row.limitations,/LTR|RTL/);
    assert.match(row.scope,/PDF/);
    assert.match(row.scope,new RegExp(name+'-start'));
    assert.match(row.scope,new RegExp(name+'-end'));
    assert.match(row.limitations,/FPDF1302/);
    assert.doesNotMatch(row.scope+' '+row.limitations,han);
  }
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
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
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
  assert.match(translate(data.css_inventory_note),/Scoped display:block\/none/);
});

test('bounded CSS list markers have complete English source-bound translations',async()=>{
  const {englishFeature,translate}=await localisation();
  const entry=englishFeature(data.features.find(f=>f.id==='css.list-style-type'));
  assert.equal(entry.status,'partial');
  assert.equal(entry.last_audited_commit,'7665f2fbafa34e14f9d792e7e7728d67f268e0e3');
  assert.match(entry.scope,/hollow-circle/);
  assert.match(entry.scope,/solid-square/);
  assert.doesNotMatch(entry.limitations,/circle\/square/);
  assert.match(entry.scope,/lower-roman/);
  assert.match(entry.scope,/Actual PDF/);
  assert.match(entry.limitations,/3999/);
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
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
 assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
 assert.doesNotMatch(row.scope+' '+row.limitations,han);
});

test('rounded report panel audit translates vector paint and explicit limits',async()=>{
 const {englishFeature,translate}=await localisation();
 const f=englishFeature(data.features.find(x=>x.id==='css.border-radius'));
 assert.equal(f.status,'partial');assert.match(f.scope,/Bezier/);
 assert.match(f.scope,/clockwise/);assert.match(f.scope,/page/);assert.match(f.limitations,/elliptical/);assert.match(f.limitations,/longhands/);
 assert.match(f.limitations,/FPDF1201\/1202\/1302\/1401/);
 assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
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
  assert.match(translate(data.css_inventory_note),/78 scoped CSS property names/);
});
