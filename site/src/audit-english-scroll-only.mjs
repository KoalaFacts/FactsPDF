import { englishAudit as previous } from './audit-english-animation.mjs';

// scroll-behavior, overscroll-behavior (and its -x/-y/-inline/-block longhands),
// overflow-anchor, -webkit-overflow-scrolling and scrollbar-width accept a single
// keyword and are dropped: a PDF page never scrolls. Only the CSS inventory note
// changes (160 → 169 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '4bfb2caf32353de7dbabdcfb440ae5ccefed802ac196ab89bc85ad070ef32812';
const changes = new Map([
  [779, previous[779] + ' scroll-behavior, overscroll-behavior and its -x, -y, -inline and -block longhands, overflow-anchor, -webkit-overflow-scrolling and scrollbar-width only steer a scroll container, and a PDF page never scrolls (overflow accepts only visible), so a single keyword value is validated and dropped; this took it from 160 to 169.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 160 to 169') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete scroll-only English audit.');
