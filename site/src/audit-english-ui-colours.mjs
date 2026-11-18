import { englishAudit as previous } from './audit-english-scroll-only.mjs';

// caret-color, accent-color, -webkit-tap-highlight-color, scrollbar-color and
// scrollbar-gutter accept auto, their keywords or well-formed colours and are
// dropped: a PDF page has no caret, native control, tap flash or scrollbar.
// Only the CSS inventory note changes (169 → 174 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0e6d561fd1baed5f19f7b8f6e05f752b04c8f0d854021dc0031069b5a5f5eb83';
const changes = new Map([
  [779, previous[779] + ' caret-color, accent-color, -webkit-tap-highlight-color, scrollbar-color and scrollbar-gutter only style a text caret, native form controls, the tap highlight and scrollbars, which a PDF page does not have, so auto, their keywords or well-formed colours (alpha allowed, since nothing is painted) are validated and dropped; this took it from 169 to 174.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 169 to 174') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete ui-colours English audit.');
