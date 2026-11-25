import { englishAudit as previous } from './audit-english-description-list.mjs';

// tfoot now renders its td rows as ordinary body rows after the tbody. The table and thead
// limitations drop "tfoot" in place (indices 101 and 111); tfoot's own scope and limitation
// enter at its row (index 108). Its stage, applicability and evidence strings already exist.
if (previous.length !== 860) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '4cdd203577afc335d6e03536bab1d1dcf725a3ec45a9251851a559d4f2eb0114';
const entries = [...previous];
entries[101] = 'Requires 1–16 explicit tracks with total widths matching the table\'s measured content width; not automatic sizing, rowspan, nested tables, caption, tables in lists, calc or negative lengths. Repeated headers/images remain whole, and unplaceable atomic items fail instead of producing approximate output.';
entries[111] = 'Only th header cells. No multiple header rows, rowspan, header splitting or table-structure recovery. The complete header and required decorations must fit a fresh fragment.';
entries.splice(108, 0,
  'An optional single tfoot directly after the nonempty tbody, holding one or more td rows; byte-identical to the same rows appended to the tbody (for example invoice totals), with the CSS cascade, inheritance and pagination of body rows.',
  'Only after the tbody and only td cells; not repeated on each page fragment, and tfoot itself takes no box styles such as background or borders; no multiple tfoot elements or table-structure recovery.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 862 || englishAudit[110] !== previous[108] || englishAudit[112] !== previous[110] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table-footer English audit.');
