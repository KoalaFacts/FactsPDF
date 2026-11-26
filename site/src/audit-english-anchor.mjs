import { englishAudit as previous } from './audit-english-hidden.mjs';

// The href / target / rel row was stale (a href and metadata link rel were already accepted) and
// target plus hyperlink rel types on a are now accepted. Its own scope and limitation (indices 163
// and 164, used by no other row) are replaced; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '34133510e2f335271455527a5356db750e46ab2293db9b85b36f169d9461f389';
const entries = [...previous];
entries.splice(163, 2,
  'href on <a> makes a clickable PDF link (HTTPS or mailto only, see <a>); target on <a> (_blank, _self, _parent, _top or a name) and hyperlink rel types (noopener, noreferrer, nofollow, external, sponsored, ugc and the like) are validated and ignored, so the PDF bytes equal the page without them; metadata rel on <link> in <head> is accepted and never fetched (see <link>).',
  'An empty target, other targets starting with an underscore (such as _new), an empty rel and unknown rel types fail FPDF1103; target on any other element fails FPDF1103; rel=stylesheet fails FPDF1204; PDF links always open outside the document, no browsing contexts.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[165] !== previous[165] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete anchor target/rel English audit.');
