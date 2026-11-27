import { englishAudit as previous } from './audit-english-auto-table-mixed-widths.mjs';

// Table rows mix th and td cells and tr/thead/tbody/tfoot paint background-color; the table, tfoot, thead, th and background-color strings are replaced in place, none added.
if (previous.length !== 877) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a59642f46e674c744fbb681a618352ff96d40d1e5c6fa229057a10f31b01c5c8';
const entries = [...previous];
entries.splice(123, 2,
  'Only after the tbody; td and th cells, not repeated on each page fragment; a tfoot background-color paints under its cells that have none, while its other box styles (borders, padding) still fail; display:table-row-group is accepted and changes nothing, while display:table-footer-group (a footer repeated on every fragment) fails FPDF1202; no multiple tfoot elements or table-structure recovery.',
  'Header cells in any thead, tbody or tfoot row (such as a label th next to a value td in a body row) support colspan, text/inline content, flat direct p/image items, padding, solid borders and backgrounds. The complete single-row header repeats per fragment; th text uses a real bold font role by default.');
entries.splice(127, 1,
  'The header row may hold th and td cells; a thead background-color paints under its cells that have none and repeats with the header. No multiple header rows, rowspan, header splitting or table-structure recovery. The complete header and required decorations must fit a fresh fragment.');
entries.splice(132, 1,
  'Headers and images do not split. Body cells advance independently without repeating short values; no rowspan. Rejects atomic content that cannot fit a fresh fragment with its header/required borders. Rows accept inherited text properties and background-color (painted under cells without their own, as in Chrome), not explicit height, padding or borders.');
entries.splice(236, 1,
  'currentColor is bounded to the final computed text ink, including its alpha; no opacity, blend modes, gradients, nested colour functions or image layers. Comma or slash alpha (a number or a percentage) is clamped to 0–1, rounded to n/255 as Chrome does and composited with PDF constant alpha; alpha 0 paints nothing. Existing applicable elements plus tr/thead/tbody/tfoot (painted under cells without their own background); table-cell restrictions remain; all prior HSL/RGB value limits still apply. Exception: a transparent background (including background:none) on inline text (span/b/strong/i/em/small/u/s/del/ins/a) is accepted and paints nothing; other values still fail FPDF1201.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 877 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
