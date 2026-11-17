import { englishAudit as previous } from './audit-english-transparent-border.mjs';

// Re-audit three value rows that the source already supports (background and
// text currentColor, dashed/dotted borders): their scope and limitations
// replace the old strings in place and one shared evidence string is inserted.
if (previous.length !== 777) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '63394686105d562d0e858553eaab53d56d290d7ef37e1d041f97225bd79695f0';
const changes = new Map([
  [396, 'On report blocks that already support backgrounds, background-color: currentColor and single-colour background: currentColor fill the real PDF background with the element\'s own final computed text colour.'],
  [397, 'Only on the existing applicable elements; with inherit the colour is recomputed from the receiving element\'s own text colour; gradients and semi-transparent colours are unsupported.'],
  [403, 'color: currentColor takes the parent\'s computed opaque text colour for real PDF text paint.'],
  [404, 'Equivalent to inheriting the parent colour; semi-transparent colours are unsupported.'],
  [409, 'border-style and the border/border-* shorthands accept dashed and dotted, painting real PDF dashed/dotted strokes; across pages the top and bottom edges appear only on the first/last fragment.'],
  [410, 'No double/hidden/groove/ridge/inset/outset; strokes too thin or edges too short fail FPDF1302; rounded boxes still require solid borders.']
]);
const evidence = 'Already implemented in current source and covered by existing tests; this pass checked source and tests. CI, independent review and human approval are verified separately.';
const updated = previous.map((value, index) => changes.get(index) ?? value);
export const englishAudit = Object.freeze([...updated.slice(0, 398), evidence, ...updated.slice(398)]);
if (englishAudit.length !== 778 || englishAudit.some(value =>
  typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete stale value-row English audit.');
