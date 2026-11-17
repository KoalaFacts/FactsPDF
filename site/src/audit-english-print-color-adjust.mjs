import { englishAudit as previous } from './audit-english-interaction-only-css.mjs';

// print-color-adjust and -webkit-print-color-adjust accept exact/economy and
// are dropped: colours and backgrounds are always painted as declared. Only
// the CSS inventory note changes (135 → 137 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'ec9a6eab8e8d980f3e1b4c8b489c395640127e5b96f17d383b45f9189e889da2';
const changes = new Map([
  [779, previous[779] + ' print-color-adjust and its common prefixed spelling -webkit-print-color-adjust accept exact or economy, which is validated and dropped because colours and backgrounds are always painted exactly as declared; this took it from 135 to 137.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 135 to 137') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete print-color-adjust English audit.');
