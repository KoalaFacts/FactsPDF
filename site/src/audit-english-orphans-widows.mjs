import { englishAudit as previous } from './audit-english-table-row-break-inside.mjs';

// css.orphans, css.widows and pdf.orphans move from none to partial and share one scope, one
// limitations and one evidence string, inserted where css.orphans is first collected (index 421).
// pdf.orphans' former scope and limitations (indices 828 and 829), used by no other row, are
// removed, and the CSS inventory note (index 870) records 384 → 386 names.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'f85cc10f1f49152f8e46847b648504e8a1136be8b05359c9fe1c132920bd4a5f';
const entries = [...previous];
entries.splice(870, 1, previous[870].replace(/\.$/, '') + '; orphans and widows are inherited positive integers that set how many lines of a paragraph stay at the bottom of a page and start the next, at the breaks Chrome prints, while paragraphs without them keep the existing line-by-line breaks and non-positive or non-integer values fail FPDF1202; this took it from 384 to 386.');
entries.splice(828, 2);
entries.splice(421, 0,
  'orphans and widows are inherited positive integers on any block (CSS-wide keywords, !important, stylesheet or style attribute) acting on the lines of paragraphs, headings, pre, anonymous panel text and list items: at a page break at least orphans lines stay at the bottom of the page and at least widows lines start the next, at the breaks Chrome prints. When the page already holds content, a paragraph that cannot keep its orphans moves whole to the next page (with its panel\'s leading padding/border, list marker or break-after:avoid heading group); otherwise it breaks where the page ends. The break moves earlier for the widows but never below the orphans, and the orphans are kept when both cannot be.',
  'Paragraphs that no orphans/widows declaration reaches do not take CSS\'s initial value 2 and keep the existing line-by-line breaks (orphans:1;widows:1 is byte-identical to them), so they differ from Chrome\'s default pagination and existing reports are unchanged. Values above 32767 compute to 32767 as in Chrome; 0, negative, non-integer, length, other keyword and calc() values fail FPDF1202; a table row that splits across pages with a declared multi-line cell paragraph fails FPDF1302. Validated and dropped on inline text, br, img, hr and col. A complete heading group that fits a fresh page still moves whole (the existing heading keep). No multi-column or other fragmentation containers. Errors do not overwrite the previous PDF.',
  'Candidate-source NUnit: 29 Chrome-printed page-split comparisons, a 4,050-layout line-order sweep, byte equality with the undeclared page, and FPDF1202/FPDF1302 rejections that write no output; 105 further synthetic pages match Chrome\'s printed breaks. Exact CI, independent review and main are verified separately.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 875 || englishAudit[420] !== previous[420] || englishAudit[424] !== previous[421] ||
  englishAudit[831] !== previous[830] || !englishAudit[871].includes('from 384 to 386') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete orphans/widows English audit.');
