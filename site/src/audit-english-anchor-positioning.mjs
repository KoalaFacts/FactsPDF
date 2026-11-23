import { englishAudit as previous } from './audit-english-animation-timeline.mjs';

// The anchor positioning and top-layer properties accept their initial values and are dropped.
// Only the CSS inventory note changes (323 → 330 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '538c62d6acd03881a14bc69434e22ff152cda7f3f1d7a89049c872b7af5ada5c';
const changes = new Map([
  [779, previous[779] + ' anchor-name, anchor-scope, position-area, position-try, position-try-fallbacks and overlay at none and position-try-order at normal are validated against their initial value and dropped, because every box stays in normal flow and nothing reaches a top layer, while other values still fail FPDF1202; this took it from 323 to 330.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 323 to 330') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete anchor positioning English audit.');
