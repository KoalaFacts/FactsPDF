import { englishAudit as previous } from './audit-english-dir.mjs';

// script is now dropped with its data and attributes. Its new scope and limitation enter at its
// row (indices 101 and 102); its stage and evidence strings already exist. No string leaves the
// snapshot.
if (previous.length !== 870) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '5b2a114a2c7356a2fdbdddad428e77cfe92d87a23dd8c63fcfacaaf9a1685751';
const entries = [...previous];
entries.splice(101, 0,
  'No script runs: the element is dropped with its script data and attributes, and the page renders as if it were absent; allowed in head, containers, paragraphs, lists and tables; markup and styles inside script data never apply.',
  'Needs its end tag (a self-closing slash is ignored, as in browsers); counts toward the element limit; not allowed inside title; script data with "<!--" followed by "<script" (the double-escaped form) fails FPDF1101; src is never loaded, no code runs, and noscript is still unsupported.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 872 || englishAudit[103] !== previous[101] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete script-element English audit.');
