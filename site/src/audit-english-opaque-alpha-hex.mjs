import { englishAudit as previous } from './audit-english-hidden-border-style.mjs';

// value.color.alphahex: fully opaque #rgba/#rrggbbaa. Scope and limitations
// replace the old strings in place; a new evidence string follows them.
if (previous.length !== 778) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'c18fab3ec8e4824ae14a89835e8f0e698170ad8fa33f32bad2b704cec7a196f2';
const changes = new Map([
  [390, '#rgba and #rrggbbaa are accepted only when fully opaque (alpha f or ff) and paint exactly the same PDF colour as the matching #rgb/#rrggbb; this applies to every property that accepts hex colours.'],
  [391, 'Any alpha other than f/ff (semi-transparent or transparent) still fails FPDF1202; there is no transparency compositing.']
]);
const evidence = 'Current source: 11 tests, including PDF byte equality between opaque alpha hex and the matching #rgb/#rrggbb and rejection of translucent or malformed values. CI, independent review and human approval are verified separately.';
const updated = previous.map((value, index) => changes.get(index) ?? value);
export const englishAudit = Object.freeze([...updated.slice(0, 392), evidence, ...updated.slice(392)]);
if (englishAudit.length !== 779 || englishAudit.some(value =>
  typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete opaque alpha hex English audit.');
