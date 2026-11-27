import { englishAudit as previous } from './audit-english-integrated-features.mjs';

// The tbody, html and body limitations name the implied tbody, the html canvas and the shrinking flex-item body; three strings replaced in place.
if (previous.length !== 916) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '26bbe810b9dac052ed4de99c773f8b36359f5c3371fd338097c11e8b0259b159';
const entries = [...previous];
entries.splice(35, 1,
  'No propagation of the body background to the canvas (for the html background see the html row); with html{display:flex} a body wider than the page shrinks to it as a flex item; only the listed properties are supported.');
entries.splice(77, 1,
  'Does not create a painted box. background-color on html is the page canvas, covering each whole sheet including its margins (white or transparent paints nothing); width, padding and borders on html still fail; not a full HTML5 document model.');
entries.splice(120, 1,
  'Follows the optional colgroup and optional thead. Rows written directly in the table go into a tbody the HTML parser implies. No multiple tbody elements or rowspan. Continuation does not make images splittable.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 916 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
