import { englishAudit as previous } from './audit-english-meta.mjs';

// link now accepts metadata links in head. Its new scope and limitation enter at its row (indices
// 84 and 85); its stage and evidence strings already exist. No string leaves the snapshot.
if (previous.length !== 872) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '99b1cae2426650b7ca752d9466bb3e61de1fa8455f61e06f214ea78c56950fd9';
const entries = [...previous];
entries.splice(84, 0,
  'Links in head that only name a related resource or a connection hint (icon, canonical, alternate, manifest, preconnect, dns-prefetch, preload and the like) are accepted, never fetched or painted, and the page renders as if they were absent.',
  'rel=stylesheet (including alternate stylesheet) fails FPDF1204 anywhere in the page, like @import (external resources are never loaded); unknown link types, an empty rel, and media and style attributes fail FPDF1103; head only; counts toward the element limit.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[86] !== previous[84] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete link-element English audit.');
