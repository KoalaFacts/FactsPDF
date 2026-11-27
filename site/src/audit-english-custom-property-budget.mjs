import { englishAudit as previous } from './audit-english-auto-table-mixed-widths.mjs';

// The functions.var limitation names the MaxCssCustomProperties budget; that one string is replaced, none added.
if (previous.length !== 877) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '535a15b0ccf49e383667b29ca0e11fa33aef41f33c5317687f285ee823a36e72';
const entries = [...previous];
entries.splice(632, 1,
  'Where a browser would unset the declaration at computed-value time (a reference with no value and no fallback, a used reference cycle, a substituted CSS-wide keyword, a value the property rejects after substitution) FactsPDF fails with FPDF1202 (or the code the literal value would get); losing declarations are substituted and checked too; malformed var() fails FPDF1202 at parse time; revert/revert-layer fail FPDF1202; each substituted value holds at most MaxCssSyntaxNodes syntax nodes, reference chains are at most MaxCssSyntaxDepth deep and substitution work counts against MaxCssMatchOperations (FPDF1205); a page may use at most MaxCssCustomProperties distinct custom property names (default 4096, a repeated name counts once; FPDF1205 beyond it); no var() in SVG presentation attributes, selectors or at-rule preludes, and no @property registration.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 877 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete English audit layer.');
