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
  assert.match(byId('css.white-space').limitations, /nowrap/);
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
