import { englishAudit as previous } from './audit-english-scroll-offsets.mjs';

// scroll-snap-type, scroll-snap-align and scroll-snap-stop accept their keyword forms and are
// dropped. Only the CSS inventory note changes (305 → 308 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a0101c43083e0477ebe085f1cc69d9ab47327aae8ccc2b6df5402cc1c5c5ba8f';
const changes = new Map([
  [779, previous[779] + ' scroll-snap-type, scroll-snap-align and scroll-snap-stop only decide where a scroll container comes to rest, and a PDF page never scrolls, so their keyword forms are validated and dropped while other values or orders still fail FPDF1202; this took it from 305 to 308.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 305 to 308') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete scroll-snap English audit.');
