import { englishAudit as previous } from './audit-english-multicol-initial-values.mjs';

// text-overflow at clip, line-clamp and -webkit-line-clamp at none and box-decoration-break and
// -webkit-box-decoration-break at slice join the initial-value no-ops. Only the CSS inventory
// note changes (251 → 256 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'cc72e860f4fbdb38d77cc4df28fb8a73d4cd7c0303a2ce7d2effa0e2225e5ad6';
const changes = new Map([
  [779, previous[779] + ' text-overflow at clip, line-clamp and -webkit-line-clamp at none and box-decoration-break and -webkit-box-decoration-break at slice are initial values; no box clips its overflow and a box split across pages already paints as slices, so they are validated and dropped while ellipsis, clone and other values still fail FPDF1202; this took it from 251 to 256.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 251 to 256') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete overflow-fragment-initial-values English audit.');
