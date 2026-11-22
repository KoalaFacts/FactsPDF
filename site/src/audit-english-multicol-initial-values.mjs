import { englishAudit as previous } from './audit-english-vertical-align-baseline-reset.mjs';

// column-count, column-width and columns at auto, column-gap at normal, column-span,
// column-rule and column-rule-style at none and column-fill at balance join the initial-value
// no-ops. Only the CSS inventory note changes (243 → 251 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'ef5e004f19de6a2d602506e1011bfe662aa6fbb645a8ad4570d12ef194c6f85f';
const changes = new Map([
  [779, previous[779] + ' column-count, column-width and columns at auto, column-gap at normal, column-span, column-rule and column-rule-style at none and column-fill at balance are initial values; the renderer always lays out a single column, so they are validated and dropped while column-count:2 and other values still fail FPDF1202; this took it from 243 to 251.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 243 to 251') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete multicol-initial-values English audit.');
