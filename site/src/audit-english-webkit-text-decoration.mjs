import { englishAudit as previous } from './audit-english-text-decoration-skip.mjs';

// -webkit-text-decoration and its -line, -color and -style longhands are aliases of the standard names.
// Only the CSS inventory note changes (224 → 228 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd061da813734ce0ebe6dca0ba7da23dbaf6f8e931ea21c49248874a3c82f08d3';
const changes = new Map([
  [779, previous[779] + ' -webkit-text-decoration and its -line, -color and -style longhands are aliases of the standard names, sharing their cascade slots, values and errors; this took it from 224 to 228.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 224 to 228') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete webkit-text-decoration English audit.');
