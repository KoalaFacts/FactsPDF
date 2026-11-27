import { englishAudit as previous } from './audit-english-root-pseudo-class.mjs';

// The @page row moves to partial: its scope and limitation (indices 620 and 621, used by no other
// row) are replaced and its new evidence string is inserted at 622. The @media limitation (index
// 618) no longer lists @page among the rejected at-rules.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '3fdf1edd14866a5c47db9b068e86550bdf367a8feed2fa62d2c3ac2020e7e6b0';
const changes = new Map([
  [618, 'Only one print or all media type per rule. Screen, media conditions, comma lists, nested @media, @supports, @container and @import reject with FPDF1204 (@page and @keyframes are allowed inside); malformed or unmatched declarations still reject with FPDF1202. Not full CSS Media Queries.'],
  [620, '@page at the top level of an embedded <style> or inside @media print/all: size (the A5/A4/A3/B5/B4/JIS-B5/JIS-B4/letter/legal/ledger keywords with or without portrait/landscape, an orientation alone, or one or two physical lengths), margin and its four longhands (real asymmetric margins that move, narrow and repaginate the body), and the six @top-left/center/right and @bottom-left/center/right margin boxes: content strings, counter(page), counter(pages) and none, with color/font-size/font-weight/font-style inherited from the html element, drawn through the structured page furniture; the rules cascade by !important and source order.'],
  [621, 'Page selectors (:first/:left/:right/:blank), named pages, corner and side margin boxes, other at-rules and nested rules inside @page fail FPDF1204; other properties in @page or a margin box fail FPDF1201; other size forms, percentage or auto margins, margins that leave no page area, other counters or counter styles, and counters()/attr()/string() fail FPDF1202; an explicit WithPage/CLI paper size or margin that disagrees with @page, or margin boxes combined with header/footer text, page numbers or structured page furniture, fail FPDF1206. Margin boxes are single-line, never wrapped or shrunk; fonts come from the writer\'s font chains, not font-family. Against Chrome, page sizes differ by at most 0.51pt (Chrome rounds to 1/300 inch) and margin-box baselines by at most 0.8pt.'],
]);
const entries = previous.map((value, index) => changes.get(index) ?? value);
entries.splice(622, 0,
  'Source code and NUnit byte, geometry and failure cases; real CLI PDFs measured against Chrome 156 page.pdf for page size, margins and margin-box positions; CI and independent review confirmed separately.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 877 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  englishAudit[623] !== previous[622] || englishAudit.at(-1) !== previous.at(-1) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete @page English audit.');
