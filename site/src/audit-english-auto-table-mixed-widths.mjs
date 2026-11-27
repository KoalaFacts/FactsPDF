import { englishAudit as previous } from './audit-english-auto-table-colspan.mjs';

// col and td/th widths now join automatic table layout.
// The col, table, td and th strings, each used by no other row, are replaced; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'adfc1d5aa0a35e5015b43c3ac6cf4ba364676e1e5ed1a73c5804982e09279159';
const entries = [...previous];
entries.splice(43, 1,
  'Only the width property applies to col. When any col omits its width (or sets auto), the whole table uses automatic column widths, and a col with a width constrains its column with that fixed or percentage width. Zero widths, HTML width/span attributes, own font-size and padding/background decoration are unsupported.');
entries.splice(112, 1,
  'Stand-alone table: an optional colgroup, an optional single header row, a nonempty tbody and an optional tfoot. With an explicit width on every col and no cell width the columns are fixed; otherwise column widths come automatically from the cells\' min-content and max-content widths and the col/cell pt, px or percentage widths (within 0.15pt of Chrome\'s column widths), and a table without a width shrinks to its content. colspan (in automatic layout a spanning cell widens the columns it covers as browsers do), flat cell paragraphs/images, whole-row movement and oversized body-row continuation. Candidate addition: table content width and padding accept bounded em/rem, based on table final font size and HTML root, and affect measured PDF box geometry and pagination; not merged yet.');
entries.splice(113, 1,
  '1–16 columns. Without colgroup the first row sets the column count and every later row must cover it (FPDF1101). In automatic layout, percentage cell padding, image sizes or text-indent, a spanning cell with its own width, and columns whose minimum content width exceeds the available width fail FPDF1302; table-layout is not implemented (FPDF1201). No rowspan, nested tables, caption or tables in lists. Repeated headers/images remain whole, and unplaceable atomic items fail instead of producing approximate output. Fixed columns must still sum exactly to the table content width; calc and negative lengths are unsupported.');
entries.splice(119, 1,
  'No lists, arbitrary nested blocks, nested tables or rowspan. A td pt, px or percentage width takes part in automatic column widths (em/rem widths and min/max-width reject); td rejects margin/break. Cell paragraphs accept only text styles and vertical margins; box/pagination declarations reject even when overridden. Rejects an indivisible image that cannot fit a fresh fragment.');
entries.splice(125, 1,
  'Bold text requires an explicit bold font chain or an override to normal. Image-only headers do not require a bold font. No scope/rowspan, lists, nested blocks/tables, em/rem or min/max widths, margin/break or Tagged PDF semantics (a pt, px or percentage width takes part in automatic column widths). Cell paragraphs reject box/pagination declarations; headers do not split.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
