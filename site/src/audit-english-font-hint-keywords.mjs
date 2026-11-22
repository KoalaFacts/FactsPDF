import { englishAudit as previous } from './audit-english-speak-hint.mjs';

// text-decoration-skip-ink accepts auto or none and font-synthesis none; both are dropped.
// Only the CSS inventory note changes (220 → 222 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '1774fb2d3d17120190f6593205dcaaaa8c4181905175820f1b3423505698cdc3';
const changes = new Map([
  [779, previous[779] + ' text-decoration-skip-ink accepts auto or none and font-synthesis accepts none: the renderer synthesizes no bold or italic face and never breaks an underline around descenders, which is what those values allow, so they are validated and dropped while text-decoration-skip-ink:all and other font-synthesis values fail FPDF1202; this took it from 220 to 222.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 220 to 222') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-hint-keywords English audit.');
