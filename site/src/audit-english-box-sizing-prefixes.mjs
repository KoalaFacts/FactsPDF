import { englishAudit as previous } from './audit-english-ui-colours.mjs';

// -webkit-box-sizing and -moz-box-sizing are legacy engine spellings of
// box-sizing and share its cascade slot, values and applicability.
// Only the CSS inventory note changes (174 → 176 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'e336b3ee8ffe89fd1171a7ce741cfe0c195c8f732f61c268aa36d6ad15af7c2f';
const changes = new Map([
  [779, previous[779] + ' -webkit-box-sizing and -moz-box-sizing are the legacy engine spellings of box-sizing and share its cascade slot, values and applicability; this took it from 174 to 176.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 174 to 176') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete box-sizing-prefixes English audit.');
