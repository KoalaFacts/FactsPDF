import { englishAudit as previous } from './audit-english-hsl-hue-units.mjs';

// Nine CSS rows (position, float, clear, overflow, overflow-x, overflow-y,
// visibility, z-index, opacity) move from none to partial and share four new
// strings (scope, limitations, stage, evidence), inserted where the first of
// those rows is collected; the CSS inventory note records 117 → 126 names.
if (previous.length !== 779) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '851c45eb85a980236b62cc96c359c61408df1478ef73e2cb006b40b7ef56e58b';
const inserted = [
  'Accepts only the initial values report normal flow already produces: position:static, float/clear:none, overflow/overflow-x/overflow-y/visibility:visible, z-index:auto, opacity:1 or 100%, and inherit/initial/unset, which compute to them. The declaration is validated and dropped, so the PDF bytes equal the undeclared page and common reset declarations in stylesheets no longer fail.',
  'No positioning, floats, clearing, clipping/scrolling, hiding, stacking order or transparency: any other value (such as position:absolute, float:left, overflow:hidden, visibility:hidden, z-index:1, opacity:0.5) fails with FPDF1202, unmatched selectors are validated just as strictly, and the existing output is protected.',
  'CSS parsing and value validation; normal-flow layout unchanged',
  'Candidate source NUnit: initial values give PDF bytes equal to the undeclared page, cascade order is unaffected, and every other value fails with FPDF1202 without writing output; exact CI, independent review and main are verified separately.'
];
const note = previous[775] + ' On 17 November 2026, nine property names accepted only at their initial value (position, float, clear, overflow, overflow-x, overflow-y, visibility, z-index, opacity) took the count from 117 to 126.';
const updated = previous.map((value, index) => index === 775 ? note : value);
export const englishAudit = Object.freeze([...updated.slice(0, 220), ...inserted, ...updated.slice(220)]);
if (englishAudit.length !== 783 || englishAudit[779] !== note ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete initial-value English audit.');
