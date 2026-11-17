import { englishAudit as previous } from './audit-english-relative-borders.mjs';

// Translate the re-audited value.border.transparent row: its scope and
// limitations replace the two old strings in place and its new evidence string
// is inserted after them; every other reviewed English translation is kept.
if (previous.length !== 776) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '01853b60925b27084e80fc0309c06500d88b61f7137b1507ccb4efd5ebd12cdd';
const changes = new Map([
  [400, 'border-color, border-*-color, the 1–4 value border-color shorthand and the border/border-* shorthands accept transparent: the edge keeps its width in layout but paints nothing; the background is still painted to the border box and shows through that edge.'],
  [401, 'Only on elements that already support borders. outline/outline-color still reject transparent (FPDF1202). A rounded panel needs all four sides transparent or all opaque; a mix fails FPDF1201. Semi-transparent colours are unsupported.']
]);
const evidence = 'Current source: 7 tests, including PDF byte equality between a transparent border and equal padding, omission of a single side, shorthands, inheritance, rounded panels and outline rejection. CI, independent review and human approval are verified separately.';
const updated = previous.map((value, index) => changes.get(index) ?? value);
export const englishAudit = Object.freeze([...updated.slice(0, 402), evidence, ...updated.slice(402)]);
if (englishAudit.length !== 777 || englishAudit.some(value =>
  typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete transparent border-color English audit.');
