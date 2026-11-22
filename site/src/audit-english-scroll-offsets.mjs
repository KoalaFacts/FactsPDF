import { englishAudit as previous } from './audit-english-aspect-mask-initial-values.mjs';

// scroll-margin and scroll-padding with their ten side and axis longhands each accept well-formed
// offsets and are dropped. Only the CSS inventory note changes (283 → 305 names); no feature
// row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'c327902d42d44505b36ef9a34b9cbc6324d70ab75177ca6c9eeb8dc8deb3d1fb';
const changes = new Map([
  [779, previous[779] + ' scroll-margin and scroll-padding with their ten side and axis longhands only offset a scroll-into-view target, and a PDF page is never scrolled to an anchor, so well-formed offsets (lengths for scroll-margin; auto, non-negative lengths or percentages for scroll-padding) are validated and dropped while auto for scroll-margin, negative padding, calc() and too many components still fail FPDF1202; this took it from 283 to 305.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 283 to 305') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete scroll-offsets English audit.');
