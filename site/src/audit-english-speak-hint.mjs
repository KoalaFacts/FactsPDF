import { englishAudit as previous } from './audit-english-legacy-initial-values.mjs';

// speak accepts its CSS Speech and CSS 2 aural keywords and is dropped.
// Only the CSS inventory note changes (219 → 220 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'e1f8bb548cc74611d7ecf3b111e9215ac80dd38ef55abb75846e5b77a05924dd';
const changes = new Map([
  [779, previous[779] + ' speak accepts the CSS Speech keywords and the CSS 2 aural keywords (none, normal, spell-out) that icon-font stylesheets still write; the renderer speaks nothing, so they are validated and dropped while other values fail FPDF1202; this took it from 219 to 220.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 219 to 220') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete speak-hint English audit.');
