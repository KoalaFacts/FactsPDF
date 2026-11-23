import { englishAudit as previous } from './audit-english-stale-resets.mjs';

// transform-origin and -webkit-transform-origin accept a well-formed origin, validated and dropped
// because only transform:none is accepted. The css.transform-origin row moves from rejected to
// partial: three strings are inserted where it is first collected; the inventory note gains two
// names (364 to 366).
if (previous.length !== 819) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '6208badd87d2037a3ff8db29e0fee7fd34262d7329247562ad49b7b9e09e9604';
const row = [
  'transform-origin (and its -webkit- prefix) at one or two positions (left, center, right, top, bottom, a length, a percentage or 0) plus an optional third length, or a CSS-wide keyword (including !important and any case), is validated and dropped on any element; since only transform:none is accepted, no box is ever transformed and the PDF bytes equal the undeclared page.',
  'Two horizontal or two vertical keywords, a length after a vertical keyword, bare non-zero numbers, angles, a percentage or keyword as the third value, four values, commas and functions still fail FPDF1202; transforms are unsupported.',
  'Candidate-source NUnit: well-formed values produce PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; only transform:none is accepted, so no box is ever transformed. Exact CI, independent review and main are verified separately.',
];
const note = previous[815].replace(/\.$/, '') + '; well-formed transform-origin and -webkit-transform-origin values are validated and dropped, because only transform:none is accepted and no box is ever transformed, while other values still fail FPDF1202; this took it from 364 to 366.';
const p = previous;
export const englishAudit = Object.freeze([...p.slice(0, 373), ...row, ...p.slice(373, 815), note, ...p.slice(816, 819)]);
if (englishAudit.length !== 822 || englishAudit[818] !== note || !note.includes('from 364 to 366') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete transform-origin English audit.');
