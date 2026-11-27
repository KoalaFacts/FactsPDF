import { englishAudit as previous } from './audit-english-background-color-adjust.mjs';

// The pseudo-class row moves to partial for :root. Its own feature name, scope and limitation
// (indices 580 to 582, used by no other row) are replaced, and the attribute-selector limitation
// (index 572) now excepts :root; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd49621cad982107d3c06a0d4098b2917f9212e65cf7112aa0ed1a3b00a838359';
const changes = new Map([
  [572, previous[572].replace('pseudo-classes, ', 'pseudo-classes other than :root, ')],
  [580, 'Pseudo-classes :root/:first-child/:nth-child/:lang'],
  [581, 'Only :root: it matches the document root element (html in HTML) with pseudo-class specificity (0,1,0) and works in compound, descendant and child selectors (html:root, :root p, :root > body); where specificity decides nothing, the PDF bytes equal the same rule written on html.'],
  [582, ':first-child, :nth-child, :lang, :hover and every other pseudo-class and pseudo-element still fail FPDF1203; on a page without an html element, :root matches nothing, exactly like the html selector.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 874 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[572].includes('other than :root') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete :root pseudo-class English audit.');
