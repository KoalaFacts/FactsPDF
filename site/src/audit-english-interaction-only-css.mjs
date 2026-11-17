import { englishAudit as previous } from './audit-english-none-initial-no-ops.mjs';

// cursor, pointer-events, user-select and resize are accepted with any single
// keyword value and dropped: they only affect pointer interaction on a screen.
// Only the CSS inventory note changes (131 → 135 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '9324ca3ed6dcb04deacabd6943f525c1c4008a00f113a233821413659ba70a53';
const changes = new Map([
  [779, previous[779] + ' Four names that only affect pointer interaction on a screen and have no effect on a PDF page (cursor, pointer-events, user-select, resize) accept a single keyword value, which is validated and dropped; this took it from 131 to 135.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 131 to 135') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete interaction-only English audit.');
