import { englishAudit as previous } from './audit-english-abbr.mjs';

// header, footer, main, nav and aside now render as block containers like div. The two header and
// footer rejection scopes, their shared rejection limitation and their static-inspection evidence
// leave the snapshot; two new strings (the shared scope and the header/footer limitation) arrive.
if (previous.length !== 855) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b6fbeb372c7d50aa8fde1461e3a8f755a8bfb556ae37649ef2c603e2cb3e28c7';
const entries = [...previous];
const removed = [entries.splice(58, 1)[0], ...entries.splice(49, 3)];
entries.splice(17, 0,
  'A nested block container like div: width, padding, background, borders, the CSS cascade and pagination.');
entries.splice(50, 0,
  'Renders as an ordinary block in the document flow, not as a header or footer repeated on every page (that needs PageFurniture / --page-furniture). Full HTML semantics and Tagged PDF are not implemented.');
export const englishAudit = Object.freeze(entries);
if (!removed[0].startsWith('The HTML header element') || !removed[1].startsWith('The HTML footer element') ||
  englishAudit.length !== 853 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete sectioning-element English audit.');
