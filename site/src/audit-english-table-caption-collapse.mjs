import { englishAudit as previous } from './audit-english-table-row-details.mjs';

// Table captions, the collapsing border model and Chrome's th centring: the caption,
// border-collapse, caption-side, th, tr and table strings are replaced in place and the
// shared candidate evidence string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '57977b10c54b599a9b2e0387c681c2e2b070d88d59a9c842d94bc036da56f58d';
const entries = [...previous];
entries.splice(405, 2,
  'caption-side:top (including CSS-wide keywords, !important and any letter case) is validated and dropped: a caption is already placed above its table, so the PDF bytes match the undeclared page.',
  'bottom and other values still fail FPDF1202; a caption below the table is not supported.');
entries.splice(402, 0,
  'Candidate-source NUnit: the initial value produces PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; cell border boxes touch without spacing, which is the separated model with zero spacing. Exact CI, independent review and main are verified separately.');
entries.splice(397, 3,
  'separate is the zero-spacing separated model (PDF bytes identical to an undeclared page); collapse implements Chrome\'s CSS 2.1 collapsing border model: hidden wins, then the wider border, then solid over dashed over dotted, then cell over row, row group and table, and between equal boxes the one further left or up; cells and the table lay out with half of each collapsed border, table padding is ignored, and borders are painted centred on the grid lines; tr/thead/tbody/tfoot borders take part in the conflict resolution; at a page break each fragment paints half of the broken line, and a repeated header repeats the table\'s top border with it.',
  'Column and column group borders are not supported (FPDF1201); a row that must split across pages fails FPDF1302 in the collapsing model; non-zero border-spacing still fails FPDF1202; Chrome rounds fractional-pixel border widths, FactsPDF uses the exact width; other values fail FPDF1202.');
entries.splice(132, 1,
  'Headers and images do not split. Body cells advance independently without repeating short values; no rowspan. Rejects atomic content that cannot fit a fresh fragment with its header/required borders. Rows accept inherited text properties and background-color (painted under cells without their own, as in Chrome), not explicit height, padding or borders; in a border-collapse:collapse table row borders take part in the collapsed border conflict resolution, while the separated model still rejects them.');
entries.splice(124, 1,
  'Header cells in any thead, tbody or tfoot row (such as a label th next to a value td in a body row) support colspan, text/inline content, flat direct p/image items, padding, solid borders and backgrounds. The complete single-row header repeats per fragment; th text uses a real bold font role by default; centred by default as in Chrome (when the parent\'s text-align is its initial value, otherwise the parent\'s alignment is inherited).');
entries.splice(113, 2,
  '1–16 columns. Without colgroup the first row sets the column count and every later row must cover it (FPDF1101). In automatic layout, percentage cell padding, image sizes or text-indent, a spanning cell with its own width, and columns whose minimum content width exceeds the available width fail FPDF1302; table-layout is not implemented (FPDF1201). No rowspan, nested tables or tables in lists (captions and collapsed borders have their own rows). Repeated headers/images remain whole, and unplaceable atomic items fail instead of producing approximate output. Fixed columns must still sum exactly to the table content width; calc and negative lengths are unsupported.');
entries.splice(39, 0,
  'Candidate source checked with NUnit (TableCaptionCollapseTests); the Chrome 158 geometry comparison runs at integration.');
entries.splice(38, 0,
  'A caption as the table\'s first child: a block above the table at the table\'s border-box width, centred by default (Chrome\'s user-agent text-align:-internal-center; it inherits the alignment when the parent sets text-align), with text properties such as font size, weight and colour, padding, borders and background; it stays with the table\'s first page fragment and does not repeat.',
  'Only as the table\'s first child and at most one (FPDF1101); inline text and br only, no p or images (FPDF1101); caption-side:bottom (FPDF1202), non-zero margins and width (FPDF1201) are not implemented; a caption whose min-content width exceeds the table\'s width fails FPDF1302, where Chrome widens the table.',
  'Bounded table structure / layout / body-row continuation');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 879 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete table caption and collapsed border English audit.');
