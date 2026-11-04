import { englishAudit as baseline } from './audit-english-percent-rgb.mjs';

// Preserve 750 earlier audited strings and update only changed named paint values.
if (baseline.length !== 750) throw new Error('Reconcile prior percent-RGB translations first.');
export const sourceDigest = 'd62fe88be45ac98ed0059db4bdec1617be5f3ef33627cb40385a39ccef77b048';
const paint = ' Every one of the 148 standard opaque CSS named colours now maps to fixed six-digit RGB, including rebeccapurple, coral, steelblue, aliceblue and gray/grey aliases. This is actual PDF text, background, border and decoration paint; existing cascade, inheritance, applicable elements, geometry and pagination remain unchanged.';
const changes = Object.freeze({
  157: baseline[157] + paint,
  189: baseline[189] + paint,
  191: baseline[191] + paint,
  193: baseline[193] + paint,
  201: baseline[201] + ' The older seven-spelling restriction is superseded: all 148 standard opaque named colours are now supported, while currentColor remains restricted to its previous border and decoration contexts.',
  253: baseline[253] + paint + ' Still a single solid colour only, not a layered or image background.',
  270: baseline[270] + paint + ' No transparent decoration ink or general CSS text-decoration support.',
  745: baseline[745] + ' A 4 November 2026 source-only audit at c9138808d785986712f17e977fa2371854cb6aed expands 14 previously accepted CSS colour property rows to all 148 standard opaque named values. Independently authored HEX PDF oracle, 64-record paginated report, managed and Native AOT checks, Poppler text/geometry and protected failures are part of the source implementation; CI, separate code review, human PDF approval and customer rights remain distinct. The 418 IDs and 83 scoped CSS property names are unchanged.'
});
export const englishAudit = Object.freeze(baseline.map((value,index)=>
  Object.hasOwn(changes,index) ? changes[index] : value));
if (englishAudit.length !== 750 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete named-colour English audit.');
