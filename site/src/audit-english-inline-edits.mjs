import { englishAudit as prior } from './audit-english-print-media.mjs';

// Preserve every reviewed historical translation and insert only the eight
// new scoped HTML feature descriptions (five scopes, one shared limitation,
// one shared stage and one shared evidence string).
if (prior.length !== 751) throw new Error('Review the prior inline-markup English catalogue before updating.');
const entries = [...prior];
entries.splice(31, 0, "<del> renders outdated quotation text with real measured PDF strike-through; existing CSS overrides, TrueType, line wrapping, pagination and original output protection continue to work.");
entries.splice(32, 0, "Inline text only; block descendants and full HTML semantics are unsupported. del/ins datetime and cite attributes reject explicitly; removed text remains searchable and is not redacted, and fonts are never discovered automatically.");
entries.splice(33, 0, "Real PDF glyph and decoration rendering (source only)");
entries.splice(34, 0, "Source code and actual PDF/Native verification definitions; completed CI and independent review are tracked separately.");
entries.splice(57, 0, "<ins> renders approved quote text with measured PDF underline; existing CSS overrides, TrueType, line wrapping, pagination and original output protection continue to work.");
entries.splice(74, 0, "<s> renders text that no longer applies with a measured PDF strike-through; existing CSS overrides, TrueType, line wrapping, pagination and original output protection continue to work.");
entries.splice(75, 0, "<small> renders secondary text at five-sixths of its computed parent font size, affecting measured PDF glyphs, wrapping and pagination; CSS overrides and resource protection still apply.");
entries.splice(96, 0, "<u> renders a measured PDF underline; existing CSS overrides, TrueType, line wrapping, pagination and original output protection continue to work.");
export const sourceDigest = '163f7b98ecee48ef14b6be8d0c4a727cfa65efbe1570403eb7bbcfea7907f6c8';
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 759 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Semantic inline-markup English catalogue is incomplete.');
