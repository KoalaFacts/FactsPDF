import { englishAudit as previous } from './audit-english-address.mjs';

// dl, dt and dd now render as block containers like div (dl with the default block margins, dd with
// the start indent). dt reuses the nested-block scope string; dd's scope and the shared limitation
// enter at dd's row, and dl's scope at dl's row. No string leaves the snapshot.
if (previous.length !== 857) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'bcc1ed8d6130682570fab4736558193f590f821d5cafc83cfe033ca2a97119b4';
const entries = [...previous];
entries.splice(47, 0,
  'A block container like div with the HTML default 24pt start indent (blockquote\'s scale): width, padding, background, borders, the CSS cascade and pagination.',
  'Full HTML semantics and Tagged PDF are not implemented; the </dt> and </dd> end tags cannot be omitted; like blockquote, dl and dd cannot sit inside lists or tables.');
entries.splice(53, 0,
  'A block container with the HTML default 8pt top and bottom margins (blockquote\'s scale): width, padding, background, borders, the CSS cascade and pagination.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 860 || englishAudit[49] !== previous[47] || englishAudit[54] !== previous[51] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete description-list English audit.');
