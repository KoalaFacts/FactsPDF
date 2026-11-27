import { englishAudit as previous } from './audit-english-auto-table-mixed-widths.mjs';

// th and td cells mix in any table row, and row and row group backgrounds paint under cells. The
// tfoot, th, thead, tr and background-color strings, each used by no other row, are replaced; no
// string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '885dc1f4c7123bfbab2ef216cb06c764203b7f523abf843c6209791ce81993f4';
const entries = [...previous];
entries.splice(123, 1,
  'Only after the tbody; td and th cells, not repeated on each page fragment; a tfoot background-color paints under its cells that have none, while its other box styles (borders, padding) still fail; display:table-row-group is accepted and changes nothing, while display:table-footer-group (a footer repeated on every fragment) fails FPDF1202; no multiple tfoot elements or table-structure recovery.');
entries.splice(124, 1,
  'Header cells in any thead, tbody or tfoot row (such as a label th next to a value td in a body row) support colspan, text/inline content, flat direct p/image items, padding, solid borders and backgrounds. The complete single-row header repeats per fragment; th text uses a real bold font role by default.');
entries.splice(127, 1,
  'The header row may hold th and td cells; a thead background-color paints under its cells that have none and repeats with the header. No multiple header rows, rowspan, header splitting or table-structure recovery. The complete header and required decorations must fit a fresh fragment.');
entries.splice(132, 1,
  'Headers and images do not split. Body cells advance independently without repeating short values; no rowspan. Rejects atomic content that cannot fit a fresh fragment with its header/required borders. Rows accept inherited text properties and background-color (painted under cells without their own, as in Chrome), not explicit height, padding or borders.');
entries.splice(236, 1,
  'currentColor is bounded to final opaque computed text ink; no transparency, alpha blending, gradients, nested colour functions or image layers. Existing applicable elements plus tr/thead/tbody/tfoot (painted under cells without their own background); table-cell restrictions remain; all prior HSL/RGB value limits still apply. Exception: a transparent background (including background:none) on inline text (span/b/strong/i/em/small/u/s/del/ins/a) is accepted and paints nothing; other values still fail FPDF1201.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[125] !== previous[125] || englishAudit[133] !== previous[133] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table row details English audit.');
