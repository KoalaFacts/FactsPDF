import { englishAudit as previous } from './audit-english-quote.mjs';

// bdi now renders as inline text like span. Its new scope and limitation enter at its row
// (indices 25 and 26); its stage, failure code and evidence strings already exist. No string
// leaves the snapshot.
if (previous.length !== 866) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd8df4604094ee95ebfd29c123b45ccc25379113d247e1cf375e54a3d8c1bd174';
const entries = [...previous];
entries.splice(25, 0,
  'Inline text like span: the CSS cascade, TrueType, wrapping, table cells and pagination.',
  'No bidirectional isolation: all text is laid out left to right, with no RTL, dir attribute or unicode-bidi; Tagged PDF is not implemented.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 868 || englishAudit[27] !== previous[25] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete bdi-element English audit.');
