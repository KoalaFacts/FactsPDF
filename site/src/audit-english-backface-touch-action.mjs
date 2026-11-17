import { englishAudit as previous } from './audit-english-appearance.mjs';

// backface-visibility (and -webkit-backface-visibility) and touch-action accept
// their keywords and are dropped: no 3D transform or touch input reaches a PDF.
// Only the CSS inventory note changes (142 → 144 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0470e211601dac42561f073d2dc49cc00c6dd37e594759e187c63c4cdfe3b046';
const changes = new Map([
  [779, previous[779] + ' backface-visibility (also spelled -webkit-backface-visibility; it only matters for 3D-transformed elements, and transform accepts only none) and touch-action (which only selects screen touch gestures) accept a single keyword value, which is validated and dropped; this took it from 142 to 144.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 142 to 144') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete backface-touch-action English audit.');
