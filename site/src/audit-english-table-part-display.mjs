import { englishAudit as previous } from './audit-english-anchor.mjs';

// Table parts now accept the display value that names what the table already renders. The tfoot
// row's limitation (index 121) and the display row's limitation (index 248), each used by no other
// row, are replaced; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '60069eb3830d06aad5354a4f0b7b738e3ad59475cdca74ec1c682dc238e70fed';
const entries = [...previous];
entries.splice(121, 1,
  'Only after the tbody and only td cells; not repeated on each page fragment, and tfoot itself takes no box styles such as background or borders; display:table-row-group is accepted and changes nothing, while display:table-footer-group (a footer repeated on every fragment) fails FPDF1202; no multiple tfoot elements or table-structure recovery.');
entries.splice(248, 1,
  'Only display:none or display:block. CSS-wide inherit/initial/unset, inline/flex/grid/contents and all other display values reject. No body/html, span, image, hr, list or table declarations, except that table parts accept the value they already render (table-header-group on thead, table-row-group on tbody/tfoot, table-row on tr, table-cell on td/th) and it changes nothing; every other combination of a table display keyword (including table-footer-group on tfoot and use on non-table elements) fails FPDF1202. Hidden markup still undergoes strict HTML/CSS parsing, resource checks and budgets; hiding cannot bypass validation. The HTML hidden attribute is [hidden]{display:none} on the same blocks (see attr.11); no media queries or JavaScript display.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[122] !== previous[122] || englishAudit[249] !== previous[249] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table-part display English audit.');
