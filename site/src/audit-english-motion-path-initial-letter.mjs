import { englishAudit as previous } from './audit-english-table-cell-initial-values.mjs';

// offset-path:none, offset-rotate and offset-anchor at auto, initial-letter:normal and
// text-emphasis-color:currentcolor are accepted at their initial value: no box follows a motion
// path, no drop cap is drawn and no emphasis mark is painted. Only the CSS inventory note changes
// (374 to 379 names); no feature row moves.
if (previous.length !== 832) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '077823822fc70b3ee64ec99ee2e987fad79fff74796e50f91a273b1519168660';
const changes = new Map([
  [828, previous[828].replace(/\.$/, '') + '; offset-path:none, offset-rotate and offset-anchor at auto, initial-letter:normal and text-emphasis-color:currentcolor are validated and dropped as initial values, because no box is placed along a motion path, no drop cap is drawn and no emphasis mark is painted, while other values still fail FPDF1202; this took it from 374 to 379.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 832 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[828].includes('from 374 to 379') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete motion path and initial-letter English audit.');
