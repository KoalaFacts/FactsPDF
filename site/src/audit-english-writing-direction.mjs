import { englishAudit as previous } from './audit-english-offset-border-image.mjs';

// direction:ltr, unicode-bidi:normal, writing-mode:horizontal-tb and text-orientation:mixed were
// already validated against their initial value and dropped by the engine, but their feature rows
// still said rejected (FPDF1201). The four rows are re-audited to partial: nine strings are
// inserted where the direction row is first collected; no earlier string moves and the CSS
// inventory note is unchanged.
if (previous.length !== 791) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'df14aaf0736cc411d81248ec06d267aa8b18b580ef3fb10023625d60b5e7ca4a';
const tail = ' (including CSS-wide keywords, !important and any case) is validated against its initial value and dropped on any element; the renderer lays out only left-to-right horizontal text and the PDF bytes equal the undeclared page.';
const rows = [
  'direction at ltr' + tail,
  'rtl and other values still fail FPDF1202; characters that need bidirectional layout still fail FPDF1305; right-to-left layout is unsupported.',
  'Candidate-source NUnit: initial values produce PDF bytes identical to the undeclared page, other values fail FPDF1202 and write no output; the renderer lays out only left-to-right horizontal text and never reorders, rotates or combines glyphs. Exact CI, independent review and main are verified separately.',
  'unicode-bidi at normal' + tail,
  'isolate, embed, bidi-override, plaintext and other values still fail FPDF1202; no bidirectional reordering is done.',
  'writing-mode at horizontal-tb' + tail,
  'vertical-rl, vertical-lr, the legacy lr-tb and other values still fail FPDF1202; vertical writing is unsupported.',
  'text-orientation at mixed' + tail,
  'upright, sideways and other values still fail FPDF1202; glyphs are never rotated.',
];
export const englishAudit = Object.freeze([...previous.slice(0, 329), ...rows, ...previous.slice(329, 791)]);
if (englishAudit.length !== 800 || englishAudit[329] !== rows[0] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete writing-direction English audit.');
