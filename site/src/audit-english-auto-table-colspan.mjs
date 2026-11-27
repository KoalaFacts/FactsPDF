import { englishAudit as previous } from './audit-english-auto-table-layout.mjs';

// colspan cells now widen the columns they cover in automatic table layout.
// The table scope and limitation, used by no other row, are replaced; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '63da38ec73cdb3c00e307d441bf55422d38a292e39be3026df2e11c22fdd90a3';
const entries = [...previous];
entries.splice(112, 1,
  'Stand-alone table: an optional colgroup, an optional single header row, a nonempty tbody and an optional tfoot. With an explicit width on every col the columns are fixed; without colgroup, or when no col has a width, column widths come automatically from the cells\' min-content and max-content widths (within 0.1pt of Chrome\'s column widths), and a table without a width shrinks to its content. colspan (in automatic layout a spanning cell widens the columns it covers as browsers do), flat cell paragraphs/images, whole-row movement and oversized body-row continuation. Candidate addition: table content width and padding accept bounded em/rem, based on table final font size and HTML root, and affect measured PDF box geometry and pagination; not merged yet.');
entries.splice(113, 1,
  '1–16 columns. Without colgroup the first row sets the column count and every later row must cover it (FPDF1101). Mixed explicit and automatic column widths, percentage cell padding, image sizes or text-indent in automatic layout, and columns whose minimum content width exceeds the available width fail FPDF1302. No rowspan, nested tables, caption or tables in lists. Repeated headers/images remain whole, and unplaceable atomic items fail instead of producing approximate output. Fixed columns must still sum exactly to the table content width; calc and negative lengths are unsupported.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
