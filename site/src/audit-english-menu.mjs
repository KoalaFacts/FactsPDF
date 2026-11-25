import { englishAudit as previous } from './audit-english-table-footer.mjs';

// menu now renders as a list exactly like ul. Its new scope and limitation enter at its row
// (indices 82 and 83); its stage, failure code and evidence strings already exist. No string
// leaves the snapshot.
if (previous.length !== 862) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'edd674bb15056b88cb2a0f81adfa9a084a153e519246b700c518b0d0b725674e';
const entries = [...previous];
entries.splice(82, 0,
  'A list exactly like ul: disc markers, the marker gutter, up to two nesting levels (mixable with ul/ol), list-style, the CSS cascade and pagination.',
  'No toolbar-menu semantics, interaction or Tagged PDF; otherwise the limits of ul apply (no start, li value or HTML type).');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 864 || englishAudit[84] !== previous[82] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete menu-element English audit.');
