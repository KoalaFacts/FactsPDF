import { englishAudit as previous } from './audit-english-outline-none-reset.mjs';

// normalize.css v3/v4 writes -ms-text-size-adjust next to the -webkit- spelling;
// -ms- and -moz-text-size-adjust are aliases of text-size-adjust and dropped.
// Only the CSS inventory note changes (176 → 178 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'c8f07dd6015f991365a82aa6fb25315f0116c4b8203626fa37b69eb98b636d85';
const changes = new Map([
  [779, previous[779] + ' -ms-text-size-adjust and -moz-text-size-adjust (normalize.css v3/v4) are prefixed aliases of text-size-adjust with the same values and are validated and dropped; this took it from 176 to 178.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 176 to 178') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete text-size-adjust-prefixes English audit.');
