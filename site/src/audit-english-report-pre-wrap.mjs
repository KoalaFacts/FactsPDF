import { englishAudit as previous } from './audit-english-report-pre-line.mjs';
// Three existing white-space Chinese descriptions changed in the 418-row
// source catalogue. Preserve every earlier stable-ID translation.
if (previous.length !== 707) throw new Error('Pre-wrap translation baseline moved.');
export const sourceDigest = '8172e53a310ebae87fe4b78b47e88060ad894fe286894a39fd5a44fff8e2defa';
const additions = {
  262: "Explicit p/h1–h6 (including fixed-column table-cell p) accept normal, nowrap, pre-line and bounded pre-wrap. pre-line collapses ordinary spaces and tabs while preserving authored CRLF/CR/LF/form-feed breaks; pre-wrap retains every literal U+0020 space, including repeats and leading/trailing spaces, and the same authored breaks. Measured PDF glyph positions, wrapping, pages, br and table-cell height are affected.",
  263: "Bounded pre-wrap preserves only literal ASCII U+0020 spaces and does not implement tab stops or other control whitespace: tabs fail explicitly with FPDF1305. No span/container/structural-table declarations, pre, break-spaces, RTL, complex shaping or full CSS Text. Unfit glyphs/spaces fail FPDF1302; invalid values FPDF1202; invalid elements FPDF1201. Existing PDF is preserved.",
  266: "New source implementation and NUnit with real PDF glyph/pagination checks, 72-record report and independently authored collapsed-space control, Linux/Windows Native AOT/Poppler position and output-recovery evidence path. Exact-head CI, independent review, merge and publication remain separate."
};
export const englishAudit = Object.freeze(previous.map((text,index) => additions[index] ?? text));
if (englishAudit.length !== 707 || englishAudit.some(value =>
    typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Invalid pre-wrap English audit catalogue.');
