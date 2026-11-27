import { englishAudit as previous } from './audit-english-overflow-wrap-anywhere.mjs';

// base now accepts one element in head as a no-op. Its new scope and limitation enter at its row
// (indices 25 and 26). Its stage string already exists but now first occurs at this row, so it
// moves from index 65 to 27; its evidence string already occurs earlier. No string leaves the snapshot.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '250ee351cb14f6884eb4441f8054f6cebc212dfb0a5cb05483b5717b2a0cf3a6';
const entries = [...previous];
const [stage] = entries.splice(65, 1);
entries.splice(25, 0,
  'One <base> in head (such as the <base href="/"> that server-side frameworks emit) is accepted and the page renders as if it were absent: FactsPDF never resolves relative URLs (links must be absolute https or mailto, images are named resources), so href is never read and takes any value; target follows the a rule.',
  'Relative URLs are never resolved against base: a relative a href still fails FPDF1103 exactly as without it; a base with neither href nor target, other attributes (media, style, hidden and the like) and an empty or other underscore target fail FPDF1103; a second base, a base outside head and </base> fail FPDF1101; counts toward the element limit.',
  stage);
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[27] !== previous[65] || englishAudit[28] !== previous[25] ||
  englishAudit[67] !== previous[64] || englishAudit[68] !== previous[66] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete base-element English audit.');
