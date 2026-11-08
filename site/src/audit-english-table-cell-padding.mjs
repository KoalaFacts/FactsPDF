import { englishAudit as baseline } from './audit-english-font-relative-images.mjs';

// Source-bound translation updates for bounded computed td/th padding.
// No IDs, status values, examples, or underlying Chinese evidence are changed.
if (baseline.length !== 774) throw new Error('Previous English audit catalogue changed.');
export const sourceDigest = 'bbb3bde84dc17ae6657a58244b8c60d65ec00767e1c2a6898886c6a209ae951a';
const scopes = new Map([
  [194, "Within the existing bounded nonnegative length and percentage model, physical or logical padding shorthands expand into separately cascaded sides. Ordinary report blocks and bound JPEG/PNG images already resolve em/rem. Newly, fixed-column td/th cells resolve em from their own final computed font size and rem from the HTML root; the computed inset changes real PDF glyph positions, cell content widths, box paint and pagination. PR #215 candidate, not yet merged."],
  [197, "padding-top resolves bounded print lengths and percentages for supported boxes, with computed em/rem on ordinary blocks, bound images and now fixed-column td/th cells; cell em uses its final font size and rem uses the HTML root, changing real PDF top insets and row heights. Candidate PR #215."],
  [198, "padding-right resolves bounded print lengths and percentages for supported boxes, with computed em/rem on ordinary blocks, bound images and now fixed-column td/th cells; cell em uses its final font size and rem uses the HTML root, changing real PDF content width and text positions. Candidate PR #215."],
  [199, "padding-bottom resolves bounded print lengths and percentages for supported boxes, with computed em/rem on ordinary blocks, bound images and now fixed-column td/th cells; cell em uses its final font size and rem uses the HTML root, changing real PDF row heights and page boundaries. Candidate PR #215."],
  [200, "padding-left resolves bounded print lengths and percentages for supported boxes, with computed em/rem on ordinary blocks, bound images and now fixed-column td/th cells; cell em uses its final font size and rem uses the HTML root, changing real PDF content width and glyph positions. Candidate PR #215."],
  [257, "LTR padding-inline accepts one or two bounded lengths, with start/end mapped to physical left/right and independent padding-inline-start/padding-inline-end cascade winners. Ordinary report boxes already resolve em/rem. Now fixed-column td/th cells do too, against their own final font size and the HTML root, changing real PDF cell width, text X and repeated table-header layout. Candidate PR #215."],
  [259, "LTR padding-block accepts one or two bounded lengths, with block-start/end mapped to physical top/bottom and independent padding-block-start/padding-block-end cascade winners. Ordinary report boxes already resolve em/rem. Now fixed-column td/th cells do too, against their own final font size and the HTML root, changing real PDF vertical placement, background and page continuation. Candidate PR #215."]
]);
const limitations = new Map([
  [195, "Percentage padding resolves against containing content width, including the full spanned-track width in a cell; it is not a height percentage. Bounded em/rem padding is now supported for fixed-column td/th cells and bound images, but not table/col/row/group structure or independently styled cell paragraphs. Negative, auto, calc and unplaceable geometry still fail with FPDF1201/1202/1302 and preserve the existing PDF. Not complete CSS Box Model support."],
  [258, "LTR horizontal writing only. No RTL or vertical writing mode, negative values, auto, calc, mixed CSS-wide keywords, relative table/col/row-group lengths or independently painted cell paragraphs. Unsupported declarations fail explicitly; overwide boxes return FPDF1302 without altering an existing PDF."],
  [260, "Bounded LTR horizontal writing and containing-block-width percentages only; not height-relative percentages, general height layout, RTL or vertical writing. Negative, auto, calc, mixed CSS-wide keywords, table/col/row-group relative lengths and independently painted cell paragraphs remain rejected, with FPDF1302 on impossible geometry and preserved previous output."]
]);
export const englishAudit = Object.freeze(baseline.map((original, i) =>
  scopes.get(i) ?? limitations.get(i) ?? original));
if (englishAudit.length !== 774 || englishAudit.some(value =>
    typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete table-cell relative-padding English catalogue.');
