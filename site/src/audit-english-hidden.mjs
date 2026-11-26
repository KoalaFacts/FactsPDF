import { englishAudit as previous } from './audit-english-link.mjs';

// The hidden attribute is now [hidden]{display:none} on ordinary report blocks. Its row's own scope
// and limitation (indices 161 and 162, used by no other row) and the display row's limitation
// (index 248, which said the hidden attribute was unsupported) are replaced; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '6fe70843e5ce9b0fd9f5098ca3beba2100dbc8ea76033d07d644343ab59c696d';
const entries = [...previous];
entries.splice(161, 2,
  'The HTML hidden attribute (empty or hidden, any ASCII case) removes the whole block as the user-agent rule [hidden]{display:none} on the ordinary report blocks that accept display:none (p, pre, h1–h6, div, section, article, header, footer, main, nav, aside, hgroup, search, blockquote, figure, figcaption, address, dl, dt, dd, outside lists and tables); author CSS display:block still shows the element.',
  'hidden="until-found" and other values fail FPDF1103; hidden on inline elements such as span, on body/html, img, hr, or inside lists and tables fails FPDF1103; hidden content is still fully validated (HTML/CSS, resources and budgets).');
entries.splice(248, 1,
  'Only display:none or display:block. CSS-wide inherit/initial/unset, inline/flex/grid/contents and all other display values reject. No body/html, span, image, hr, list or table declarations. Hidden markup still undergoes strict HTML/CSS parsing, resource checks and budgets; hiding cannot bypass validation. The HTML hidden attribute is [hidden]{display:none} on the same blocks (see attr.11); no media queries or JavaScript display.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[163] !== previous[163] || englishAudit[249] !== previous[249] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete hidden-attribute English audit.');
