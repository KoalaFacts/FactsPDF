import { englishAudit as previous } from './audit-english-root-pseudo-class.mjs';

// The custom property / var() row moves to partial. Its own scope and limitation (indices 630 and
// 631) are replaced, and its stage becomes the existing 语义/渲染, so the 仅语法可表示 stage it
// shared with the value-function row (index 632) now first appears after that row's scope and
// limitation (index 634); no string is added or removed.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'ec7b30b4a5fa54a67a3b88ec6bd96f45b142b120037d2592ebc09976018f5ab0';
const entries = [...previous];
const stage = previous[632];
entries.splice(630, 3,
  'A --name custom property on any element (stylesheets and style attributes; case-sensitive names; cascades by !important, style attribute, specificity and source order; inherited; inherit/initial/unset accepted). var(--name) and var(--name, fallback) work in the value of every supported property (shorthands such as padding, margin and border, and inside functions) and in custom properties referring to others, resolved in dependency order with cycles invalid; the substituted value is checked exactly like the written-out literal, and the PDF bytes equal the literal page.',
  'Where a browser would unset the declaration at computed-value time (a reference with no value and no fallback, a used reference cycle, a substituted CSS-wide keyword, a value the property rejects after substitution) FactsPDF fails with FPDF1202 (or the code the literal value would get); losing declarations are substituted and checked too; malformed var() fails FPDF1202 at parse time; revert/revert-layer fail FPDF1202; each substituted value holds at most MaxCssSyntaxNodes syntax nodes, reference chains are at most MaxCssSyntaxDepth deep and substitution work counts against MaxCssMatchOperations (FPDF1205); no var() in SVG presentation attributes, selectors or at-rule preludes, and no @property registration.');
entries.splice(634, 0, stage);
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[634] !== 'Syntax representation only' ||
  englishAudit[632] !== previous[633] || englishAudit[635] !== previous[635] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete custom property English audit.');
