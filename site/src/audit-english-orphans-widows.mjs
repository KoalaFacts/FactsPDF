import { englishAudit as previous } from './audit-english-translucent-colours.mjs';

// css.orphans, css.widows and pdf.orphans move from none to partial and share one scope, one
// limitations and one evidence string, inserted where css.orphans is first collected (index 423).
// pdf.orphans' former scope and limitations (indices 831 and 832), used by no other row, are
// removed, and the CSS inventory note (index 873) records 384 → 386 names.
if (previous.length !== 877) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '19a1405b50b59a01109e4deae93476119d331a27a1248dfaf9e8823f2a27d839';
const entries = [...previous];
entries.splice(873, 1, previous[873].replace(/\.$/, '') + '; orphans and widows are inherited positive integers, 2 where undeclared, that set how many lines of a paragraph stay at the bottom of a page and start the next, at the breaks Chrome prints, and non-positive or non-integer values fail FPDF1202; this took it from 384 to 386.');
entries.splice(831, 2);
entries.splice(423, 0,
  'orphans and widows are inherited positive integers on any block (CSS-wide keywords, !important, stylesheet or style attribute), 2 (the CSS initial value) where undeclared, acting on the lines of paragraphs, headings, pre, anonymous panel text, list items and cell paragraphs of table rows that split across pages: at a page break at least orphans lines stay at the bottom of the page and at least widows lines start the next, at the breaks Chrome prints. When the page already holds content, a paragraph that cannot keep its orphans moves whole to the next page (with its panel\'s leading padding/border or list marker); otherwise it breaks where the page ends. The break moves earlier for the widows but never below the orphans, and the orphans are kept when both cannot be. A break-after:avoid heading group moves only when the paragraph after it cannot keep its orphans.',
  'The initial value 2 changes the earlier line-by-line breaks: a page no longer ends with a paragraph\'s lone first line or starts with its lone last line, and a break-after:avoid heading no longer moves its whole group when two lines of its paragraph fit after it (orphans:1;widows:1 restores the old breaks). A later cell paragraph that cannot keep its orphans moves to the next page within its cell; one that starts its cell\'s fragment moves the whole row. Table rows that fit a fresh page still move whole (an existing difference from Chrome). Values above 32767 compute to 32767 as in Chrome; 0, negative, non-integer, length, other keyword and calc() values fail FPDF1202. Validated and dropped on inline text, br, img, hr and col. No multi-column or other fragmentation containers. Errors do not overwrite the previous PDF.',
  'Candidate-source NUnit: Chrome-printed page-split comparisons (paragraphs, lists, headings and 13 table-cell cases), a 4,050-layout line-order sweep, byte equality and FPDF1202 rejections; the author compared synthetic pages with Chrome\'s printed breaks one by one. Exact CI, independent review, the output change against main (Parity-Expect: orphans-widows-initial-2) and main are verified separately.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 878 || englishAudit[422] !== previous[422] || englishAudit[426] !== previous[423] ||
  englishAudit[834] !== previous[833] || !englishAudit[874].includes('from 384 to 386') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete orphans/widows English audit.');
