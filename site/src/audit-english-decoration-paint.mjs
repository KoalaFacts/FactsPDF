import { englishAudit as previous } from './audit-english-table-vertical.mjs';
// Preserve all 714 earlier independently reviewed source-string positions.
// The new 418-record audit changes only the existing css.text-decoration
// scope/limits/stage/applicability/evidence and the CSS inventory prose.
if (previous.length !== 714) throw new Error('Text decoration audit translation baseline changed.');
export const sourceDigest = '5ed4b713f5f80489851b19bd19ce110b41c294402a9787ed496e3c92a8f67fcc';
const entries = [...previous];
entries[253] = 'Real PDF single-line decoration for report text: text-decoration/text-decoration-line support none, underline, overline and line-through. A bounded single-line text-decoration shorthand may include one opaque colour and one solid/dashed/dotted stroke; text-decoration-color accepts currentcolor or documented RGB and text-decoration-style accepts solid/dashed/dotted. Glyph-width-measured PDF strokes, inline styling and pagination are implemented.';
entries[254] = 'Only one line at a time with font-size-derived thickness and position. No simultaneous lines, double/wavy, alpha, arbitrary thickness or offsets, HTML u/s, browser descendant propagation, RTL or complex shaping. Unsupported paint and combinations fail explicitly without overwriting an existing PDF.';
entries[255] = 'CSS cascade, real PDF glyphs and clipped vector paint (bounded scope)';
entries[256] = 'Supported p/h1–h6 and inline span/b/strong/i/em runs; explicit font, colour, resource and document-structure limits remain in force.';
entries[257] = 'Implemented actual PDF paths, font width, pagination, inline colour and CLI failure/recovery tests in current source. The 60-record Native AOT/Poppler witness must pass exact-head CI and independent review. Not a human page approval or distributed package.';
entries[710] = previous[710].replaceAll('68 CSS property names', '70 CSS property names').replaceAll('68 scoped CSS property names', '70 scoped CSS property names').replaceAll('68 bounded CSS property names', '70 bounded CSS property names').replaceAll('68 accepted CSS property names', '70 accepted CSS property names') + ' As of 2026-11-01, text-decoration-color and text-decoration-style and a single-line text-decoration shorthand produce real glyph-measured, paginated opaque PDF decoration strokes; no combined lines, double/wavy, alpha, arbitrary thickness or full CSS Text Decoration. Now 70 bounded CSS property names.';
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 714 || englishAudit.some(value =>
    typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Invalid text-decoration English audit translation catalogue.');
