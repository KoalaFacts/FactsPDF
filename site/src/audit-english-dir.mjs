import { englishAudit as previous } from './audit-english-template.mjs';

// dir="ltr" is now accepted on any element. The dir row's own scope and limitation (indices 155
// and 156, used by no other row) are replaced; its stage, failure code and evidence strings
// already exist.
if (previous.length !== 870) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'fedaf3e9e4bc315e7a467c480480ff274c17aefc549c8774e81b71b2e901f319';
const entries = [...previous];
entries.splice(155, 2,
  'dir="ltr" (any ASCII case) is accepted on any element as the left-to-right direction already in effect; the PDF bytes equal the page without it.',
  'dir="rtl", dir="auto" and an empty value still fail FPDF1103; [dir] attribute selectors still fail FPDF1203; no bidirectional layout.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 870 || englishAudit[157] !== previous[157] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete dir-attribute English audit.');
