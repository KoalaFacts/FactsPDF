import { englishAudit as previous } from './audit-english-bdi.mjs';

// template now drops itself and its inert contents. Its new scope and limitation enter at its row
// (indices 114 and 115); its stage, failure code and evidence strings already exist. No string
// leaves the snapshot.
if (previous.length !== 868) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '29c75b0fb7b7d4c942f0199e03c231a20d751527b7973a1ab0bb91f9eea11172';
const entries = [...previous];
entries.splice(114, 0,
  'The element and its contents are dropped as an inert HTML template fragment: nothing renders, and style elements and style attributes inside it do not apply; allowed in head, containers, lists and tables, and may nest.',
  'Needs its end tag; its contents must still tokenize and count toward the element limit; declarative shadow DOM (shadowrootmode) is not supported (FPDF1103); no script runs and the template is never cloned.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 870 || englishAudit[116] !== previous[114] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete template-element English audit.');
