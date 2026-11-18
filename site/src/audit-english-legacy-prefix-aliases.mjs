import { englishAudit as previous } from './audit-english-compositing-initial-values.mjs';

// The engine-prefixed spellings of border-radius, box-shadow, transform, filter, clip-path and
// backdrop-filter become aliases of the standard names. Only the CSS inventory note changes
// (184 → 195 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd1ec6945da58ff5e5118bf2a6ccc9d86d64c6bbf4e02c5249590690c8a93fa64';
const changes = new Map([
  [779, previous[779] + ' -webkit-/-moz-border-radius, -webkit-/-moz-box-shadow, -webkit-/-moz-/-ms-/-o-transform, -webkit-filter, -webkit-clip-path and -webkit-backdrop-filter are the legacy engine spellings of the standard properties and share their values, cascade slot, applicability and errors; this took it from 184 to 195.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 184 to 195') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete legacy-prefix-aliases English audit.');
