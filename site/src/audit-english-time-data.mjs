import { englishAudit as previous } from './audit-english-phrasing-elements.mjs';

// time and data now render as inline text. Three new strings (two scopes and one shared
// limitation); every earlier string keeps its order.
if (previous.length !== 850) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'fce666d72a2aa0d745aa464d661e92affe26380113615eef4e233e7bedbf36dc';
const scope = (tag, attribute) => `<${tag}> renders as inline text with no default style; its ${attribute} attribute is machine-readable metadata, accepted and not drawn. CSS overrides, TrueType, line wrapping, table cells and pagination work.`;
const entries = [...previous];
entries.splice(40, 0, scope('data', 'value'),
  'Inline text only; block descendants are unsupported. The machine-readable value is not written to PDF metadata; other unsupported attributes are rejected explicitly.');
entries.splice(105, 0, scope('time', 'datetime'));
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 853 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete time and data English audit.');
