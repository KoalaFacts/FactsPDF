import { englishAudit as previous } from './audit-english-will-change.mjs';

// transition and its five longhands accept a well-formed transition list and are
// dropped: a written PDF page is never restyled. Only the CSS inventory note
// changes (145 → 151 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd7221af01e2c5acb81ee01beff529f48d37044474ba98e97b7c76393588bd184';
const changes = new Map([
  [779, previous[779] + ' transition and its transition-property, -duration, -timing-function, -delay and -behavior longhands only animate a change of computed value on screen, and a written PDF page is never restyled, so a well-formed transition list is validated and dropped; this took it from 145 to 151.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 145 to 151') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete transition English audit.');
