import { englishAudit as previous } from './audit-english-font-synthesis-longhands.mjs';

// border-collapse:separate and border-spacing at one or two zero lengths are accepted at their
// initial value: cells already paint separate border boxes that touch without spacing. The
// css.border-collapse and css.border-spacing rows move from rejected to partial: five strings
// (two scopes, two limitations, one shared evidence line) are inserted where the rows are first
// collected; the inventory note gains two names (370 to 372).
if (previous.length !== 822) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '1d1346560d77e545c58cd21fefee12719ec013df766c109c05ac873c3a837b95';
const rows = [
  'border-collapse at separate (including CSS-wide keywords, !important and any case) is validated and dropped on any element as its initial value; every cell paints its own border box and adjacent border boxes touch without spacing (the separated model), so the PDF bytes equal the undeclared page.',
  'collapse and other values still fail FPDF1202; the collapsing border model is not supported, so adjacent cell borders stay two distinct strokes.',
  'Candidate-source NUnit: the initial value produces PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; cell border boxes touch without spacing, which is the separated model with zero spacing. Exact CI, independent review and main are verified separately.',
  'border-spacing at one or two zero lengths (0, 0pt, 0 0 and so on, including CSS-wide keywords, !important and any case) is validated and dropped on any element; cell border boxes already touch without spacing, so the PDF bytes equal the undeclared page.',
  'Non-zero lengths, percentages, keywords and three or more components still fail FPDF1202; cell spacing is not supported.',
];
const note = previous[818].replace(/\.$/, '') + '; border-collapse:separate and border-spacing at one or two zero lengths are validated and dropped as initial values, because cell border boxes already touch without spacing (the separated model with zero spacing), while collapse and non-zero spacing still fail FPDF1202; this took it from 370 to 372.';
const p = previous;
export const englishAudit = Object.freeze([...p.slice(0, 357), ...rows, ...p.slice(357, 818), note, ...p.slice(819, 822)]);
if (englishAudit.length !== 827 || englishAudit[823] !== note || !note.includes('from 370 to 372') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table border model English audit.');
