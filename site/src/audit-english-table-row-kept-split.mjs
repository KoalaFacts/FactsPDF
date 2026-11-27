import { englishAudit as previous } from './audit-english-table-row-break-inside.mjs';

// A kept table row that must split now paginates like Chrome instead of failing. The break-inside
// row's limitation (index 406) and the page-break-inside row's limitation (index 418) are replaced
// in place; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0fba5ce7a8a30c050f2d3558180e7827053eb74f3eddd73ad0bef4ad71d175df';
const entries = [...previous];
entries.splice(406, 1,
  'Only top-level ordinary div/section/article/blockquote with 1–8 direct nonempty p/pre/h1–h6 children. Exception: table tr and thead accept auto/avoid and render byte-identical to the page without it (a row that fits a fresh fragment with the repeated header, and the header, never split); a kept row that must split (taller than a fresh fragment, or split only for the table\'s top decoration) breaks before itself as Chrome does (the whole table moves for the first body row) and then continues from the fresh page at complete lines; a last row split only for the table\'s bottom padding/border still fails FPDF1302. Nested boxes, lists, tbody/tfoot/td/th, images, internal page breaks, fixed/min-height, over-one-page content and general Paged Media are unsupported. Inapplicable FPDF1201, invalid FPDF1202 and unplaceable FPDF1302 preserve the previous PDF.');
entries.splice(418, 1,
  'Same strict scope as break-inside: no lists, images, nested/complex children, internal forced breaks, fixed/min-height or cards taller than one page. Only auto/avoid values. In tables only tr and thead accept it (byte-identical to the page without it; a row that must split starts on a fresh page and continues there as in Chrome, and a last row split only for the bottom decoration fails FPDF1302), while tbody/tfoot/td/th still fail FPDF1201. FPDF1201, FPDF1202 and FPDF1302 reject unsupported input without overwriting the prior PDF.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[407] !== previous[407] || englishAudit[419] !== previous[419] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete kept table-row English audit.');
