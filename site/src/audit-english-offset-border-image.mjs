import { englishAudit as previous } from './audit-english-font-kerning.mjs';

// Box offsets (top, right, bottom, left, inset and the six logical insets) at auto and
// border-image / border-image-source at none are validated against their initial value and
// dropped. Six feature rows (css.top/right/bottom/left/inset, css.border-image) move from
// rejected to partial: five strings are inserted where those rows are first collected, which
// also moves three earlier strings after the border-image row; the inventory note gains 13 names.
if (previous.length !== 786) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b78c8c99d602117606bad5580e56ebcfa18331d204b6076a5610849a9b9a975b';
const offsets = [
  'top, right, bottom, left, inset and the logical inset-block/inset-inline (with -start/-end) at auto (including CSS-wide keywords, !important and any case) are validated against their initial value and dropped on any element; since only position:static is accepted, offsets never move a box and the PDF bytes equal the undeclared page.',
  'Lengths, percentages, several components (such as inset:auto auto) and other values still fail FPDF1202; positioning is unsupported.',
  'Candidate-source NUnit: initial values produce PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; only position:static is accepted, so offsets never move a box, and there is no border image. Exact CI, independent review and main are verified separately.',
];
const borderImage = [
  'border-image and border-image-source at none (including CSS-wide keywords, !important and any case) are validated against their initial value and dropped on any element; borders are always painted from their style, width and colour and the PDF bytes equal the undeclared page.',
  'url(), gradients and other values still fail FPDF1202; border images are unsupported.',
];
const note = previous[782] + ' top, right, bottom, left, inset and the six logical inset properties at auto, and border-image and border-image-source at none, are validated against their initial value and dropped, because offsets never move a position:static box and no border image is painted, while other values still fail FPDF1202; this took it from 351 to 364.';
const p = previous;
export const englishAudit = Object.freeze([
  ...p.slice(0, 224), ...offsets, ...p.slice(227, 269), ...borderImage, ...p.slice(269, 277),
  ...p.slice(224, 227), ...p.slice(277, 782), note, ...p.slice(783, 786),
]);
if (englishAudit.length !== 791 || englishAudit[787] !== note || !note.includes('from 351 to 364') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete offset and border-image English audit.');
