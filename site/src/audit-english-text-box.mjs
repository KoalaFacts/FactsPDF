import { englishAudit as previous } from './audit-english-screen-ui.mjs';

// The text box, paint-order and math layout properties accept their initial values and are
// dropped. Only the CSS inventory note changes (337 → 344 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a48f22d4b49dff3ad2a4459765f673cce63490f781fb586c7689f056404a4d7f';
const changes = new Map([
  [779, previous[779] + ' text-wrap-style and text-box-edge at auto, text-box-trim at none and text-box, paint-order, math-style and math-shift at normal are validated against their initial value and dropped, because lines break greedily, line boxes are never trimmed, glyphs carry no stroke and no MathML layout runs, while other values still fail FPDF1202; this took it from 337 to 344.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 337 to 344') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete text box English audit.');
