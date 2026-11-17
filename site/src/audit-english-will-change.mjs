import { englishAudit as previous } from './audit-english-backface-touch-action.mjs';

// will-change accepts auto or a comma list of feature names and is dropped:
// a written PDF page never animates. Only the CSS inventory note changes
// (144 → 145 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '66e675c0243a09f90a0b175d289b8bcd7183114fbd762bef7326b8345da5dbc6';
const changes = new Map([
  [779, previous[779] + ' will-change only tells a browser compositor which features are about to animate, and a written PDF page never changes, so it accepts auto or a comma-separated list of property names, which is validated and dropped; this took it from 144 to 145.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 144 to 145') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete will-change English audit.');
