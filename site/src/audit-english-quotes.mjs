import { englishAudit as previous } from './audit-english-motion-path-initial-letter.mjs';

// quotes at auto, none, match-parent or pairs of strings is validated and dropped: no q element and
// no open-quote/close-quote content is accepted, so no quotation mark is ever inserted. The
// css.quotes row moves from rejected to partial: three strings (scope, limitations, evidence) are
// inserted where the row is first collected; the inventory note gains one name (379 to 380).
if (previous.length !== 832) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a8d38ed983b943b48573394a580bd742de39cfdea10453dcace1679746035d3b';
const rows = [
  'quotes at auto, none, match-parent or pairs of strings (including CSS-wide keywords, !important and any case) is validated and dropped on any element; no q element and no open-quote/close-quote generated content is accepted, so no quotation mark is ever inserted and the PDF bytes equal the undeclared page.',
  'A single or odd number of strings, other keywords and functions still fail FPDF1202; the q element and quote generated content are still unsupported.',
  'Candidate-source NUnit: auto, none, match-parent and string pairs produce PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; no q element or quote generated content is accepted. Exact CI, independent review and main are verified separately.',
];
const note = previous[828].replace(/\.$/, '') + '; quotes at auto, none, match-parent or pairs of strings is validated and dropped, because no q element or quote generated content is accepted and no quotation mark is ever inserted, while other values still fail FPDF1202; this took it from 379 to 380.';
const p = previous;
export const englishAudit = Object.freeze([...p.slice(0, 356), ...rows, ...p.slice(356, 828), note, ...p.slice(829, 832)]);
if (englishAudit.length !== 835 || englishAudit[831] !== note || !note.includes('from 379 to 380') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete quotes English audit.');
