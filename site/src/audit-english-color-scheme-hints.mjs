import { englishAudit as previous } from './audit-english-text-size-adjust-prefixes.mjs';

// color-scheme and forced-color-adjust accept well-formed values and are dropped:
// a PDF page has no preferred or forced colour scheme.
// Only the CSS inventory note changes (178 → 180 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '6e696ee21648ef405b32ba574b216ed9cc356106c1e12fa293a144d3eada23d3';
const changes = new Map([
  [779, previous[779] + ' color-scheme and forced-color-adjust only let a browser pick dark user-agent defaults or opt out of a forced high-contrast palette; a PDF page has no preferred or forced scheme and paints every colour as declared, so well-formed values are validated and dropped; this took it from 178 to 180.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 178 to 180') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete color-scheme-hints English audit.');
