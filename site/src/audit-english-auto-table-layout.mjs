import { englishAudit as previous } from './audit-english-table-row-kept-split.mjs';

// Tables without colgroup, or whose col elements have no width, now use automatic column widths.
// The col, colgroup, table and tbody strings, each used by no other row, are replaced; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0d04065cef83cbcb6b354c4b06d05c17b9db472dc2ad2b96bfac476b84f61b44';
const entries = [...previous];
entries.splice(43, 1,
  'Only the width property applies to col. When every col omits its width (or sets auto), the whole table uses automatic column widths; some col elements with a width and some without fail FPDF1302. Zero widths, HTML width/span attributes, own font-size and padding/background decoration are unsupported.');
entries.splice(48, 1,
  'At most one colgroup per table, before thead/tbody, containing 1–16 col elements; without one, the first row\'s cells set the column count and column widths are automatic.');
entries.splice(49, 1,
  'No span attribute, column-group box decoration or multiple column groups. Only inherited text properties are accepted.');
entries.splice(112, 1,
  'Stand-alone table: an optional colgroup, an optional single header row, a nonempty tbody and an optional tfoot. With an explicit width on every col the columns are fixed; without colgroup, or when no col has a width, column widths come automatically from the cells\' min-content and max-content widths (within 0.1pt of Chrome\'s column widths), and a table without a width shrinks to its content. colspan (fixed columns), flat cell paragraphs/images, whole-row movement and oversized body-row continuation. Candidate addition: table content width and padding accept bounded em/rem, based on table final font size and HTML root, and affect measured PDF box geometry and pagination; not merged yet.');
entries.splice(113, 1,
  '1–16 columns. Without colgroup the first row sets the column count and every later row must cover it (FPDF1101). Mixed explicit and automatic column widths, colspan in automatic layout, percentage cell padding, image sizes or text-indent in automatic layout, and columns whose minimum content width exceeds the available width fail FPDF1302. No rowspan, nested tables, caption or tables in lists. Repeated headers/images remain whole, and unplaceable atomic items fail instead of producing approximate output. Fixed columns must still sum exactly to the table content width; calc and negative lengths are unsupported.');
entries.splice(117, 1,
  'Follows the optional colgroup and optional thead. No multiple tbody elements, implicit completion or rowspan. Continuation does not make images splittable.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
