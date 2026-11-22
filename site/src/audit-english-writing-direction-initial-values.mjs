import { englishAudit as previous } from './audit-english-font-palette-emphasis-initial-values.mjs';

// direction:ltr, unicode-bidi:normal, writing-mode:horizontal-tb, text-orientation:mixed and
// text-combine-upright:none join the initial-value no-ops. Only the CSS inventory note changes
// (238 → 243 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '3e63e0974d431d9f2f03f8bc9ea42ed1de99ff28be7913ffa1d9c14134e076a3';
const changes = new Map([
  [779, previous[779] + ' direction at ltr, unicode-bidi at normal, writing-mode at horizontal-tb, text-orientation at mixed and text-combine-upright at none are initial values; the renderer lays out only left-to-right horizontal text and never reorders, rotates or combines glyphs, so they are validated and dropped while rtl, isolate, vertical-rl and other values still fail FPDF1202; this took it from 238 to 243.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 238 to 243') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete writing-direction-initial-values English audit.');
