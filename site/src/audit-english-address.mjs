import { englishAudit as previous } from './audit-english-figure-elements.mjs';

// address now renders as an italic block like div. Its row comes before every other partial HTML
// element, so its shared stage string ("Semantics / rendering") moves up from index 16 to just after
// its new scope and limitation. No string leaves the snapshot.
if (previous.length !== 855) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '4ee01de6cdaaaeb21ac199aac9ace5dd68d3d2e0057a1bbfa1de0c2ce553d64b';
const entries = [...previous];
const [stage] = entries.splice(16, 1);
entries.splice(10, 0,
  'A block container like div whose text is italic by default (needs an italic font chain): width, padding, background, borders, the CSS cascade and pagination.',
  'Full HTML semantics and Tagged PDF are not implemented; without an italic font it fails like em.',
  stage);
export const englishAudit = Object.freeze(entries);
if (stage !== 'Semantics / rendering' || englishAudit.length !== 857 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete address-element English audit.');
