import { englishAudit as previous } from './audit-english-custom-properties.mjs';

// A kept table row that must split now paginates like Chrome instead of failing. The break-inside
// row's limitation (index 408) and the page-break-inside row's limitation (index 420) are replaced
// in place; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0e4408a1cc3960189e84bf82d58ef78837b36b031e35f0157e7052edd0c45602';
const entries = [...previous];
entries.splice(408, 1,
  'Only top-level ordinary div/section/article/blockquote with 1–8 direct nonempty p/pre/h1–h6 children. Exception: table tr and thead accept auto/avoid and render byte-identical to the page without it (a row that fits a fresh fragment with the repeated header, and the header, never split); a kept row that must split (taller than a fresh fragment, or split only for the table\'s top decoration) breaks before itself as Chrome does (the whole table moves for the first body row) and then continues from the fresh page at complete lines; a last row split only for the table\'s bottom padding/border still fails FPDF1302. Nested boxes, lists, tbody/tfoot/td/th, images, internal page breaks, fixed/min-height, over-one-page content and general Paged Media are unsupported. Inapplicable FPDF1201, invalid FPDF1202 and unplaceable FPDF1302 preserve the previous PDF.');
entries.splice(420, 1,
  'Same strict scope as break-inside: no lists, images, nested/complex children, internal forced breaks, fixed/min-height or cards taller than one page. Only auto/avoid values. In tables only tr and thead accept it (byte-identical to the page without it; a row that must split starts on a fresh page and continues there as in Chrome, and a last row split only for the bottom decoration fails FPDF1302), while tbody/tfoot/td/th still fail FPDF1201. FPDF1201, FPDF1202 and FPDF1302 reject unsupported input without overwriting the prior PDF.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[409] !== previous[409] || englishAudit[421] !== previous[421] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete kept table-row English audit.');
