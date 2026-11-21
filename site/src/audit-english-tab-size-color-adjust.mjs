import { englishAudit as previous } from './audit-english-font-initial-values.mjs';

// color-adjust becomes the deprecated alias of print-color-adjust, and tab-size (with
// -moz-tab-size) accepts a non-negative number or length and is dropped.
// Only the CSS inventory note changes (210 → 213 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'fc99a0efe114d7a2e9b8564babea71e228970633c5a5c2839c2bc27296d1db80';
const changes = new Map([
  [779, previous[779] + ' color-adjust is the deprecated alias of print-color-adjust, and tab-size (with -moz-tab-size) accepts a non-negative number or length that is validated and dropped, because no tab stop reaches a PDF page (collapsed tabs become spaces and preserved tabs fail FPDF1305), while other values fail FPDF1202; this took it from 210 to 213.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 210 to 213') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete tab-size-color-adjust English audit.');
