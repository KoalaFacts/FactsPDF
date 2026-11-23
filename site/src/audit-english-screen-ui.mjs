import { englishAudit as previous } from './audit-english-anchor-positioning.mjs';

// The screen and UI properties accept their initial values and are dropped. Only the CSS
// inventory note changes (330 → 337 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '56ad226b5e6d67a730aca04b68c4365b778153907889fc01c7e69d628bf72096';
const changes = new Map([
  [779, previous[779] + ' interpolate-size:numeric-only, reading-flow:normal, field-sizing:fixed, dynamic-range-limit:no-limit and caret, caret-animation and caret-shape at auto are validated against their initial value and dropped, because nothing animates, no text caret is drawn, form fields keep fixed sizing and colours are never mapped to a high-dynamic-range display, while other values still fail FPDF1202; this took it from 330 to 337.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 330 to 337') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete screen and UI English audit.');
