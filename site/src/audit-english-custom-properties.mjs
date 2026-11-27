import { englishAudit as previous } from './audit-english-root-pseudo-class.mjs';

// The custom property / var() row moves to partial. Its own scope and limitation (indices 628 and
// 629) are replaced, and its stage becomes the existing 语义/渲染, so the 仅语法可表示 stage it
// shared with the value-function row (index 630) now first appears after that row's scope and
// limitation (index 632); no string is added or removed.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '779497d2cfb1426bfc6d8a0c6fec66b4c27ba0cb451cb0da0e0822a70954b733';
const entries = [...previous];
const stage = previous[630];
entries.splice(628, 3,
  'A --name custom property on any element (stylesheets and style attributes; case-sensitive names; cascades by !important, style attribute, specificity and source order; inherited; inherit/initial/unset accepted). var(--name) and var(--name, fallback) work in the value of every supported property (shorthands such as padding, margin and border, and inside functions) and in custom properties referring to others, resolved in dependency order with cycles invalid; the substituted value is checked exactly like the written-out literal, and the PDF bytes equal the literal page.',
  'Where a browser would unset the declaration at computed-value time (a reference with no value and no fallback, a used reference cycle, a substituted CSS-wide keyword, a value the property rejects after substitution) FactsPDF fails with FPDF1202 (or the code the literal value would get); losing declarations are substituted and checked too; malformed var() fails FPDF1202 at parse time; revert/revert-layer fail FPDF1202; each substituted value holds at most MaxCssSyntaxNodes syntax nodes, reference chains are at most MaxCssSyntaxDepth deep and substitution work counts against MaxCssMatchOperations (FPDF1205); no var() in SVG presentation attributes, selectors or at-rule preludes, and no @property registration.');
entries.splice(632, 0, stage);
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[632] !== 'Syntax representation only' ||
  englishAudit[630] !== previous[631] || englishAudit[633] !== previous[633] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete custom property English audit.');
