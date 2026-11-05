import { englishAudit as previous } from './audit-english-decoration-thickness.mjs';

// Keep the 763 prior translations and all historical source identities.
// Only three feature text fields and the inventory string change in this audit.
if (previous.length !== 763) throw new Error('Prior English audit catalogue changed.');
export const sourceDigest = 'bd2a2ac9291170fc137b19bdef0e10004b758c5b70aed766d4f7d7ea630c7bd7';
const entries = [...previous];
entries[282] = previous[282] + " Independently, text-underline-offset:auto retains byte-identical legacy output. Nonnegative physical pt/px/mm/cm/in/pc/q, own final-font em or computed HTML-root rem of 0–24pt moves the actual PDF underline below the alphabetic baseline, not glyphs, overlines or strike-throughs. It is inherited and supports CSS specificity, source order, !important, inherit/unset and initial reset.";
entries[283] = previous[283].replace("unbounded width, offset,", "unbounded width, negative or greater-than-24pt underline offsets,") + " text-underline-offset affects only one underline; no negative offset, percent, calc, from-font, browser underline-position or standalone-image use. This is not full CSS Text Decoration.";
entries[286] = previous[286] + " The underline-offset engine source commit b8f9b938ec42b108fb6f325715a4dd9b692e027d adds real PDF baseline positions, a separate 44-row A4 physical-point control, em/rem computed font paths, TrueType, managed CLI and protected failure/retry assertions. Exact-head CI and genuinely independent review still require verification; source is not a new binary or human-approved release.";
entries[759] = previous[759] + " As of 5 November 2026, bounded text-underline-offset accepts auto or actual nonnegative 0–24pt physical/em/rem values for PDF underlines only. Negative, percent, calc, from-font and standalone images reject. The 418 stable feature IDs remain unchanged; the accepted scoped CSS names increase from 108 to 109 without claiming complete CSS Text Decoration.";
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 763 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Bounded underline-offset English translation catalogue is incomplete.');
