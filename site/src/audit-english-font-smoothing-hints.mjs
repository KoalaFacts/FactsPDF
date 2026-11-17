import { englishAudit as previous } from './audit-english-print-color-adjust.mjs';

// text-rendering, -webkit-font-smoothing and -moz-osx-font-smoothing accept
// their keywords and are dropped: they only steer a screen rasteriser. Only the
// CSS inventory note changes (137 → 140 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '83f3e838189cfe7ddbab7f8d3ed41e595830e67eb47d1f570d5a00f96d13ee50';
const changes = new Map([
  [779, previous[779] + ' Three more names that only control screen glyph rasterisation and have no effect on a PDF file (text-rendering, -webkit-font-smoothing, -moz-osx-font-smoothing) accept a single keyword value, which is validated and dropped; this took it from 137 to 140.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 137 to 140') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-smoothing-hints English audit.');
