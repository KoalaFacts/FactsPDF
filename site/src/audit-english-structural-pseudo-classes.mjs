import { englishAudit as previous } from './audit-english-table-row-break-inside.mjs';

// Structural pseudo-classes and :not() with compound arguments now match. The attribute-selector
// limitation (index 572) stops listing pseudo-classes as a whole; the pseudo-class and logical
// pseudo-class rows' scope and limitation strings (indices 581–584, used by no other row) are
// replaced by their new scope, limitation, stage and evidence strings (581–587).
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'a7da66ccdffbddd6b896e47618794ed4032c5e271274f013036cada804069321';
const entries = [...previous];
entries.splice(572, 1,
  'Only id/class/title/lang/href/src/alt/hidden and nonempty data-* attribute names. Empty non-equality operands, unknown attributes or operators including !=, multiple or unknown flags, whitespace-split operators, unsupported pseudo-classes, sibling/nested selectors and full CSS Selectors conformance are unsupported; i folds ASCII letters only, not Unicode. Failure preserves an existing PDF.');
entries.splice(581, 4,
  'Structural pseudo-classes :first-child, :last-child, :only-child, :first-of-type, :last-of-type, :only-of-type, :nth-child(), :nth-last-child(), :nth-of-type(), :nth-last-of-type() (odd/even and the CSS Syntax An+B grammar) and :empty are supported. They match what Chrome matches, count as a class in specificity and take part in the cascade, !important and the CSS match budget. Siblings include style, br and the dropped script/template elements, and document-level content is placed in head or body as the HTML parser places it; :empty treats whitespace-only text as content, as Chrome does.',
  'The of S form, other pseudo-classes such as :lang, :root and :hover, pseudo-elements, An+B values beyond ±1000000000, documents whose html/head/body tags the parser would ignore or merge, and :empty on body fail FPDF1203. An unsupported declaration in a matched rule still fails as before (for example a tr background colour, FPDF1201). Failure preserves an existing PDF.',
  'Structural pseudo-class matching in the real PDF cascade (source).',
  'Source, NUnit tests and a differential check against Chrome 158 Element.matches() and computed colours (489 document/stylesheet pairs agree); exact-head CI and independent review are separate gates.',
  ':not() accepts a list of compound selectors (such as :not(:empty), :not(.a, #b) and nested :not()), with the specificity of its most specific argument. It matches what Chrome matches and takes part in the cascade, !important and the CSS match budget.',
  'Combinators inside :not() (complex selectors, which Chrome accepts), empty arguments, and :is()/:where()/:has() fail FPDF1203; :not() nested more than 32 levels deep or given more than 32 arguments fails FPDF1205. Failure preserves an existing PDF.',
  'Negation pseudo-class matching in the real PDF cascade (source).');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 877 || englishAudit[571] !== previous[571] || englishAudit[573] !== previous[573] ||
  englishAudit[588] !== previous[585] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete structural pseudo-class English audit.');
