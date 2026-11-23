import { englishAudit as previous } from './audit-english-writing-direction.mjs';

// font-variant, text-overflow, box-decoration-break, columns, column-count, column-width and
// column-gap at their initial value, and well-formed animation and transition lists, were already
// validated and dropped by the engine, but their feature rows still said rejected (FPDF1201). The
// nine rows are re-audited to partial: nineteen strings are inserted where those rows are first
// collected, one string that only these rows used drops out, and the inventory note is unchanged.
if (previous.length !== 800) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '3d5a1fc249638c2fc736f20d568541d02cfd5a77c3ef8da37bda269ef44c8ecd';
const k = ' (including CSS-wide keywords, !important and any case) is validated against its initial value and dropped on any element; ';
const col = 'the renderer lays every box out in one column and the PDF bytes equal the undeclared page.';
const initialEvidence = 'Candidate-source NUnit: initial values produce PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output. Exact CI, independent review and main are verified separately.';
const fontVariant = [
  'font-variant at normal' + k + 'the renderer never substitutes small-caps, East Asian, alternate or sub/superscript glyphs, and the PDF bytes equal the undeclared page.',
  'small-caps and other values still fail FPDF1202; glyph variants are unsupported.',
  initialEvidence,
];
const textOverflow = [
  'text-overflow at clip' + k + 'no box ever clips its overflow, and the PDF bytes equal the undeclared page.',
  'ellipsis and other values still fail FPDF1202; no ellipsis is drawn.',
];
const columns = [
  'box-decoration-break (and its -webkit- prefix) at slice' + k + 'a box split across pages already paints as slices, and the PDF bytes equal the undeclared page.',
  'clone and other values still fail FPDF1202; a split box does not repeat its borders and background.',
  'column-gap at normal' + k + col,
  'Lengths, percentages and other values still fail FPDF1202; multi-column, flex and grid gaps are unsupported.',
  'columns at auto' + k + col,
  'Column counts, column widths and other values still fail FPDF1202; multi-column layout is unsupported.',
  'column-count at auto' + k + col,
  'Integer column counts and other values still fail FPDF1202; multi-column layout is unsupported.',
  'column-width at auto' + k + col,
  'Lengths and other values still fail FPDF1202; multi-column layout is unsupported.',
];
const motion = [
  'Well-formed lists for the animation shorthand and every animation-* longhand (including the -webkit- prefix) are validated and dropped on any element, and @keyframes rules are dropped too; a written PDF page plays no animation, so the PDF bytes equal the undeclared page.',
  'Malformed values still fail FPDF1202; keyframes are never applied.',
  'Candidate-source NUnit: well-formed lists produce PDF bytes identical to the undeclared page, malformed values fail FPDF1202 and write no output. Exact CI, independent review and main are verified separately.',
  'Well-formed lists for the transition shorthand and every transition-* longhand (including the -webkit- prefix) are validated and dropped on any element; a written PDF page is never restyled, so the PDF bytes equal the undeclared page.',
  'Malformed values still fail FPDF1202; no value is ever transitioned.',
];
const p = previous;
export const englishAudit = Object.freeze([
  ...p.slice(0, 287), ...fontVariant, ...p.slice(287, 311), ...textOverflow, ...p.slice(311, 366),
  ...columns, ...p.slice(367, 368), ...motion, ...p.slice(368, 800),
]);
if (englishAudit.length !== 819 || englishAudit[287] !== fontVariant[0] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete stale-reset English audit.');
