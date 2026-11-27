import { englishAudit as previous } from './audit-english-table-row-break-inside.mjs';

// display:flex lays out single-line, row-direction flex containers. The eight flex rows move from
// rejected to partial with their own scope and a shared limitation; gap, row-gap and column-gap
// accept lengths; the display row's limitation names flex; the inventory note gains twelve names
// (384 to 396).
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b39e89a4893e805865e50f0394d30b401619e0d4752a3515c1a077b11e9e63a5';
const entries = [...previous];
// Splices run from the end, so each index refers to the earlier layer.
entries.splice(870, 1,
  previous[870].replace(/\.$/, '') + "; display:flex lays out single-line, row-direction flex containers, adding flex, flex-direction, flex-wrap, flex-flow, flex-grow, flex-shrink, flex-basis, justify-content, align-items, align-self, align-content and order (twelve names), and gap, row-gap and column-gap now take lengths and percentages; this took it from 384 to 396.");
entries.splice(424, 6,
  "The flex shorthand (none, auto, one number as the grow factor with a 0% basis, or grow shrink basis) sets a flex item's flex-grow, flex-shrink and flex-basis; widths are resolved with the CSS flexible-length algorithm in a single-line row container, content sizes measured from the real PDF glyphs.",
  "Only single-line (flex-wrap:nowrap), row-direction (flex-direction:row) display:flex, on ordinary report panels, li and body; each inline child and each run of text between them is its own flex item, and a flex li has no list marker and is not numbered; html{display:flex} is accepted only as a no-op where body fills the page width, otherwise FPDF1201. A row is never split across pages: when it does not fit the rest of the page it moves to the next page only if every item is a single paragraph of at most three lines (which a browser also moves whole), otherwise, and for a row taller than a page or items that cannot shrink to the container, FPDF1302. wrap, column and reverse directions, inline-flex, display:flex on paragraphs or inside tables and tables as flex items fail FPDF1201/FPDF1202; align-content does nothing in a single-line container and is validated and dropped; errors leave the previous PDF untouched.",
  "CSS parsing, cascade and real PDF flex layout",
  "Candidate-source NUnit (FlexLayoutTests: exact Courier geometry, alignment, shrinking, pagination and failure codes) and a word-origin comparison with Chrome print output (horizontal differences at most 0.28pt, vertical differences the same as for the same content in plain block flow); test suites not run locally, exact CI, independent review and main still to be confirmed.",
  "flex-direction accepts only row (the initial value, with CSS-wide keywords and the flex-flow shorthand): a display:flex container lays its children out left to right in one row.",
  "flex-wrap accepts only nowrap (the initial value, with CSS-wide keywords and the flex-flow shorthand): every flex item stays on one line and shrinks by flex-shrink when the row overflows.",
  "flex-grow takes a nonnegative number: flex items share the container's free width in proportion, within min-width/max-width, with the freezing loop of the CSS flexible-length algorithm.",
  "flex-shrink takes a nonnegative number: when the row is too wide, items shrink in proportion to shrink factor times base size, never below their min-content width (the automatic minimum) or min-width.",
  "flex-basis: auto (the width, else the max-content width), content, lengths, percentages of the container width and em/rem; with box-sizing:border-box it includes padding and borders.",
  "align-items and align-self: normal/stretch (boxes and images stretch to the line height), flex-start/start/self-start, flex-end/end/self-end, center and baseline (first-line baselines, the border-box bottom for an item without text); the container's height or min-height sets the line height.",
  "justify-content: normal/flex-start/start/left/stretch, flex-end/end/right, center, space-between, space-around and space-evenly; auto margins take the free space first, and column-gap inserts a fixed gap between items.",
  "The gap shorthand sets row-gap and column-gap: normal, a nonnegative length, a percentage of the container width or em/rem, one or two values; a display:flex container inserts the column gap between adjacent flex items, and other elements are unaffected (as in a browser).",
  "A single-line flex container has one line, so the row gap has no effect; no grid or multi-column gaps; negative values, auto and more than two values fail FPDF1202.",
  "CSS parsing, cascade and spacing between flex items",
  "row-gap: normal, nonnegative lengths, percentages and em/rem are validated and accepted; a single-line flex container has one line, so no row gap is inserted (as in a browser).",
  "Negative values, auto and other values fail FPDF1202; no multi-line flex, grid or multi-column row gaps.",
  "column-gap: normal, a nonnegative length, a percentage of the container width or em/rem; a display:flex container inserts it between adjacent flex items, and other elements are unaffected (as in a browser).",
  "Negative values, auto and other values fail FPDF1202; no multi-column layout gaps.");
entries.splice(248, 1,
  "display:none, display:block and display:flex (single-line, row-direction flex layout, see the flex row; on ordinary report panels, li and body, and on html only as a no-op where body fills the page width). CSS-wide inherit/initial/unset, inline/inline-flex/grid/contents and all other display values reject, and display:flex on paragraphs or inside tables fails FPDF1201. For display:none/block, no body/html, span, image, hr, list or table declarations, except that table parts accept the value they already render (table-header-group on thead, table-row-group on tbody/tfoot, table-row on tr, table-cell on td/th) and it changes nothing; every other combination of a table display keyword (including table-footer-group on tfoot and use on non-table elements) fails FPDF1202. Hidden markup still undergoes strict HTML/CSS parsing, resource checks and budgets; hiding cannot bypass validation. The HTML hidden attribute is [hidden]{display:none} on the same blocks (see attr.11); no media queries or JavaScript display.");
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 886 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete flex layout English audit.');
