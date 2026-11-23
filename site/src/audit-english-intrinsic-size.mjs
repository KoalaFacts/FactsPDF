import { englishAudit as previous } from './audit-english-text-box.mjs';

// The contain-intrinsic-size family and transform-box accept their initial values and are
// dropped. Only the CSS inventory note changes (344 → 350 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '1648f9c7c3b3558fb9025a65c22b40a97f5089300d870babd59be8ee4e6f14b3';
const changes = new Map([
  [779, previous[779] + ' contain-intrinsic-size and its -width, -height, -block-size and -inline-size longhands at none and transform-box at view-box are validated against their initial value and dropped, because size containment and transforms never apply, while other values still fail FPDF1202; this took it from 344 to 350.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 344 to 350') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete intrinsic-size English audit.');
