import { englishAudit as previous } from './audit-english-intrinsic-size.mjs';

// font-kerning accepts auto or none and is dropped: its feature row moves from rejected to
// partial (three new strings inserted where the row is first collected) and the CSS inventory
// note gains one name (350 → 351).
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a01e0857cb65b34345d83f4e539c1a57c999c0a196847a697c88976641b894c8';
const row = [
  'font-kerning at auto or none (including CSS-wide keywords, !important and any case) is validated and dropped on any element; the renderer never reads kern or GPOS data and never kerns a glyph pair, so auto (the renderer\'s choice) and none (no kerning) are already honoured and the PDF bytes equal the undeclared page.',
  'normal asks for kerning and still fails FPDF1202; other keywords, several components or numbers fail FPDF1202 as well; no kerning is provided.',
  'Candidate-source NUnit: auto/none produce PDF bytes identical to the undeclared page, normal and other values fail FPDF1202 and write no output; the source reads no kern/GPOS data. Exact CI, independent review and main are verified separately.',
];
const note = previous[779] + ' font-kerning at auto or none is validated and dropped, because the renderer never kerns, while normal and other values still fail FPDF1202; this took it from 350 to 351.';
const updated = previous.map((value, index) => index === 779 ? note : value);
export const englishAudit = Object.freeze([...updated.slice(0, 282), ...row, ...updated.slice(282)]);
if (englishAudit.length !== 786 || englishAudit[782] !== note || !note.includes('from 350 to 351') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-kerning English audit.');
