import { englishAudit as prior } from './audit-english-current.mjs';

// Exact-source overlay of the existing reviewed English audit. Unchanged
// strings reuse their original translations; new panel margin wording is
// translated without altering raw status, provenance or PDF examples.
if (prior.length !== 673) throw new Error('Reconcile the prior English audit first.');
export const sourceDigest = "f500d4849945d36e581bb6c1ec34bd75a623399e576d47b93f93a5604092fb23";
export const englishAudit = Object.freeze([
  ...prior.slice(0, 199),
  "Ordinary p/h1–h6 retain 1–4-value TRBL margins, actual vertical gaps, bounded rem/em and horizontal percentages/auto. Ordinary div/section/article report panels additionally accept horizontal margin-left/right and margin:0 auto. Free space is resolved against the actual containing width, box-sizing, min/max-width, padding and borders, shifting PDF text, paint and continued fragments. Panel top/bottom margins may only be zero or initial/unset.",
  "Ordinary p/h1–h6 still reject negative, percentage, auto and calc vertical margins. div/section/article panels only gain nonnegative horizontal pt/px/mm/cm/in/%/rem/em/auto and zero/reset vertical margins; nonzero or inherited vertical margins and margin collapse are unsupported. body/lists/table/image/inline elements remain excluded; table-cell p permits only explicit zero horizontal margins and rejects rem/em. Overwide geometry raises FPDF1302 with existing PDF output preserved.",
  ...prior.slice(201, 202),
  "p/h1–h6 (existing bounded scope); div/section/article report panels (horizontal sides and zero vertical sides only)",
  "Horizontal margin-left/right on ordinary p/h1–h6 and div/section/article report panels accept nonnegative pt/px/mm/cm/in/%/rem/em or auto. Percentages use the containing content width, rem the html root size and em the element's final font size. Auto allocates free space outside the actual outer box, moving PDF text X, wrapping, backgrounds/borders and continued page fragments.",
  "Only ordinary p/h1–h6 and div/section/article panels are supported. body, lists, tables, images and inline elements remain excluded. Nonzero table-cell p horizontal margins and all cell rem/em margins reject. Negative values, calc and outer boxes exceeding the containing rectangle (FPDF1302) are unsupported; panel vertical margins remain zero-only. Unchanged templates preserve their original PDF bytes.",
  ...prior.slice(205, 206),
  "p/h1–h6 and div/section/article panels (horizontal); table-cell p only explicit zero",
  ...prior.slice(207, 669),
  "Current renderer semantics accept 52 CSS property names. Bounded relative font-size/line-height and ordinary block width/min/max-width/padding plus paragraph/heading margins use root rem or element em for real PDF geometry, wraps and pagination. text-indent and letter/word spacing accept bounded em/rem; computed extra glyph advances remain within 0–24pt. div/section/article panels now accept nonnegative horizontal lengths or auto and margin:0 auto to centre the actual outer box; their vertical margins are zero/initial/unset only. body/list/table/image/inline margins and nonzero general-container vertical margins remain rejected; border and image/table/column relative box lengths remain unsupported. Justification, width constraints and overflow-wrap:break-word are scoped subsets, not full CSS.",
  ...prior.slice(670, 673),
]);
