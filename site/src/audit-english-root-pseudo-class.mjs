import { englishAudit as previous } from './audit-english-runtime-targets.mjs';

// The pseudo-class row moves to partial for :root. Its own feature name, scope and limitation
// (indices 582 to 584, used by no other row) are replaced, and the attribute-selector limitation
// (index 574) now excepts :root; no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'cf4ff1dc6a955acf122bf49ed835dc2ffae7fc54835cde6a61420230ac3651f7';
const changes = new Map([
  [574, previous[574].replace('pseudo-classes, ', 'pseudo-classes other than :root, ')],
  [582, 'Pseudo-classes :root/:first-child/:nth-child/:lang'],
  [583, 'Only :root: it matches the document root element (html in HTML) with pseudo-class specificity (0,1,0) and works in compound, descendant and child selectors (html:root, :root p, :root > body); where specificity decides nothing, the PDF bytes equal the same rule written on html.'],
  [584, ':first-child, :nth-child, :lang, :hover and every other pseudo-class and pseudo-element still fail FPDF1203; on a page without an html element, :root matches nothing, exactly like the html selector.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 876 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[574].includes('other than :root') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete :root pseudo-class English audit.');
