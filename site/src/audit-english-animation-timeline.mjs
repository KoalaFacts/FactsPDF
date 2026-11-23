import { englishAudit as previous } from './audit-english-scroll-snap.mjs';

// The animation timeline and view-transition properties accept their initial values and are
// dropped. Only the CSS inventory note changes (308 → 323 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '33e1c2325b76cb571a368d2d7c0f12eb7c22a0db7fe67253f956a58e267a47cf';
const changes = new Map([
  [779, previous[779] + ' animation-composition:replace, animation-timeline and view-timeline-inset at auto, animation-range and its -start/-end longhands at normal, scroll-timeline, view-timeline and their -name longhands, timeline-scope, view-transition-name and view-transition-class at none, and scroll-timeline-axis and view-timeline-axis at block are validated against their initial value and dropped, because no animation runs and a PDF page is never scrolled or navigated, while other values still fail FPDF1202; this took it from 308 to 323.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 308 to 323') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete animation timeline English audit.');
