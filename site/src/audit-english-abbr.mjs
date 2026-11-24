import { englishAudit as previous } from './audit-english-time-data.mjs';

// abbr now renders as inline text, with a dotted underline when it has a title. Its row is the
// first to use the shared glyph-rendering stage and source/test/raster evidence strings, so those
// two move forward to it; two new strings (scope and limitation) are inserted before them.
if (previous.length !== 853) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '68bae99e38c38f876079d494127367ca9fc1921ba9d9e1b71816580c80aa7836';
const entries = [...previous];
const moved = entries.splice(29, 2);
entries.splice(6, 0,
  '<abbr> renders as inline text; with a title attribute it draws the HTML default dotted underline, and without one it has no default style. The title is not drawn. CSS overrides, TrueType, line wrapping, table cells and pagination work.',
  'Inline text only; block descendants are unsupported. The title is not shown as a tooltip or written to the PDF; other unsupported attributes are rejected explicitly.',
  ...moved);
export const englishAudit = Object.freeze(entries);
if (moved[0] !== 'Real PDF glyph and decoration rendering (source only)' || !moved[1].startsWith('Source code, unit tests') ||
  englishAudit.length !== 855 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete abbr English audit.');
