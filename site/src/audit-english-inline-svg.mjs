import { englishAudit as previous } from './audit-english-table-row-kept-split.mjs';

// Inline svg moves to partial. Its new scope, limitation and evidence strings are inserted at index
// 136, so the <math> row's feature name (index 138), which now first appears after them, moves
// with them ahead of the scope and limitation the two rows used to share (indices 136 and 137, now
// first used by <math>, whose scope no longer names SVG). The namespace-selector limitation
// (index 591), the custom property limitation (index 631) and the image limitation (index 845) are
// replaced in place.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0b6d179c8f711c8bcf33fd19e1b0830bf60a3b6a2afe6f11b8361383a6acaae8';
const entries = [...previous];
const math = previous[138];
entries.splice(845, 1,
  'No unsupported PNG/JPEG modes, SVG image resources (inline svg is drawn as vector paths; see the svg element row), implicit URL/path/data loading, unconstrained auto-shrink or image fragments. Resource and geometry budgets, intrinsic-ratio checks and output protection apply. Image height percentages, calc, negatives, object-fit, implicit resource loading and image splitting remain unsupported. Structural table/col/cell em/rem sizing remains rejected; invalid value or unplaceable geometry fails while preserving prior PDF. Candidate only.');
entries.splice(631, 1,
  'Where a browser would unset the declaration at computed-value time (a reference with no value and no fallback, a used reference cycle, a substituted CSS-wide keyword, a value the property rejects after substitution) FactsPDF fails with FPDF1202 (or the code the literal value would get); losing declarations are substituted and checked too; malformed var() fails FPDF1202 at parse time; revert/revert-layer fail FPDF1202; each substituted value holds at most MaxCssSyntaxNodes syntax nodes, reference chains are at most MaxCssSyntaxDepth deep and substitution work counts against MaxCssMatchOperations (FPDF1205); var() in SVG paint attributes is substituted from the svg element\'s computed custom properties; no var() in selectors or at-rule preludes, and no @property registration.');
entries.splice(591, 1, 'Inline svg now renders, but svg|… namespace selectors and @namespace are still unsupported.');
entries.splice(138, 1);
entries.splice(136, 0,
  'An inline <svg> is a replaced box (a standalone block or a direct td/th child, like img) drawn as PDF vector paths, never rasterised: its width/height attributes are presentational hints that CSS overrides; viewBox and preserveAspectRatio (every alignment and meet/slice/none) map into the content box, which clips the drawing; g, defs, a one-shape clipPath (userSpaceOnUse), path (the full path data grammar M/L/H/V/C/S/Q/T/A/Z, relative and absolute, implicit repeats, exponents; quadratics and arcs become cubic Béziers), rect (with rx/ry), circle, ellipse, line, polyline, polygon, title/desc; transform lists; inherited fill/fill-rule/stroke/stroke-width/stroke-linecap/stroke-linejoin/stroke-miterlimit/color/visibility/clip-rule, painted with any opaque CSS colour, currentColor, none and var() substituted from the svg element\'s custom properties; clip-path=url(#id) resolves to the first element in the document with that id, and a missing id or a non-clipPath element clips nothing (as in Chrome).',
  'Block box only: inside a paragraph, inline element or list it fails FPDF1101, and there is no line-box descender gap below it as under Chrome\'s inline svg (layout matches svg{display:block}); text, use, image, gradients, patterns, mask, filter, marker, a nested svg and style inside svg fail FPDF1102; opacity/fill-opacity/stroke-opacity other than 1, dashed strokes, url() paint servers, percentage or unit lengths inside the svg, a percentage height, multi-shape or objectBoundingBox clipPaths, transform/clip-path on the svg element itself and malformed path data fail FPDF1103 (a browser renders the valid prefix; FactsPDF fails), translucent colours FPDF1202; CSS that would change svg content (color where currentColor paints, display, width/height on rect, custom properties declared on content, SVG properties such as fill) fails FPDF1201; every number is bounded to ±1000000, path segments count against MaxDisplayCommands (FPDF1401) and nesting against MaxDepth (FPDF1003); tracked separately from WHATWG HTML elements.',
  'Source code, unit tests and real CLI PDF checks; a Chrome 158 print-to-PDF and the FactsPDF PDF rasterised by MuPDF at 120 DPI and compared with css_visual_metrics: identical ink bounds, 40 of 187,748 ink pixels differ (0.02%, curve anti-aliasing); independent review to be confirmed separately.',
  math);
entries.splice(140, 1, 'MathML root elements are not currently accepted.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 879 || englishAudit[139] !== '<math> (foreign namespace)' ||
  englishAudit[140] !== 'MathML root elements are not currently accepted.' || englishAudit[141] !== previous[137] || englishAudit[142] !== previous[139] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete inline svg English audit.');
