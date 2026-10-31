import { englishAudit as prior } from './audit-english-list-markers.mjs';

// Preserve all 418 previously audited feature IDs and earlier translations.
// Only css.text-transform's three source strings and the inventory note change.
if (prior.length !== 702) throw new Error('Reconcile the list marker English catalogue.');
export const sourceDigest = 'd8a838b2eef1f4e2dcbe91951457f7a4a086f7d7a84f722fc5ae41cb7027dfdd';
const changed = new Map([
  [253, "ASCII-only report text-transform:none/uppercase/lowercase/capitalize. Capitalize uppercases just the first lowercase letter of each ASCII letters/digits token, retaining mixed-case tails; hyphens, punctuation, spaces and non-ASCII split tokens. Styled inline spans do not reset word boundaries, explicit br does. Actual searchable PDF glyphs, font selection, line wrapping and pages change; CJK, numbers and punctuation remain unchanged."],
  [254, "No general Unicode word segmentation, locale-specific casing, full-width or full-size-kana. Non-ASCII cased letters, mapped Roman numerals and circled letters reject with FPDF1305; unsupported CSS values FPDF1202, standalone image targets FPDF1201, missing transformed-font glyphs FPDF1504. A failed conversion preserves the existing PDF. Not full CSS Text."],
  [256, "Source NUnit real TrueType glyphs, mixed-style span boundaries, pagination and preserved-output negative cases, plus a 54-entry service-register HTML/PDF literal-case oracle and Linux Native AOT qpdf/text/page verifier. Exact-head CI and genuinely independent review must be confirmed separately."]
]);
export const englishAudit = Object.freeze(prior.map((value, index) =>
  index === 698 ? value + " Bounded ASCII text-transform:capitalize changes actual PDF glyphs, line layout and pagination while preserving mixed-case word tails and inline word boundaries. No Unicode capitalization or full CSS Text support." : (changed.get(index) ?? value)));
