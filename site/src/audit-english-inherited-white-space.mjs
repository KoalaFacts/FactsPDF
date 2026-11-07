import { englishAudit as previous } from './audit-english-attribute-operators.mjs';
// Preserve the 774 reviewed source-bound translations by their stable ordered
// Chinese-source identity. Only the four existing css.white-space prose fields
// and the appended audit-method note change, never unrelated records.
if (previous.length !== 774) throw new Error('Inherited white-space English baseline changed.');
export const sourceDigest = '0b4b3652a41c8e85562c46920e229e81346ac69f08c218bfb300f74738d45b10';
const updates = new Map([
  [303, 'Explicit p/h1–h6, including p in fixed-column cells, accept normal, nowrap, pre-line, pre-wrap and bounded pre. The same modes on html/body and ordinary div/section/article/blockquote now flow via computed text-style inheritance into descendant report paragraphs/headings, affecting real PDF glyph spacing, authored breaks, wrapping, pagination and positions. Child declarations, !important and inherit/initial/unset may override. pre-line collapses spaces and preserves CRLF/CR/LF/FF breaks, pre-wrap preserves U+0020 and source breaks with soft wrapping, pre preserves both without automatic wrapping. Standalone pre defaults to pre; explicit br still works.'],
  [304, 'Only ordinary document/report containers gain white-space inheritance. Inline/span, list/table structural elements and images still cannot declare it (FPDF1201); fixed-column table p keep existing width constraints. No break-spaces, RTL, complex shaping or full CSS Text. pre/pre-wrap preserve literal U+0020 and source breaks but not tab stops or other control whitespace (FPDF1305); unfit pre content fails FPDF1302, invalid values FPDF1202, and a previous PDF is preserved.'],
  [305, 'Inherited text white-space on html/body and ordinary div/section/article/blockquote outside lists/tables; the real consumers are existing explicit p/h1–h6, including bounded fixed-column cell paragraphs; shared CLI/.NET core.'],
  [306, 'Source and real NUnit PDF tests cover five modes byte-equivalent to explicit child styles, html/body, nested containers, !important and CSS-wide overrides, a 48-row multipage report, anonymous leading-space glyph advances and invalid-input preservation of an existing PDF. Exact-head CI, independent review and public release are separate gates.'],
  [769, previous[769] + ' Additional source-only one-record inherited white-space audit at 488691c604e43c5bd1cf8c2b444e0d7cfb388699: document and ordinary report containers now propagate normal/nowrap/pre-line/pre-wrap/pre through computed text style to actual paragraph/headline PDF layout. Five-mode equivalent explicit-child tests, html/body inheritance, cascade/wide values, 48-entry multipage oracle and protected-invalid-output tests accompany the code; full CI and independent review are separate pending gates. 418 IDs and 116 bounded accepted CSS property names unchanged.']
]);
export const englishAudit = Object.freeze(previous.map((text,index)=>updates.get(index)??text));
if(englishAudit.length!==774 || englishAudit.some(text=>
  typeof text!=='string' || !text.trim() || /[\u3400-\u9fff]/u.test(text)))
  throw new Error('Incomplete inherited white-space English audit.');
