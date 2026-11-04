import { englishAudit as prior } from './audit-english-logical-border-components.mjs';

// Preserve every earlier translated feature. The previous shared
// "syntax-only" stage remains in use later, so it moves to index 508.
if (prior.length !== 750 || typeof prior[493] !== 'string' ||
    typeof prior[495] !== 'string')
  throw new Error('Review the prior print-media audit catalogue before updating.');
export const sourceDigest = '93877b4a79b606cfe71bf8c4abb62c6868eb5ad847ab0647a033ff962904953f';
const entries = [
  ...prior.slice(0, 493),
  'An embedded stylesheet can use a single @media print or @media all block. Its existing supported selectors and property values drive real PDF text, backgrounds, border paint, font metrics, cascade and pagination.',
  'Only one print or all media type per rule. Screen, media conditions, comma lists, nested @media, @supports, @container, @import and @page reject with FPDF1204; malformed or unmatched declarations still reject with FPDF1202. Not full CSS Media Queries.',
  'Scoped print-media rule / actual PDF rendering',
  ...prior.slice(496, 508),
  prior[495],
  ...prior.slice(508)
];
entries[746] = prior[745] + ' On 4 November 2026, source 70f37e7e881bade30851ecaa06528a27813f0403 added bounded print/all media-block execution to existing rules. The single existing rule.media record changes from unsupported to partial; 418 stable IDs and 107 scoped property names remain. Real PDF 44-row oracle, TrueType, CLI, native qpdf/Poppler, strict unsupported-query errors and original-output recovery are source evidence; final CI, independent review, human approval, software distribution and customer licensing remain separate.';
entries[747] = prior[746] + ' As of 4 November 2026, single @media print/all blocks run supported styles through the real PDF renderer. Screen/compound/conditional/nested media and other at-rules are not implemented. The 418 audited records and 107 bounded CSS property names remain unchanged.';
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 751 ||
    englishAudit.some(x => typeof x !== 'string' || !x.trim() || /[\u3400-\u9fff]/u.test(x)))
  throw new Error('Print-media English catalogue is incomplete.');
