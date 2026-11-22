import { englishAudit as previous } from './audit-english-font-hint-keywords.mjs';

// The legacy text-decoration-skip (with its -webkit- alias) accepts none, auto or objects and is dropped.
// Only the CSS inventory note changes (222 → 224 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '5df45ca2ac73211b0863d1bfa80baa4831b3913a419c7fbf4679a0e02e2dd596';
const changes = new Map([
  [779, previous[779] + ' The legacy text-decoration-skip (with its -webkit- alias, which normalize.css 7 and Bootstrap 4 write as objects on links) accepts none, auto or objects: no image or other atomic inline can sit inside a line, so it is validated and dropped while ink, spaces, edges and other values fail FPDF1202; this took it from 222 to 224.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 222 to 224') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete text-decoration-skip English audit.');
