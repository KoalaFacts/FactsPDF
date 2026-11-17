import { englishAudit as previous } from './audit-english-font-smoothing-hints.mjs';

// text-size-adjust and -webkit-text-size-adjust accept auto, none or a
// non-negative percentage and are dropped: a PDF page is never inflated. Only
// the CSS inventory note changes (140 → 141 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0bf71279209f9fc356c19a2181b1a7410fa04f8a2375ee8d83a923e47811bff6';
const changes = new Map([
  [779, previous[779] + ' text-size-adjust and its common prefixed spelling -webkit-text-size-adjust only control automatic font inflation in mobile browsers and accept auto, none or a non-negative percentage, which is validated and dropped; this took it from 140 to 141.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 140 to 141') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete text-size-adjust English audit.');
