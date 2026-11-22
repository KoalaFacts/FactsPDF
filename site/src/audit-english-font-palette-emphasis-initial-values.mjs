import { englishAudit as previous } from './audit-english-font-variant-initial-values.mjs';

// font-language-override, font-variant-emoji and font-palette at normal and text-emphasis and
// text-emphasis-style at none join the initial-value no-ops. Only the CSS inventory note changes
// (233 → 238 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '2958159be4243d83d5aec5dfe827a9b0b4077a7dcd5e81e3ccc5a1f52316fe7b';
const changes = new Map([
  [779, previous[779] + ' font-language-override, font-variant-emoji and font-palette at normal and text-emphasis and text-emphasis-style at none are initial values; the renderer applies no language-specific glyph forms, emoji presentation switches or palettes and draws no emphasis marks, so they are validated and dropped while other values still fail FPDF1202; this took it from 233 to 238.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 233 to 238') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-palette-emphasis-initial-values English audit.');
