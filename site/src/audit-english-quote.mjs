import { englishAudit as previous } from './audit-english-menu.mjs';

// q now renders as inline text in quotation marks. Its new scope and limitation enter at its row
// (indices 95 and 96); its stage, failure code and evidence strings already exist. No string
// leaves the snapshot.
if (previous.length !== 864) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'cbd64977f57574930e3872396990e6af3eb650526f0837c103d1e3a30e3cf8a2';
const entries = [...previous];
entries.splice(95, 0,
  'Inline text wrapped in English quotation marks (the HTML default quotes:auto): curly double quotes “” outside and curly single quotes ‘’ for nested q; the marks take the q\'s text style and the CSS cascade; the cite attribute is metadata only.',
  'No lang-dependent localised quotes, CSS quotes or content; normal text without a supplied font uses the ASCII straight quotes " and \'; Tagged PDF is not implemented.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 866 || englishAudit[97] !== previous[95] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete quote-element English audit.');
