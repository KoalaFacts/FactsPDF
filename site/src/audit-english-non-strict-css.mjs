import { englishAudit as previous } from './audit-english-custom-properties.mjs';

// The rendering-path CSS error recovery row moves to partial with the opt-in non-strict mode. Its
// own scope and limitation (indices 599 and 600) are replaced; its stage becomes the existing
// 语义/渲染, so the 严格拒绝 stage it alone used (index 601) gives way to its new evidence level.
// No string is added or removed.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'aef11b62ae75664f8a0372ff5b4b1884e9d689dba27a9c91469721cc785a5866';
const entries = [...previous];
entries.splice(599, 3,
  'Strict mode (the default) rejects recovery diagnostics and invalid CSS. The opt-in non-strict mode (.NET WithStrict(false), CLI --strict false) drops CSS that is invalid per the specifications exactly as a browser handles CSS errors (unknown properties and at-rules, values the property grammar does not accept, invalid selector lists together with their whole rule, CSS syntax errors; a var() declaration invalid at computed-value time computes as unset) and reports each as a warning with the code, message and location strict mode would throw; the PDF bytes equal the page with that CSS removed.',
  'Only CSS proven invalid is dropped (superset grammars and name lists checked against Chrome 158 CSSOM); valid CSS that is not implemented (such as position:absolute, :hover, @page, display:flex) and every resource limit fail in both modes; values that cannot be proven invalid (properties without a grammar table, values holding a known function, values valid in quirks mode) fail as before; declarations written directly inside @media still fail; at most MaxCssWarnings warnings are stored.',
  'Source code, unit tests, Chrome 158 CSSOM and real CLI PDF bytes checked; independent review confirmed separately.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[598] !== previous[598] || englishAudit[602] !== previous[602] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete non-strict CSS English audit.');
