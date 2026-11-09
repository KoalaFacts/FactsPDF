import { englishAudit as previous } from './audit-english-table-relative-tracks.mjs';

// Extend the existing ordered, source-bound 774-entry English catalogue in place.
// The CSS feature remains partial, with no release or complete-CSS claim.
if (previous.length !== 774) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '57e3602b76761343b1c89c893388a04e588723cdfbf42b6e69aa5eae6032dfcc';
const changes = new Map([
  [295, "Explicit report p/h1–h6, flat p elements within fixed-track td/th, and td/th cells themselves may declare bounded nonnegative text-indent lengths: pt/px/mm/cm/in/pc/q/%/em/rem/zero. Ordinary html/body and report panel hosts outside list/table ancestry continue to inherit computed text indent. Cell declarations reach anonymous cell text and nested flat paragraphs; the measured PDF shifts only the first line and can change wrapping and pagination, not the cell's background box. em uses final element font, rem uses root font; percentages use the measured cell/paragraph content width. Cascade, important and inherit/initial/unset remain effective."],
  [296, "No negative or hanging indent, each-line/hanging modifiers, calc, inline/span or structural list/table (table/col/colgroup/tr/thead/tbody) declarations. The new table-cell scope is limited to explicit-column td/th and their flat p children, not nested tables or complete CSS Text. Oversized/unfittable first lines fail FPDF1302, invalid scope/value fails FPDF1201/FPDF1202; existing PDF bytes and stream position are protected."],
  [298, "Non-table p/h1–h6, bounded report text containers, plus explicit fixed-track td/th and flat cell p, shared .NET / CLI core."]
]);
export const englishAudit = Object.freeze(previous.map((text, index) => changes.get(index) ?? text));
if (englishAudit.length !== 774 || englishAudit.some(text =>
  typeof text !== 'string' || !text.trim() || /[\u3400-\u9fff]/u.test(text)))
  throw new Error('Incomplete table-cell text-indent English audit.');
