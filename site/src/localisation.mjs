import { sourceDigest, englishAudit } from './audit-english-list-markers.mjs';
import { diagnosticEnglish } from './diagnostic-english.mjs';

export const auditFields = ['feature','scope','limitations','stage','applicability','planning_bucket','evidence_level','test_reference'];
const han = /[\u3400-\u9fff]/u;
const localeKey = 'factspdf.locale';

export function readLocale(storage) {
  try { return (storage ?? globalThis.localStorage)?.getItem(localeKey) === 'zh' ? 'zh' : 'en'; }
  catch { return 'en'; }
}
export function saveLocale(locale, storage) {
  try { (storage ?? globalThis.localStorage)?.setItem(localeKey, locale === 'zh' ? 'zh' : 'en'); }
  catch { /* Storage can be unavailable; the page must still work. */ }
}

// Stable traversal of display prose only. IDs, status, provenance and code
// examples are never translated. Keep the raw downloadable audit unchanged.
export function collectAuditStrings(data) {
  const values = new Set();
  const add = value => { if (typeof value === 'string' && han.test(value)) values.add(value); };
  for (const feature of data.features) for (const field of auditFields) add(feature[field]);
  add(data.audit_method); add(data.css_inventory_note);
  for (const drift of data.documentation_drift) add(drift.problem);
  return [...values];
}

export async function prepareTranslations(data) {
  const source = collectAuditStrings(data);
  // Bind the compact translation catalogue to the exact ordered source text,
  // not to an engine version or to mutable feature-count heuristics.
  if (!globalThis.crypto?.subtle) throw new Error('This browser cannot verify the English translation catalogue.');
  const bytes = new TextEncoder().encode(JSON.stringify(source));
  const digest = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))]
    .map(value => value.toString(16).padStart(2, '0')).join('');
  if (digest !== sourceDigest || source.length !== englishAudit.length)
    throw new Error('English audit translations need updating for this snapshot.');
  if (englishAudit.some(value => typeof value !== 'string' || !value.trim() || han.test(value)))
    throw new Error('Invalid English audit translation.');
  const translations = new Map(source.map((value, index) => [value, englishAudit[index]]));
  // Some failure_code fields contain prose as well as identifiers. Translate
  // their exact original wording for display, without touching the raw field.
  for (const [original, display] of Object.entries(diagnosticEnglish)) {
    if (!display.trim() || han.test(display) ||
        JSON.stringify(original.match(/FPDF\d{4}(?:–\d{4})?/g)) !==
        JSON.stringify(display.match(/FPDF\d{4}(?:–\d{4})?/g)))
      throw new Error('Invalid English diagnostic translation.');
    translations.set(original, display);
  }
  function translate(value) {
    if (typeof value !== 'string' || !han.test(value)) return value;
    if (!translations.has(value)) throw new Error(`Missing English audit translation: ${value}`);
    return translations.get(value);
  }
  function validateTranslations(snapshot) {
    for (const value of collectAuditStrings(snapshot)) translate(value);
    for (const feature of snapshot.features) translate(feature.failure_code);
  }
  function englishFeature(feature) {
    return { ...feature, ...Object.fromEntries(auditFields.map(field => [field, translate(feature[field])])) };
  }
  validateTranslations(data);
  return { translate, validateTranslations, englishFeature };
}
