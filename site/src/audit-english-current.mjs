import { englishAudit as baseline } from './audit-english.mjs';

// Retain the independently reviewed base translations unchanged. The concurrent
// overflow-wrap audit adds four new unique strings before the planned-text
// section and replaces the inventory note; its source text is verified in full.
if (baseline.length !== 669 || baseline[241] !== 'C text expression (planned)' ||
    !baseline[665].startsWith('Current rendering semantics accept 51 CSS property names.'))
  throw new Error('The English audit baseline changed; reconcile the current catalogue.');

export const sourceDigest = "ddadf9e81014520a6f6e075e6e464d3f2746d3a8e5093ac02fd522440a8d27a1";
export const englishAudit = Object.freeze([
  ...baseline.slice(0, 241),
  'Explicit p/h1–h6 can use overflow-wrap:break-word to split overly long invoice and approval identifiers at supported glyph boundaries when normal break opportunities are insufficient. This affects actual PDF glyph positions, line height and pagination; normal preserves default output. Flat p inside fixed-column table cells can also opt in.',
  'Only normal/break-word. Words that fit whole on a fresh line are not split, and nowrap prohibits automatic breaks. No anywhere, legacy word-wrap alias, inline span/general-container/direct-table-element declarations, inserted hyphens, complex glyph clusters or RTL. An unfit individual glyph or first-line indent raises FPDF1302; prior output is preserved.',
  'CSS parsing, cascade, glyph measurement, line breaking, pagination and PDF rendering (bounded scope)',
  'Source and positive/negative verification prepared: actual NUnit layout and multi-page report, independent manual-break oracle, Linux Native AOT content/pagination and output-preserving failure recovery. Final-head CI and independent review remain to be checked.',
  ...baseline.slice(241, 665),
  baseline[665].replace('accept 51 CSS property names.', 'accept 52 CSS property names.') + ' Explicit paragraphs/headings may use overflow-wrap:break-word for real PDF emergency breaks only for identifiers too long to fit whole on a fresh line. Ordinary words move whole to the next line, nowrap still forbids automatic wrapping, and normal preserves default PDF bytes.',
  ...baseline.slice(666)
]);
