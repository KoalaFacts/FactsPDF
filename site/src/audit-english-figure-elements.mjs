import { englishAudit as previous } from './audit-english-sectioning-elements.mjs';

// figure now renders like blockquote and figcaption like div. figcaption reuses the sectioning
// strings; figure adds its scope and limitation. No string leaves the snapshot.
if (previous.length !== 853) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'e9cd4450754361d51f6a2ce61e4d915193776df94c6582cdb278767a37a66dbe';
const entries = [...previous];
entries.splice(50, 0,
  'A block container like blockquote: default 8pt top and bottom and 24pt left and right margins; width, padding, background, borders, the CSS cascade and pagination.',
  'Full HTML semantics and Tagged PDF are not implemented; like blockquote it cannot sit inside a list or a table.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 855 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete figure-element English audit.');
