import { englishAudit as previous } from './audit-english-legacy-prefix-aliases.mjs';

// The -webkit-/-moz-/-ms-user-select spellings become aliases of user-select, and
// -webkit-user-drag, -webkit-touch-callout and -ms-overflow-style are dropped hints.
// Only the CSS inventory note changes (195 → 201 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '44e487d21a6707d32f4c13c9bfe3b54a110a9c1fabf1e8fe05d1fd471a0163ed';
const changes = new Map([
  [779, previous[779] + ' -webkit-/-moz-/-ms-user-select are prefixed aliases of user-select, and -webkit-user-drag, -webkit-touch-callout and -ms-overflow-style only steer screen dragging, the iOS long-press callout and the legacy Edge/IE scrollbar, which a PDF page does not have, so their keywords are validated and dropped while other values fail FPDF1202; this took it from 195 to 201.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 195 to 201') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete screen-gesture-hints English audit.');
