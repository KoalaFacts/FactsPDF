import { englishAudit as previous } from './audit-english-color-scheme-hints.mjs';

// clip-path:none, backdrop-filter:none, mix-blend-mode:normal and isolation:auto
// join the initial-value no-ops: normal flow clips, blends and isolates nothing.
// Only the CSS inventory note changes (180 → 184 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'c22089ff587f058a9548cff4eefea27959d918f8c90801b6f6b963c4c812fc03';
const changes = new Map([
  [779, previous[779] + ' clip-path and backdrop-filter at none, mix-blend-mode:normal and isolation:auto are initial values; normal flow clips, blends and isolates nothing, so they are validated and dropped while other values still fail FPDF1202; this took it from 180 to 184.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 180 to 184') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete compositing-initial-values English audit.');
