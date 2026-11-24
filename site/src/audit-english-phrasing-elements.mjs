import { englishAudit as previous } from './audit-english-unused-zero-insets.mjs';

// code, kbd, samp, var, cite and dfn now render as inline text. Their rows were the first to use
// the shared "semantic rejection" strings, so those strings keep their place; only the shared
// "real PDF glyph and decoration rendering" stage moves forward to the cite row, which now uses it
// first. Eight new strings: six scopes, one shared limitation and one shared evidence level.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'ce6139c90cebe9b0187a7d491cdc1e9dd2a4bc05c572b584ea25490fcf9a2f0d';
const italic = 'the HTML default style) and needs an italic font chain, otherwise FPDF1507; CSS overrides, TrueType, line wrapping, table cells and pagination work.';
const regular = 'text with the supplied regular font chain; CSS overrides, TrueType, line wrapping, table cells, pre and pagination work.';
const entries = [...previous];
const [stage] = entries.splice(37, 1);
const inserted = new Map([
  [27, `<cite> renders as inline italic text for the title of a work (${italic}`],
  [28, 'Inline text only; block descendants are unsupported. There is no monospace fallback (as for pre, only the supplied fonts are used and fonts are never discovered automatically); unsupported attributes are rejected explicitly.'],
  [29, stage],
  [30, 'Source code, unit tests and a rasterised real CLI PDF were checked; independent review is tracked separately.'],
  [31, `<code> renders as inline code ${regular}`],
  [43, `<dfn> renders as inline italic text for a defined term (${italic}`],
  [67, `<kbd> renders as inline keyboard-input ${regular}`],
  [85, `<samp> renders as inline program-output ${regular}`],
  [108, `<var> renders as inline italic text for a variable (${italic}`],
]);
for (const [index, value] of inserted) entries.splice(index, 0, value);
export const englishAudit = Object.freeze(entries);
if (stage !== 'Real PDF glyph and decoration rendering (source only)' || englishAudit.length !== 850 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete phrasing element English audit.');
