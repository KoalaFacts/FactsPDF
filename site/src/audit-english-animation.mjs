import { englishAudit as previous } from './audit-english-transition.mjs';

// animation and its eight longhands accept a well-formed animation list and
// @keyframes rules are validated; both are dropped: a written PDF page shows
// no keyframe. Only the CSS inventory note changes (151 → 160 names).
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b33eb4174a608a37d4bf41d99fea2155d2cf75cffd02768d63f61a6eef060f72';
const changes = new Map([
  [779, previous[779] + ' animation and its animation-name, -duration, -timing-function, -delay, -iteration-count, -direction, -fill-mode and -play-state longhands only run on screen, and a written PDF page shows no keyframe, so a well-formed animation list is validated and dropped, as is a well-formed @keyframes rule; this took it from 151 to 160.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 151 to 160') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete animation English audit.');
