import { englishAudit as prior } from './audit-english-current-colour.mjs';

// Only one existing CSS feature record and the two global methodology strings
// were changed. Preserve the other 746 exact, reviewed translations.
if (prior.length !== 750) throw new Error('Reconcile currentColor English source catalogue first.');
export const sourceDigest = 'd2821b0b5490ba63d7d5a58568691069248db1c4cd7cbaba69f8f25b3c9cc0ac';
const additions = Object.freeze({
  191: prior[191] + ' Six additional LTR-only logical border shorthands (border-inline, border-block, border-inline-start/end and border-block-start/end) now map to actual left/right/top/bottom PDF strokes, preserving width/style/opaque colour, important cascade, inheritance and multi-page paint.',
  192: prior[192] + ' Logical border shorthands do not implement RTL, vertical writing or logical border-width/style/colour component longhands; no new element applicability is implied.',
  745: prior[745] + ' On 4 November 2026, source 6916ef5da96068c167690ef3a0b5f1c94d41f255 adds six LTR logical border shorthand spellings to one existing css.border record; 418 feature IDs remain unchanged, accepted CSS names rise from 83 to 89. A 44-entry multi-page report has an independent physical-border oracle, true PDF and Native AOT qpdf/Poppler comparison, CLI, explicit font and failure-recovery source tests; CI, independent review, human approval, private candidate and customer grants are separate.',
  746: prior[746] + ' Six additional LTR-only border shorthand names are now accepted, with actual PDF stroke, geometry, cascade and pagination: border-inline, border-block and inline/block start/end. The accepted CSS name count is now 89 (previously 83) while all 418 capability IDs remain stable. No RTL, vertical writing or logical border component longhands.'
});
export const englishAudit = Object.freeze(prior.map((value, index) =>
  Object.hasOwn(additions,index) ? additions[index] : value));
if (englishAudit.length !== 750 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete LTR logical border English audit.');
