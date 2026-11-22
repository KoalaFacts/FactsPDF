import { englishAudit as previous } from './audit-english-transform-mask-initial-values.mjs';

// hanging-punctuation at none, hyphenate-character and text-underline-position at auto and
// text-spacing-trim at normal join the initial-value no-ops. Only the CSS inventory note changes
// (266 → 270 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '3b7f1bb48e901f8b485a16735eece024c4864ff23562b49588b89eccaf4fe48c';
const changes = new Map([
  [779, previous[779] + ' hanging-punctuation at none, hyphenate-character and text-underline-position at auto and text-spacing-trim at normal are initial values; the renderer hangs no punctuation, hyphenates nothing automatically, trims no CJK spacing and places underlines itself, so they are validated and dropped while first, trim-start, under and other values still fail FPDF1202; this took it from 266 to 270.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 266 to 270') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete typography-initial-values English audit.');
