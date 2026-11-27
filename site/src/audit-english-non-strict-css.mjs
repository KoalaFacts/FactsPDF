import { englishAudit as previous } from './audit-english-translucent-colours.mjs';

// The rendering-path CSS error recovery row moves to partial with the opt-in non-strict mode. Its
// own scope and limitation (indices 601 and 602) are replaced; its stage becomes the existing
// 语义/渲染, so the 严格拒绝 stage it alone used (index 603) gives way to its new evidence level.
// The custom property row's limitation (index 632) is replaced: substituted CSS-wide keywords now
// apply as the keyword, revert works, and non-strict mode unsets invalid substitutions.
// No string is added or removed.
if (previous.length !== 877) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '6e37caedfbae54ecc5d9cbd6034e28830fc9675fc0550476f94f08ac7ef71b6b';
const entries = [...previous];
entries.splice(601, 3,
  'Strict mode (the default) rejects recovery diagnostics and invalid CSS. The opt-in non-strict mode (.NET WithStrict(false), CLI --strict false) drops CSS that is invalid per the specifications exactly as a browser handles CSS errors (unknown properties and at-rules, values the property grammar does not accept, invalid selector lists together with their whole rule, CSS syntax errors, style elements whose type is not CSS; a var() declaration invalid at computed-value time computes as unset) and reports each as a warning with the code, message and location strict mode would throw; the PDF bytes equal the page with that CSS removed.',
  'Only CSS proven invalid is dropped (superset grammars and name lists checked against Chrome 158 CSSOM); valid CSS that is not implemented (such as position:absolute, :hover, @font-face, display:flex) and every resource limit fail in both modes; values that cannot be proven invalid (properties without a grammar table, values holding a known function, values valid in quirks mode) fail as before; declarations written directly inside @media still fail (Chrome drops the following rule together with them); at most MaxCssWarnings warnings are stored.',
  'Source code, unit tests, Chrome 158 CSSOM and real CLI PDF bytes checked; independent review confirmed separately.');
entries[632] = 'Where a browser would unset the declaration at computed-value time (a reference with no value and no fallback, a used reference cycle, a value the property rejects after substitution) strict mode fails with FPDF1202 (or the code the literal value would get) and non-strict mode computes it as unset with a warning; a substitution that yields one CSS-wide keyword (inherit/initial/unset/revert/revert-layer) applies as that keyword, as in Chrome 158; revert/revert-layer on a custom property act as unset (inherit); losing declarations are substituted and checked too; malformed var() fails FPDF1202 at parse time; each substituted value holds at most MaxCssSyntaxNodes syntax nodes, reference chains are at most MaxCssSyntaxDepth deep and substitution work counts against MaxCssMatchOperations (FPDF1205); no var() in SVG presentation attributes, selectors or at-rule preludes, and no @property registration.';
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 877 || englishAudit[600] !== previous[600] || englishAudit[604] !== previous[604] ||
  englishAudit[631] !== previous[631] || englishAudit[633] !== previous[633] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete non-strict CSS English audit.');
