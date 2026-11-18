import { englishAudit as previous } from './audit-english-box-sizing-prefixes.mjs';

// Outside report panels no outline is ever painted, so outline resets
// (outline:none/0, outline-style:none/initial/unset, widths, colours, offsets)
// are validated and dropped there; a style that could paint still fails.
// Only the css.outline limitations text changes; no status or count moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '61add9533f0009b5d2868cd9928acfd8d47e02c0ee247935ce03823d50e7eae8';
const changes = new Map([
  [270, 'Widths are zero or 0.2–6pt, offset 0–12pt in physical pt/px/mm/cm/in/pc/q only. Solid/dashed/dotted/none and opaque currentColor are supported. Rounded outlines, negatives, percentages, calc(), alpha, images and general CSS Outline are rejected without overwriting the PDF. Outside panels (list, table, image, paragraph and inline targets) no outline is ever painted, so resets that make none (outline:none/0, outline-style:none/initial/unset, widths, colours, offsets) are validated and dropped with unchanged PDF bytes; a solid/dashed/dotted/inherit style that could paint there still fails FPDF1201.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[270].includes('outline:none/0') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete outline-none-reset English audit.');
