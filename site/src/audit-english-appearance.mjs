import { englishAudit as previous } from './audit-english-text-size-adjust.mjs';

// appearance, -webkit-appearance and -moz-appearance accept their keywords and
// are dropped: the renderer draws no native form controls. Only the CSS
// inventory note changes (141 → 142 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '7cbd15e573c4961d28ae7e51e8bef676514e789718b2d567facf1d5e71b398c8';
const changes = new Map([
  [779, previous[779] + ' appearance and its -webkit-/-moz- spellings only switch native form-control styling, which the renderer never draws, and accept a single keyword value, which is validated and dropped; this took it from 141 to 142.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 141 to 142') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete appearance English audit.');
