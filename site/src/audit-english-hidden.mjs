import { englishAudit as previous } from './audit-english-link.mjs';

// The hidden attribute is now [hidden]{display:none} on ordinary report blocks. Its row's own scope
// and limitation (indices 161 and 162, used by no other row) and the display row's limitation
// (index 248, which said the hidden attribute was unsupported) and the attribute-selector row's
// limitation (index 572, which gains hidden) are replaced; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '0324cda400c99bb50902855f987b5d5272fddfb615252a1291232abaaa937f95';
const entries = [...previous];
entries.splice(161, 2,
  'The HTML hidden attribute (empty or hidden, any ASCII case) removes the whole block as the user-agent rule [hidden]{display:none} on the ordinary report blocks that accept display:none (p, pre, h1–h6, div, section, article, header, footer, main, nav, aside, hgroup, search, blockquote, figure, figcaption, address, dl, dt, dd, outside lists and tables); author CSS display:block still shows the element.',
  'hidden="until-found" and other values fail FPDF1103; hidden on inline elements such as span, on body/html, img, hr, or inside lists and tables fails FPDF1103; hidden content is still fully validated (HTML/CSS, resources and budgets).');
entries.splice(248, 1,
  'Only display:none or display:block. CSS-wide inherit/initial/unset, inline/flex/grid/contents and all other display values reject. No body/html, span, image, hr, list or table declarations. Hidden markup still undergoes strict HTML/CSS parsing, resource checks and budgets; hiding cannot bypass validation. The HTML hidden attribute is [hidden]{display:none} on the same blocks (see attr.11); no media queries or JavaScript display.');
entries.splice(572, 1,
  'Only id/class/title/lang/href/src/alt/hidden and nonempty data-* attribute names. Empty non-equality operands, unknown attributes or operators including !=, multiple or unknown flags, whitespace-split operators, pseudo-classes, sibling/nested selectors and full CSS Selectors conformance are unsupported; i folds ASCII letters only, not Unicode. Failure preserves an existing PDF.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[163] !== previous[163] || englishAudit[249] !== previous[249] || englishAudit[573] !== previous[573] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete hidden-attribute English audit.');
