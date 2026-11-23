import { englishAudit as previous } from './audit-english-table-border-model.mjs';

// empty-cells:show and caption-side:top are accepted at their initial value: empty cells already
// paint their background and borders, and no caption element is accepted. The css.empty-cells and
// css.caption-side rows move from rejected to partial: five strings (two scopes, two limitations,
// one shared evidence line) are inserted where the rows are first collected; the inventory note
// gains two names (372 to 374).
if (previous.length !== 827) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '759d02b0e69b3d2f9b1d7741c33e30186a509f66aec129f2654c9050a203087b';
const rows = [
  'empty-cells at show (including CSS-wide keywords, !important and any case) is validated and dropped on any element as its initial value; empty cells already paint their background and borders, so the PDF bytes equal the undeclared page.',
  'hide and other values still fail FPDF1202; hiding empty cells is not supported.',
  'Candidate-source NUnit: the initial value produces PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; empty cells paint their background and borders, and no caption element is accepted. Exact CI, independent review and main are verified separately.',
  'caption-side at top (including CSS-wide keywords, !important and any case) is validated and dropped on any element as its initial value; no caption element is accepted, so the caption side never applies and the PDF bytes equal the undeclared page.',
  'bottom and other values still fail FPDF1202; the caption element itself is still unsupported.',
];
const note = previous[823].replace(/\.$/, '') + '; empty-cells:show and caption-side:top are validated and dropped as initial values, because empty cells already paint and no caption element is accepted, while other values still fail FPDF1202; this took it from 372 to 374.';
const p = previous;
export const englishAudit = Object.freeze([...p.slice(0, 362), ...rows, ...p.slice(362, 823), note, ...p.slice(824, 827)]);
if (englishAudit.length !== 832 || englishAudit[828] !== note || !note.includes('from 372 to 374') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table cell initial-value English audit.');
