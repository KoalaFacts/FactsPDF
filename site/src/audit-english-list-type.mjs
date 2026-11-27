import { englishAudit as previous } from './audit-english-base.mjs';

// The HTML type attribute on ol, ul and li is now a presentational hint for list-style-type. The
// shared li/ol/ul limitation (index 82), used by no other row, is replaced; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '15c23da834a918f4f26fc7e63b33d9189e1840576bac70edf0316c873ca08983';
const entries = [...previous];
entries.splice(82, 1,
  'Positive ASCII list start/value only (1–999999); reversed is a bounded boolean. HTML type is a presentational hint for list-style-type (1, a, A, i, I on ol/li, case-sensitive; disc, circle, square, none on ul/li); author CSS wins and other values fail FPDF1103. No ul start or ul-li value, CSS counters, list-style images, custom markers, item keep, widows or orphans. Roman numerals stop at 3999; inside is limited to horizontal first-line layout, without RTL, vertical writing or full CSS Lists.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[81] !== previous[81] || englishAudit[83] !== previous[83] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete list type English audit.');
