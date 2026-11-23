import { englishAudit as previous } from './audit-english-transform-origin.mjs';

// font-synthesis-weight, -style, -small-caps and -position accept none like the font-synthesis
// shorthand and are dropped. Only the CSS inventory note changes (366 → 370 names); no feature
// row moves.
if (previous.length !== 822) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '47607cbc455bd67f44d07bd0374141e58a8b77314bc6a77dcdc0e8f2a2566b37';
const changes = new Map([
  [818, previous[818].replace(/\.$/, '') + '; font-synthesis-weight, font-synthesis-style, font-synthesis-small-caps and font-synthesis-position at none are validated and dropped like the shorthand, because no bold, italic, small-caps or super/subscript face is ever synthesized, while auto and other values still fail FPDF1202; this took it from 366 to 370.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 822 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[818].includes('from 366 to 370') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-synthesis longhand English audit.');
